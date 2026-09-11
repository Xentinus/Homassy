using Homassy.API.Context;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Models.Activity;
using Homassy.API.Models.Notification;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json;

namespace Homassy.API.Functions
{
    /// <summary>
    /// The notification centre's reads and writes (#116): the caller's own inbox, its unread
    /// count, read state, dismissal - and the write side the notification workers use to record
    /// what they send.
    /// </summary>
    /// <remarks>
    /// Takes only an <see cref="IDbContextFactory{TContext}"/>, which puts it in the closed set of
    /// Functions classes that work in a host with no SignalR hubs (see the API's CLAUDE.md):
    /// <c>Homassy.Notifications</c> borrows this one, and that is the whole point - the delivery
    /// record is written from the same code path that sends the push, so the two cannot diverge.
    /// <para>
    /// Deliberately uncached, unlike most of this layer. An inbox is per-user, changes from
    /// out-of-process workers, and is read once when the drawer opens; a process-wide cache of it
    /// would be invalidated by almost every write and would hold a slice of every user's data in
    /// memory for no gain.
    /// </para>
    /// </remarks>
    public class NotificationFunctions
    {
        /// <summary>
        /// How long a delivered notification is kept. Past this the row is pruned by
        /// <c>Homassy.Notifications</c>'s scheduler.
        /// </summary>
        /// <remarks>
        /// An inbox is a "what did I miss" list, not an archive: the activity feed already keeps
        /// the durable per-family history, and a year-old "1 item was added to Weekly shop" is
        /// noise even to the person it was sent to. Sixty days is comfortably longer than any
        /// plausible gap between two uses of the app.
        /// </remarks>
        public const int RetentionDays = 60;

        /// <summary>Largest page the list endpoint will serve, whatever the caller asks for.</summary>
        public const int MaxPageSize = 50;

        private const int DefaultPageSize = 25;

        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;

        public NotificationFunctions(IDbContextFactory<HomassyDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// One page of the caller's notifications, newest first, plus their total unread count.
        /// </summary>
        /// <remarks>
        /// The cursor is <c>(CreatedAt, PublicId)</c> encoded by <see cref="ActivityCursor"/> -
        /// reused rather than reimplemented: it is a codec for exactly this pair, and its own
        /// remarks explain why the timestamp alone is not a safe page boundary (two rows can share
        /// a tick, and a batch of notifications from one worker iteration routinely do).
        /// </remarks>
        public async Task<NotificationPage> GetNotificationsAsync(
            string? cursor,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var userId = RequireUserId();
            var size = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);

            using var context = _contextFactory.CreateForReading();

            var query = context.Set<UserNotification>()
                .Where(n => n.UserId == userId);

            if (!string.IsNullOrEmpty(cursor))
            {
                if (!ActivityCursor.TryDecode(cursor, out var createdAt, out var publicId))
                {
                    throw new ArgumentException("Malformed notification cursor", nameof(cursor));
                }

                // The compound comparison, not `CreatedAt < createdAt`: rows tied on the
                // timestamp have to be split by the public id or the boundary falls inside the
                // tied group non-deterministically.
                query = query.Where(n =>
                    n.CreatedAt < createdAt
                    || (n.CreatedAt == createdAt && n.PublicId.CompareTo(publicId) < 0));
            }

            // One extra row, to learn whether there is a next page without a second query.
            var rows = await query
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.PublicId)
                .Take(size + 1)
                .ToListAsync(cancellationToken);

            var hasMore = rows.Count > size;
            if (hasMore) rows.RemoveAt(rows.Count - 1);

            var last = rows.Count > 0 ? rows[^1] : null;

            return new NotificationPage
            {
                Items = rows.Select(ToInfo).ToList(),
                NextCursor = hasMore && last != null
                    ? ActivityCursor.Encode(last.CreatedAt, last.PublicId)
                    : null,
                UnreadCount = await CountUnreadAsync(context, userId, cancellationToken)
            };
        }

        /// <summary>The caller's unread notification count.</summary>
        public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
        {
            var userId = RequireUserId();
            using var context = _contextFactory.CreateForReading();
            return await CountUnreadAsync(context, userId, cancellationToken);
        }

        /// <summary>
        /// Marks one of the caller's notifications read. Idempotent, and silently a no-op for a
        /// row that is already read.
        /// </summary>
        /// <returns>The caller's remaining unread count.</returns>
        /// <exception cref="NotificationNotFoundException">
        /// The row does not exist, is dismissed, or belongs to someone else - the three are
        /// deliberately indistinguishable to the caller, so this endpoint cannot be used to probe
        /// whether another user has a given notification.
        /// </exception>
        public async Task<int> MarkReadAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var userId = RequireUserId();
            using var context = _contextFactory.CreateDbContext();

            var notification = await context.Set<UserNotification>()
                .FirstOrDefaultAsync(n => n.PublicId == publicId && n.UserId == userId, cancellationToken);

            if (notification == null)
            {
                throw new NotificationNotFoundException();
            }

            if (notification.ReadAt == null)
            {
                notification.ReadAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
            }

