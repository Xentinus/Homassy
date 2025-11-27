using Homassy.API.Context;
using Homassy.API.Entities;
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
        public async Task InitializeCacheAsync()
        {
            var context = new HomassyDbContext();
            var families = await context.Families.AsNoTracking().ToListAsync();

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

        public async Task RefreshCacheAsync(int familyId)
        {
            try
            {
                var context = new HomassyDbContext();
                var family = await context.Families.AsNoTracking().FirstOrDefaultAsync(f => f.Id == familyId);
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
                family = context.Families.AsNoTracking().FirstOrDefault(f => f.Id == familyId);
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
                family = context.Families.AsNoTracking().FirstOrDefault(f => f.ShareCode == shareCode);
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
                    .AsNoTracking()
                    .Where(f => missingIds.Contains(f.Id))
                    .ToList();

                result.AddRange(dbFamilies);
            }

            return result;
        }
        #endregion

        public async Task<Family> CreateFamilyAsync(CreateFamilyRequest request)
        {
            var family = new Family
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                FamilyPictureBase64 = request.FamilyPictureBase64
            };

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Families.Add(family);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return family;
        }

        public async Task AddUserToFamilyAsync(User user, int familyId)
        {
            user.FamilyId = familyId;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task RemoveUserFromFamilyAsync(User user)
        {
            user.FamilyId = null;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(user);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateFamilyAsync(Family family, UpdateFamilyRequest request)
        {
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

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(family);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UploadFamilyPictureAsync(Family family, string familyPictureBase64)
        {
            family.FamilyPictureBase64 = familyPictureBase64;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(family);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteFamilyPictureAsync(Family family)
        {
            family.FamilyPictureBase64 = null;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(family);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
