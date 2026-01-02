using Homassy.API.Context;
using Homassy.API.Entities.Activity;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Models.Activity;
using Homassy.API.Models.Common;
using Homassy.API.Security;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Functions
{
    public class ActivityFunctions
    {
        private static readonly ConcurrentDictionary<int, Activity> _activityCache = new();
        public static bool Inited = false;

        #region Cache Management
        public async Task InitializeCacheAsync(CancellationToken cancellationToken = default)
        {
            var context = new HomassyDbContext();

            // Load recent activities (last 30 days)
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var activities = await context.Activities
                .Where(a => a.Timestamp >= cutoffDate)
                .ToListAsync(cancellationToken);

            try
            {
                foreach (var activity in activities)
                {
                    _activityCache[activity.Id] = activity;
                }

                Inited = true;
                Log.Information($"Initialized activity cache with {activities.Count} recent activities.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize activity cache.");
                throw;
            }
        }

        public async Task RefreshActivityCacheAsync(int activityId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var activity = await context.Activities
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken);

                var existsInCache = _activityCache.ContainsKey(activityId);

                if (activity != null && existsInCache)
                {
                    _activityCache[activityId] = activity;
                    Log.Debug($"Refreshed activity {activityId} in cache.");
                }
                else if (activity != null && !existsInCache)
                {
                    _activityCache[activityId] = activity;
                    Log.Debug($"Added activity {activityId} to cache.");
                }
                else if (activity == null && existsInCache)
                {
                    _activityCache.TryRemove(activityId, out _);
                    Log.Debug($"Removed deleted activity {activityId} from cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for activity {activityId}.");
                throw;
            }
        }
        #endregion

        #region Activity Recording
        public async Task RecordActivityAsync(
            int userId,
            int? familyId,
            ActivityType activityType,
            int recordId,
            string recordName,
            Unit? unit = null,
            decimal? quantity = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();

                var activity = new Activity
                {
                    UserId = userId,
                    FamilyId = familyId,
                    Timestamp = DateTime.UtcNow,
                    ActivityType = activityType,
                    RecordId = recordId,
                    RecordName = recordName,
                    Unit = unit,
                    Quantity = quantity
                };

                context.Activities.Add(activity);
                await context.SaveChangesAsync(cancellationToken);

                Log.Information($"Recorded activity: {activityType} by user {userId} on record {recordId} ({recordName})");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to record activity {activityType} for record {recordId}");
                // Don't throw - activities are "nice to have"
            }
        }
        #endregion

        #region Activity Retrieval
        public async Task<PagedResult<ActivityInfo>> GetActivitiesAsync(
            GetActivitiesRequest request,
            CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
                throw new UnauthorizedException("User not authenticated", ErrorCodes.AuthUnauthorized);

            var context = new HomassyDbContext();

            // Build query - Activities themselves should NOT be filtered by IsDeleted
            // (they inherit from RecordChangeEntity which has soft delete)
            var query = context.Activities
                .IgnoreQueryFilters()
                .AsQueryable();

            // Apply filters
            if (request.ActivityType.HasValue)
                query = query.Where(a => a.ActivityType == request.ActivityType.Value);

            if (request.StartDate.HasValue)
                query = query.Where(a => a.Timestamp >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(a => a.Timestamp <= request.EndDate.Value);

            if (request.UserPublicId.HasValue)
            {
                // Find user by publicId
                var requestedUser = new UserFunctions().GetUserByPublicId(request.UserPublicId.Value);
                if (requestedUser != null)
                {
                    query = query.Where(a => a.UserId == requestedUser.Id);
                }
                else
                {
                    // User not found, return empty result
                    return PagedResult<ActivityInfo>.Create(new List<ActivityInfo>(), 0, request.PageNumber, request.PageSize);
                }
            }
            else
            {
                // Default: Show user's own activities + family activities
                var familyId = SessionInfo.GetFamilyId();
                if (familyId.HasValue)
                    query = query.Where(a => a.UserId == userId.Value || a.FamilyId == familyId.Value);
                else
                    query = query.Where(a => a.UserId == userId.Value);
            }

            // Order by timestamp descending (newest first)
            query = query.OrderByDescending(a => a.Timestamp);

            // Get total count
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply pagination
            if (!request.ReturnAll)
                query = query.Skip(request.Skip).Take(request.PageSize);

            var activities = await query.ToListAsync(cancellationToken);

            // Map to ActivityInfo
            var userFunctions = new UserFunctions();
            var activityInfos = activities.Select(a =>
            {
                var user = userFunctions.GetUserById(a.UserId);
                return new ActivityInfo
                {
                    PublicId = a.PublicId,
                    UserPublicId = user?.PublicId ?? Guid.Empty,
                    UserName = user?.Name ?? "Unknown User",
                    Timestamp = a.Timestamp,
                    ActivityType = a.ActivityType,
                    RecordName = a.RecordName,  // Using cached name from Activity table
                    Unit = a.Unit,
                    Quantity = a.Quantity
                };
            }).ToList();

            return request.ReturnAll
                ? PagedResult<ActivityInfo>.CreateUnpaginated(activityInfos)
                : PagedResult<ActivityInfo>.Create(activityInfos, totalCount, request.PageNumber, request.PageSize);
        }
        #endregion
    }
}
