using Homassy.API.Constants;
using Homassy.API.Context;
using Homassy.Data.Entities.Family;
using Homassy.Data.Entities.Location;
using Homassy.Data.Entities.User;
using Homassy.Data.Exceptions;
using Homassy.API.Models.Family;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Homassy.Data.Context;
using Homassy.Data.Functions;

namespace Homassy.API.Functions
{
    public class FamilyFunctions
    {
        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;
        private readonly UserFunctions _userFunctions;
        private readonly FamilyCache _familyCache;

        public FamilyFunctions(IDbContextFactory<HomassyDbContext> contextFactory, UserFunctions userFunctions, FamilyCache familyCache)
        {
            _contextFactory = contextFactory;
            _userFunctions = userFunctions;
            _familyCache = familyCache;
        }

        #region Family Management
        public async Task<FamilyInfo> CreateFamilyAsync(CreateFamilyRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UnauthorizedException("Invalid authentication");
            }

            var user = _userFunctions.GetUserById(userId.Value);
            if (user == null)
            {
                Log.Warning($"User not found for userId {userId.Value}");
                throw new UserNotFoundException("User not found");
            }

            if (user.FamilyId.HasValue)
            {
                throw new BadRequestException("You are already a member of a family. Please leave your current family first.");
            }

            using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var family = new Family
                {
                    Name = request.Name.Trim(),
                    Description = request.Description?.Trim()
                };

                context.Families.Add(family);
                await context.SaveChangesAsync(cancellationToken);

                user.FamilyId = family.Id;
                context.Users.Update(user);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} created family {family.Id} with share code {family.ShareCode}");

                // Refresh both caches now instead of waiting for the trigger-driven poller: the very
                // next request resolves the caller's family from the cached User row via SessionInfo,
                // so without this the creator appears to have no family for up to one poll interval.
                await _familyCache.RefreshCacheAsync(family.Id, cancellationToken);
                await _userFunctions.RefreshUserCacheAsync(user.Id, cancellationToken);

                // Record activity
                try
                {
                    await ActivityRecorder.RecordAsync(
                        _contextFactory,
                        userId.Value,
                        family.Id,
                        Homassy.Data.Enums.ActivityType.FamilyCreate,
                        family.Id,
                        family.Name,
                        null,
                        null,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record FamilyCreate activity for family {family.Name}");
                }

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

            var user = _userFunctions.GetUserById(userId.Value);
            if (user == null || !user.FamilyId.HasValue)
            {
                throw new InvalidOperationException("You are not a member of any family");
            }

            var family = _familyCache.GetFamilyById(user.FamilyId.Value);
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
                FamilyPictureUrl = MediaUrls.FamilyPicture(family.FamilyPictureVersion)
            };

            return response;
        }

        public List<FamilyMemberResponse> GetFamilyMembersAsync()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var user = _userFunctions.GetUserById(userId.Value);
            if (user == null || !user.FamilyId.HasValue)
            {
                throw new InvalidOperationException("You are not a member of any family");
            }

            var currentPublicId = SessionInfo.GetPublicId();
            using var context = _contextFactory.CreateForReading();

            var members = context.Users
                .Include(u => u.Profile)
                .Where(u => u.FamilyId == user.FamilyId.Value)
                .OrderByDescending(u => u.LastLoginAt)
                // Two projections: the first is the one that runs in SQL (Profile is optional on
                // User, and a null navigation yields NULL there rather than throwing), the second
                // builds the avatar URL, which SQL cannot.
                .Select(u => new
                {
                    u.PublicId,
                    u.Name,
                    DisplayName = u.Profile != null ? u.Profile.DisplayName : string.Empty,
                    u.LastLoginAt,
                    PictureVersion = u.Profile != null ? u.Profile.ProfilePictureVersion : null,
                    IdentityColor = u.Profile != null ? u.Profile.IdentityColor : null
                })
                .ToList()
                .Select(u => new FamilyMemberResponse
                {
                    PublicId = u.PublicId,
                    Name = u.Name,
                    DisplayName = u.DisplayName,
                    LastLoginAt = u.LastLoginAt,
                    ProfilePictureUrl = MediaUrls.ProfilePicture(u.PublicId, u.PictureVersion),
                    IsCurrentUser = u.PublicId == currentPublicId,
                    IdentityColor = u.IdentityColor
                })
                .ToList();

            return members;
        }

        public async Task UpdateFamilyAsync(UpdateFamilyRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var user = _userFunctions.GetUserById(userId.Value);
            if (user == null || !user.FamilyId.HasValue)
            {
                throw new FamilyNotFoundException("You are not a member of any family");
            }

            using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var family = _familyCache.GetFamilyById(user.FamilyId.Value);

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

        #endregion
    }
}
