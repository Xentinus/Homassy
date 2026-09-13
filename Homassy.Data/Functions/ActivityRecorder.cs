using Homassy.Data.Context;
using Homassy.Data.Entities.Activity;
using Homassy.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Data.Functions
{
    /// <summary>
    /// Writes one row to the family activity feed.
    /// </summary>
    /// <remarks>
    /// The write half of the activity feed, separated from the read half on purpose (#91). The
    /// API's <c>ActivityFunctions</c> keeps the reads, and those are backed by a process-wide cache
    /// that only the API initialises; a worker holding the same class could call a cache-backed
    /// read and get an empty answer that looks like an empty feed. Recording is cache-free -
    /// insert a row, save - so it is the part that is safe to share, and this class is the shape
    /// that says so in the type system rather than in a comment.
    /// <para>
    /// Failures are logged and swallowed. An activity row is a "nice to have": losing one is
    /// better than failing the operation that produced it, which is usually the thing the user
    /// actually asked for.
    /// </para>
    /// </remarks>
    public static class ActivityRecorder
    {
        /// <summary>Records one activity, opening its own context from the factory.</summary>
        public static async Task RecordAsync(
            IDbContextFactory<HomassyDbContext> contextFactory,
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
                using var context = contextFactory.CreateDbContext();
                await RecordAsync(context, userId, familyId, activityType, recordId, recordName, unit, quantity, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to record activity {activityType} for record {recordId}");
                // Don't throw - activities are "nice to have"
            }
        }

        /// <summary>
        /// Records one activity into a context the caller owns, so it can be part of the same
        /// transaction as whatever produced it.
        /// </summary>
        public static async Task RecordAsync(
            HomassyDbContext context,
            int userId,
            int? familyId,
            ActivityType activityType,
            int recordId,
            string recordName,
            Unit? unit = null,
            decimal? quantity = null,
            CancellationToken cancellationToken = default)
        {
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
    }
}