            return await CountUnreadAsync(context, userId, cancellationToken);
        }

        /// <summary>Marks every unread notification of the caller's read.</summary>
        /// <returns>How many rows were changed.</returns>
        public async Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
        {
            var userId = RequireUserId();
            using var context = _contextFactory.CreateDbContext();

            var unread = await context.Set<UserNotification>()
                .Where(n => n.UserId == userId && n.ReadAt == null)
                .ToListAsync(cancellationToken);

            if (unread.Count == 0) return 0;

            var now = DateTime.UtcNow;
            foreach (var notification in unread)
            {
                notification.ReadAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);
            Log.Information("User {UserId} marked {Count} notifications read", userId, unread.Count);
            return unread.Count;
        }

        /// <summary>
        /// Dismisses one of the caller's notifications - the soft delete behind swipe-to-dismiss.
        /// </summary>
        /// <returns>The caller's remaining unread count.</returns>
        public async Task<int> DismissAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var userId = RequireUserId();
            using var context = _contextFactory.CreateDbContext();

            var notification = await context.Set<UserNotification>()
                .FirstOrDefaultAsync(n => n.PublicId == publicId && n.UserId == userId, cancellationToken);

            if (notification == null)
            {
                throw new NotificationNotFoundException();
            }

            // Dismissing an unread notification also reads it: it is gone from the list, and a
            // badge that keeps counting a row nobody can open is a badge that cannot be cleared.
            notification.ReadAt ??= DateTime.UtcNow;
            notification.DeleteRecord(userId);
            await context.SaveChangesAsync(cancellationToken);

            return await CountUnreadAsync(context, userId, cancellationToken);
        }

        /// <summary>
        /// Records notifications for a set of recipients. Called by the notification workers from
        /// the same place they send the push.
        /// </summary>
        /// <remarks>
        /// Takes an explicit <paramref name="context"/> so the caller can commit these rows in the
        /// same unit of work as whatever else it is writing (a delivery marker, a subscription
        /// cleanup), and explicit user ids because a worker acts on behalf of other users - there
        /// is no session here.
        /// <para>
        /// Does <b>not</b> save: the caller owns the commit. That is what lets a worker record the
        /// rows and clean up dead subscriptions in one round trip, and what keeps this from
        /// half-committing a batch.
        /// </para>
        /// </remarks>
        public static void Record(
            HomassyDbContext context,
            IReadOnlyCollection<int> userIds,
            IReadOnlyCollection<NotificationEnvelope> notifications,
            string? targetUrl,
            DateTime createdAt)
        {
            if (userIds.Count == 0 || notifications.Count == 0) return;

            foreach (var userId in userIds)
            {
                foreach (var envelope in notifications)
                {
                    context.Set<UserNotification>().Add(new UserNotification
                    {
                        UserId = userId,
                        Type = envelope.Type,
                        ParametersJson = JsonSerializer.Serialize(envelope.Parameters),
                        TargetUrl = Truncate(targetUrl, 512),
                        CreatedAt = createdAt
                    });
                }
            }
        }

        /// <summary>
        /// Hard-deletes notifications older than <see cref="RetentionDays"/>, dismissed ones
        /// included.
        /// </summary>
        /// <returns>How many rows were removed.</returns>
        /// <remarks>
        /// A real delete, not a soft one: a soft-deleted notification is still a row nobody will
        /// ever read, and this table grows with every worker iteration for every member of every
        /// family. `ExecuteDeleteAsync` so the rows never come into the change tracker - the
        /// point of a retention sweep is not to materialise the data it is discarding.
        /// </remarks>
        public static async Task<int> PruneAsync(
            HomassyDbContext context,
            DateTime now,
            CancellationToken cancellationToken = default)
        {
            var cutoff = now.AddDays(-RetentionDays);

            return await context.Set<UserNotification>()
                .IgnoreQueryFilters()
                .Where(n => n.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        private static Task<int> CountUnreadAsync(HomassyDbContext context, int userId, CancellationToken cancellationToken)
        {
            return context.Set<UserNotification>()
                .Where(n => n.UserId == userId && n.ReadAt == null)
                .CountAsync(cancellationToken);
        }

        private static NotificationInfo ToInfo(UserNotification notification)
        {
            return new NotificationInfo
            {
                PublicId = notification.PublicId,
                // The member name, not the number the column stores - it is the client's i18n key.
                Type = notification.Type.ToString(),
                Parameters = DeserializeParameters(notification.ParametersJson),
                TargetUrl = notification.TargetUrl,
                CreatedAt = notification.CreatedAt,
                IsRead = notification.ReadAt != null
            };
        }

        /// <summary>
        /// Parameters, or an empty set for a row whose JSON cannot be read.
        /// </summary>
        /// <remarks>
        /// Never throws. A row with unreadable parameters is a row whose template renders with
        /// blanks - unhelpful, but it still says which *kind* of thing happened and when, and it
        /// still marks read. Letting one bad row take out the whole page would be the worse
        /// failure, and this is data written by a previous version of our own code, so a shape
        /// change is a live possibility rather than a hypothetical.
        /// </remarks>
        private static IReadOnlyDictionary<string, string> DeserializeParameters(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return NotificationEnvelope.NoParameters;

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? NotificationEnvelope.NoParameters;
            }
            catch (JsonException ex)
            {
                Log.Warning(ex, "Unreadable notification parameters: {Json}", json);
                return NotificationEnvelope.NoParameters;
            }
        }

        private static string? Truncate(string? value, int maxLength)
            => value != null && value.Length > maxLength ? value[..maxLength] : value;

        private static int RequireUserId()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            return userId.Value;
        }
    }
}
