using Homassy.API.Constants;
using Homassy.API.Context;
using Homassy.API.Entities.Activity;
using Homassy.API.Entities.User;
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

        /// <summary>
        /// Same-actor, same-type activities recorded within this many minutes of a run's newest
        /// entry collapse into a single <see cref="ActivityTimelineEntry"/> - e.g. a bulk import
        /// that fires 40 inventory-create activities in a few seconds shows as one card instead of
        /// 40 near-identical ones. Measured from the run's first (newest) row, not a sliding window
        /// against each preceding row, so one run cannot chain indefinitely by staying just under
        /// the gap each time.
        /// </summary>
        private const int TimelineAggregationBucketMinutes = 5;

        /// <summary>
        /// <see cref="GetActivityTimelineAsync"/> over-fetches raw activity rows by this multiple of
        /// the requested page size (plus one) before grouping, because a run that collapses into a
        /// single entry must not starve the page of entries - e.g. with a page size of 30, if the
        /// first 30 raw rows all belonged to one run, fetching only 30 rows would yield just 1 entry.
        /// </summary>
        private const int TimelineOverFetchMultiplier = 4;

        /// <summary>Absolute cap on the raw over-fetch, so a large requested page size cannot force an unbounded query.</summary>
        private const int TimelineMaxRawFetch = 500;

        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;

        public ActivityFunctions(IDbContextFactory<HomassyDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        #region Cache Management
        public async Task InitializeCacheAsync(CancellationToken cancellationToken = default)
        {
            using var context = _contextFactory.CreateForReading();

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
                using var context = _contextFactory.CreateForReading();
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
                using var context = _contextFactory.CreateDbContext();

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

            using var context = _contextFactory.CreateForReading();

            // Build query - Activities themselves should NOT be filtered by IsDeleted
            // (they inherit from RecordChangeEntity which has soft delete)
            var query = context.Activities
                .IgnoreQueryFilters()
                .AsQueryable();

            // Apply filters
            if (request.ActivityType.HasValue)
                query = query.Where(a => a.ActivityType == request.ActivityType.Value);

            if (request.StartDate.HasValue)
                query = query.Where(a => a.Timestamp >= DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc));

            if (request.EndDate.HasValue)
                query = query.Where(a => a.Timestamp <= DateTime.SpecifyKind(request.EndDate.Value, DateTimeKind.Utc));

            var visibilityFilteredQuery = ApplyActivityVisibilityFilter(query, userId.Value, request.UserPublicId);
            if (visibilityFilteredQuery == null)
            {
                // request.UserPublicId named a user that does not exist - empty result rather than an error.
                return PagedResult<ActivityInfo>.Create(new List<ActivityInfo>(), 0, request.PageNumber, request.PageSize);
            }
            query = visibilityFilteredQuery;

            // Order by timestamp descending (newest first)
            query = query.OrderByDescending(a => a.Timestamp);

            // Get total count
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply pagination
            if (!request.ReturnAll)
                query = query.Skip(request.Skip).Take(request.PageSize);

            var activities = await query.ToListAsync(cancellationToken);

            // Map to ActivityInfo
            var userFunctions = new UserFunctions(_contextFactory);
            var activityInfos = activities.Select(a =>
            {
                var user = userFunctions.GetUserById(a.UserId);
                var profile = userFunctions.GetUserProfileByUserId(a.UserId);
                return new ActivityInfo
                {
                    PublicId = a.PublicId,
                    UserPublicId = user?.PublicId ?? Guid.Empty,
                    UserName = user?.Name ?? "Unknown User",
                    IdentityColor = profile?.IdentityColor,
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

        /// <summary>
        /// The "who may see this activity" rule shared by <see cref="GetActivitiesAsync"/> and
        /// <see cref="GetActivityTimelineAsync"/>: one named member's own activities when
        /// <paramref name="filterUserPublicId"/> is given, otherwise the caller's own activities
        /// plus their family's (or just their own, with no family). Kept as one private helper so
        /// the two entry points can never drift apart on who is allowed to see what - that drift
        /// would be a data-leak bug.
        /// </summary>
        /// <returns>
        /// The visibility-filtered query, or <c>null</c> when <paramref name="filterUserPublicId"/>
        /// names a user that does not exist - the caller should then answer an empty result without
        /// querying further.
        /// </returns>
        private IQueryable<Activity>? ApplyActivityVisibilityFilter(
            IQueryable<Activity> query,
            int callerUserId,
            Guid? filterUserPublicId)
        {
            if (filterUserPublicId.HasValue)
            {
                var requestedUser = new UserFunctions(_contextFactory).GetUserByPublicId(filterUserPublicId.Value);
                if (requestedUser == null)
                    return null;

                return query.Where(a => a.UserId == requestedUser.Id);
            }

            // Default: the caller's own activities + their family's.
            var familyId = SessionInfo.GetFamilyId();
            return familyId.HasValue
                ? query.Where(a => a.UserId == callerUserId || a.FamilyId == familyId.Value)
                : query.Where(a => a.UserId == callerUserId);
        }

        /// <summary>
        /// Cursor-paged, server-aggregated activity timeline: same-actor, same-type activities
        /// recorded within <see cref="TimelineAggregationBucketMinutes"/> of a run's newest entry
        /// collapse into one <see cref="ActivityTimelineEntry"/>, and paging follows an opaque
        /// cursor (see <see cref="ActivityCursor"/>) instead of a page number, so a newly inserted
        /// activity cannot shift the window a client is already paging through.
        /// </summary>
        public async Task<ActivityTimelineResult> GetActivityTimelineAsync(
            ActivityTimelineRequest request,
            CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
                throw new UnauthorizedException("User not authenticated", ErrorCodes.AuthUnauthorized);

            var pageSize = request.PageSize > 0 ? request.PageSize : 30;

            var hasCursor = !string.IsNullOrEmpty(request.Cursor);
            DateTime cursorTimestamp = default;
            Guid cursorPublicId = default;
            if (hasCursor && !ActivityCursor.TryDecode(request.Cursor, out cursorTimestamp, out cursorPublicId))
            {
                // An undecodable cursor is a client error, not a silent reset to the first page:
                // silently restarting would make an infinite-scroll client loop forever.
                throw new ArgumentException("The activity timeline cursor is not valid.");
            }

            using var context = _contextFactory.CreateForReading();

            // Activities themselves are not filtered by IsDeleted - see GetActivitiesAsync above.
            var query = context.Activities
                .IgnoreQueryFilters()
                .AsQueryable();

            if (request.ActivityType.HasValue)
                query = query.Where(a => a.ActivityType == request.ActivityType.Value);

            var visibilityFilteredQuery = ApplyActivityVisibilityFilter(query, userId.Value, request.UserPublicId);
            if (visibilityFilteredQuery == null)
                return new ActivityTimelineResult { Entries = [], NextCursor = null };
            query = visibilityFilteredQuery;

            if (hasCursor)
            {
                // Strict "older than the cursor" under (Timestamp, PublicId) treated as a compound
                // key - see ActivityCursor's remarks for why PublicId has to break timestamp ties.
                query = query.Where(a =>
                    a.Timestamp < cursorTimestamp ||
                    (a.Timestamp == cursorTimestamp && a.PublicId.CompareTo(cursorPublicId) < 0));
            }

            query = query
                .OrderByDescending(a => a.Timestamp)
                .ThenByDescending(a => a.PublicId);

            // Over-fetch before grouping (see TimelineOverFetchMultiplier's docs): a run that
            // collapses into one entry must still leave room for a full page of entries.
            var rawFetchSize = Math.Min(pageSize * TimelineOverFetchMultiplier + 1, TimelineMaxRawFetch);
            var rows = await query.Take(rawFetchSize).ToListAsync(cancellationToken);

            // Resolve every actor once - not per row like GetActivitiesAsync's GetUserById-per-row
            // above - so this method does not carry the same N+1 into a path with more rows fetched.
            var distinctUserIds = rows.Select(a => (int?)a.UserId).Distinct().ToList();
            var usersById = new UserFunctions(_contextFactory)
                .GetAllUsersDataByIds(distinctUserIds)
                .ToDictionary(u => u.Id);

            // Group consecutive rows (in the newest-first scan order) that share actor + type and
            // fall within the bucket window measured from the run's first (newest) row.
            var runs = new List<List<Activity>>();
            foreach (var row in rows)
            {
                var currentRun = runs.Count > 0 ? runs[^1] : null;
                if (currentRun != null)
                {
                    var runAnchor = currentRun[0];
                    if (row.UserId == runAnchor.UserId &&
                        row.ActivityType == runAnchor.ActivityType &&
                        runAnchor.Timestamp - row.Timestamp <= TimeSpan.FromMinutes(TimelineAggregationBucketMinutes))
                    {
                        currentRun.Add(row);
                        continue;
                    }
                }

                runs.Add([row]);
            }

            var reachedEndOfData = rows.Count < rawFetchSize;

            List<List<Activity>> pageRuns;
            bool truncated;
            if (runs.Count <= pageSize)
            {
                pageRuns = runs;
                truncated = false;
            }
            else
            {
                pageRuns = runs.Take(pageSize).ToList();
                truncated = true;
            }

            var entries = pageRuns.Select(run => BuildTimelineEntry(run, usersById)).ToList();

            // NextCursor must be encoded from the last row actually consumed by this page - the
            // oldest row of the last included run - never from that run's first (newest) row:
            // encoding from the newest row would leave the older rows of that same run still
            // "after" the cursor, so the next page would re-fetch and re-emit them as duplicates.
            string? nextCursor = null;
            if (truncated)
            {
                var lastConsumedRow = pageRuns[^1][^1];
                nextCursor = ActivityCursor.Encode(lastConsumedRow.Timestamp, lastConsumedRow.PublicId);
            }
            else if (!reachedEndOfData && rows.Count > 0)
            {
                // Every fetched row was consumed, but the fetch returned a full batch - there may
                // be more beyond it. Anchor to the very last row fetched; a next call that finds
                // nothing past it will correctly report NextCursor = null in its turn.
                var lastConsumedRow = rows[^1];
                nextCursor = ActivityCursor.Encode(lastConsumedRow.Timestamp, lastConsumedRow.PublicId);
            }

            return new ActivityTimelineResult
            {
                Entries = entries,
                NextCursor = nextCursor
            };
        }

        private static ActivityTimelineEntry BuildTimelineEntry(List<Activity> run, Dictionary<int, User> usersById)
        {
            var newest = run[0];
            usersById.TryGetValue(newest.UserId, out var user);

            var profile = user?.Profile;
            var profilePictureUrl = MediaUrls.ProfilePicture(user?.PublicId ?? Guid.Empty, profile?.ProfilePictureVersion);

            if (run.Count == 1)
            {
                return new ActivityTimelineEntry
                {
                    PublicId = newest.PublicId,
                    UserPublicId = user?.PublicId ?? Guid.Empty,
                    UserName = user?.Name ?? "Unknown User",
                    UserProfilePictureUrl = profilePictureUrl,
                    UserIdentityColor = profile?.IdentityColor,
                    Timestamp = newest.Timestamp,
                    LastTimestamp = null,
                    ActivityType = newest.ActivityType,
                    RecordName = newest.RecordName,
                    Unit = newest.Unit,
                    Quantity = newest.Quantity,
                    Count = 1,
                    Items = null
                };
            }

            var oldest = run[^1];
            var items = run.Select(a => new ActivityInfo
            {
                PublicId = a.PublicId,
                UserPublicId = user?.PublicId ?? Guid.Empty,
                UserName = user?.Name ?? "Unknown User",
                IdentityColor = profile?.IdentityColor,
                Timestamp = a.Timestamp,
                ActivityType = a.ActivityType,
                RecordName = a.RecordName,
                Unit = a.Unit,
                Quantity = a.Quantity
            }).ToList();

            return new ActivityTimelineEntry
            {
                PublicId = newest.PublicId,
                UserPublicId = user?.PublicId ?? Guid.Empty,
                UserName = user?.Name ?? "Unknown User",
                UserProfilePictureUrl = profilePictureUrl,
                UserIdentityColor = profile?.IdentityColor,
                Timestamp = newest.Timestamp,
                LastTimestamp = oldest.Timestamp,
                ActivityType = newest.ActivityType,
                RecordName = newest.RecordName,
                Unit = newest.Unit,
                Quantity = newest.Quantity,
                Count = run.Count,
                Items = items
            };
        }
        #endregion
    }
}
