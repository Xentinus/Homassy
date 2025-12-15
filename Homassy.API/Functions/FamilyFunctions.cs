using Homassy.API.Context;
using Homassy.API.Entities.Family;
using Homassy.API.Entities.Location;
using Homassy.API.Entities.User;
using Homassy.API.Exceptions;
using Homassy.API.Models.Family;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Functions
{
    public class FamilyFunctions
    {
        private static readonly ConcurrentDictionary<int, Family> _familyCache = new();
        public static bool Inited = false;

        #region Cache Management
        public async Task InitializeCacheAsync(CancellationToken cancellationToken = default)
        {
            var context = new HomassyDbContext();
            var families = await context.Families
                .ToListAsync(cancellationToken);

            try
            {
                foreach (var family in families)
                {
                    _familyCache[family.Id] = family;
                }
                Inited = true;
                Log.Information($"Initialized family cache with {families.Count} families.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize family cache.");
                throw;
            }
        }

        public async Task RefreshCacheAsync(int familyId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var family = await context.Families
                    .FirstOrDefaultAsync(f => f.Id == familyId, cancellationToken);

                var existsInCache = _familyCache.ContainsKey(familyId);

                if (family != null && existsInCache)
                {
                    _familyCache[familyId] = family;
                    Log.Debug($"Refreshed family {familyId} in cache.");
                }
                else if (family != null && !existsInCache)
                {
                    _familyCache[familyId] = family;
                    Log.Debug($"Added family {familyId} to cache.");
                }
                else if (family == null && existsInCache)
                {
                    _familyCache.TryRemove(familyId, out _);
                    Log.Debug($"Removed deleted family {familyId} from cache.");
                }
                else
                {
                    Log.Debug($"Family {familyId} not found in DB or cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for family {familyId}.");
                throw;
            }
        }

        public Family? GetFamilyById(int? familyId)
        {
            if (familyId == null) return null;
            Family? family = null;

            if (Inited)
            {
                _familyCache.TryGetValue((int)familyId, out family);
            }

            if (family == null)
            {
                var context = new HomassyDbContext();
                family = context.Families.FirstOrDefault(f => f.Id == familyId);
            }

            return family;
        }

        public Family? GetFamilyByShareCode(string? shareCode)
        {
            if (string.IsNullOrWhiteSpace(shareCode)) return null;
            Family? family = null;

            if (Inited)
            {
                family = _familyCache.Values.FirstOrDefault(f => f.ShareCode == shareCode);
            }

            if (family == null)
            {
                var context = new HomassyDbContext();
                family = context.Families
                    .FirstOrDefault(f => f.ShareCode == shareCode);
            }

            return family;
        }

        public List<Family> GetFamiliesByIds(List<int?> familyIds)
        {
            if (familyIds == null || !familyIds.Any()) return new List<Family>();

            var validIds = familyIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<Family>();

            var result = new List<Family>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_familyCache.TryGetValue(id, out var family))
                    {
                        result.Add(family);
                    }
                    else
                    {
                        missingIds.Add(id);
                    }
                }
            }
            else
            {
                missingIds = validIds;
            }

            if (missingIds.Count > 0)
            {
                var context = new HomassyDbContext();
                var dbFamilies = context.Families
                    .Where(f => missingIds.Contains(f.Id))
                    .ToList();

                result.AddRange(dbFamilies);
            }

            return result;
        }
        #endregion

        #region Family Management
        public async Task<FamilyInfo> CreateFamilyAsync(CreateFamilyRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UnauthorizedException("Invalid authentication");
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                Log.Warning($"User not found for userId {userId.Value}");
                throw new UserNotFoundException("User not found");
            }

            if (user.FamilyId.HasValue)
            {
                throw new BadRequestException("You are already a member of a family. Please leave your current family first.");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var family = new Family
                {
                    Name = request.Name.Trim(),
                    Description = request.Description?.Trim(),
                    FamilyPictureBase64 = request.FamilyPictureBase64
                };

                context.Families.Add(family);
                await context.SaveChangesAsync(cancellationToken);

                user.FamilyId = family.Id;
                context.Users.Update(user);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} created family {family.Id} with share code {family.ShareCode}");

                var response = new FamilyInfo
                {
                    Name = family.Name,
                    ShareCode = family.ShareCode
                };

                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error($"Error creating family for user {userId.Value}: {ex.Message}");
                throw;
            }
        }

        public FamilyDetailsResponse GetFamilyAsync()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null || !user.FamilyId.HasValue)
            {
                throw new InvalidOperationException("You are not a member of any family");
            }

            var family = GetFamilyById(user.FamilyId.Value);
            if (family == null)
            {
                Log.Warning($"Family not found for familyId {user.FamilyId.Value}");
                throw new FamilyNotFoundException("Family not found");
            }

            var response = new FamilyDetailsResponse
            {
                Name = family.Name,
                Description = family.Description,
                ShareCode = family.ShareCode,
                FamilyPictureBase64 = family.FamilyPictureBase64
            };

            return response;
        }

        public async Task UpdateFamilyAsync(UpdateFamilyRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null || !user.FamilyId.HasValue)
            {
                throw new FamilyNotFoundException("You are not a member of any family");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var family = GetFamilyById(user.FamilyId.Value);

                if (family == null)
                {
                    Log.Warning($"Family not found for familyId {user.FamilyId.Value}");
                    throw new FamilyNotFoundException("Family not found");
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    family.Name = request.Name.Trim();
                }

                if (request.Description != null)
                {
                    family.Description = string.IsNullOrWhiteSpace(request.Description)
                        ? null
                        : request.Description.Trim();
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {SessionInfo.GetUserId()} updated family {family.Id}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error($"Error updating family for user {SessionInfo.GetUserId()}: {ex.Message}");
                throw;
            }
        }

        public async Task UploadFamilyPictureAsync(string familyPictureBase64, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(familyPictureBase64))
            {
                throw new BadRequestException("Family picture data is required");
            }

            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null || !user.FamilyId.HasValue)
            {
                throw new FamilyNotFoundException("You are not a member of any family");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var family = await context.Families.FindAsync(user.FamilyId, cancellationToken);

                if (family == null)
                {
                    Log.Warning($"Family not found for familyId {user.FamilyId}");
                    throw new FamilyNotFoundException("Family not found");
                }

                family.FamilyPictureBase64 = familyPictureBase64;

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {SessionInfo.GetUserId()} uploaded picture for family {user.FamilyId}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error($"Error uploading family picture: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteFamilyPictureAsync(CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null || !user.FamilyId.HasValue)
            {
                throw new FamilyNotFoundException("You are not a member of any family");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var family = await context.Families.FindAsync(user.FamilyId.Value, cancellationToken);

                if (family == null)
                {
                    Log.Warning($"Family not found for familyId {user.FamilyId.Value}");
                    throw new FamilyNotFoundException("Family not found");
                }

                if (string.IsNullOrEmpty(family.FamilyPictureBase64))
                {
                    throw new BadRequestException("No family picture to delete");
                }

                family.FamilyPictureBase64 = null;

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {SessionInfo.GetUserId()} deleted picture for family {user.FamilyId.Value}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error($"Error deleting family picture: {ex.Message}");
                throw;
            }
        }
        #endregion
    }
}
