using Homassy.API.Constants;
using Homassy.API.Context;
using Homassy.API.Entities.Family;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Models.Activity;
using Homassy.API.Models.FamilyChat;
using Homassy.API.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Functions
{
    /// <summary>
    /// The family chat's reads and writes (#144): history, sending, deleting - and the broadcast
    /// that follows every committed write.
    /// </summary>
    /// <remarks>
    /// Takes <see cref="FunctionsRuntime"/> rather than a bare context factory because it
    /// broadcasts over SignalR; see that class's own remarks for why the dependencies arrive as
    /// one parameter object.
    /// <para>
    /// <b>Uncached, deliberately</b>, unlike most of this layer. The trigger-driven process-wide
    /// caches exist for slow-changing master data every request reads; a conversation is
    /// write-heavy and read once when the panel opens, so caching it would mean invalidating on
    /// nearly every write while holding a slice of every family's messages in memory.
    /// </para>
    /// <para>
    /// <b>Access is the caller's own family, always.</b> No method here takes a family id: the
    /// only conversation a caller can address is the one their <see cref="SessionInfo"/> says they
    /// are in. That is what makes "a member of another family cannot read or post" true by
    /// construction rather than by a check somebody has to remember to write.
    /// </para>
    /// </remarks>
    public class FamilyChatFunctions
    {
        /// <summary>Largest page the history endpoint will serve, whatever the caller asks for.</summary>
        public const int MaxPageSize = 50;

        private const int DefaultPageSize = 30;

        /// <summary>Longest message body the API accepts, matching the column.</summary>
        public const int MaxBodyLength = 4000;

        /// <summary>Text messages one user may send per <see cref="RateWindow"/>.</summary>
        /// <remarks>
        /// Per user rather than per IP, which is all the rate-limiting middleware can do from a
        /// route template: a family behind one NAT shares an IP bucket, so an IP limit tight
        /// enough to matter would throttle the household rather than the sender. Generous on
        /// purpose - a backstop against a runaway client, not a conversation speed limit.
        /// </remarks>
        public const int SendMaxPerWindow = 40;

        private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);

        private readonly FunctionsRuntime _runtime;
        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;

        public FamilyChatFunctions(FunctionsRuntime runtime)
        {
            _runtime = runtime;
            _contextFactory = runtime.ContextFactory;
        }

        #region Access

        /// <summary>
        /// The caller's family, or <see cref="FamilyChatAccessDeniedException"/> if they have none.
        /// </summary>
        /// <remarks>
        /// "You are in no family" and "that family is not yours" answer the same 403 on purpose:
        /// both mean the conversation the caller asked for is not one they may see, and the client
        /// has one branch to write instead of two.
        /// </remarks>
        public (int FamilyId, Guid FamilyPublicId) RequireFamily()
        {
            var familyId = SessionInfo.GetFamilyId();
            if (!familyId.HasValue)
            {
                throw new FamilyChatAccessDeniedException("You are not a member of any family");
            }

            var family = new FamilyFunctions(_contextFactory).GetFamilyById(familyId.Value)
                ?? throw new FamilyChatAccessDeniedException("You are not a member of any family");

            return (family.Id, family.PublicId);
        }

        #endregion

        #region Reads

        /// <summary>
        /// One page of the caller's family conversation, newest first.
        /// </summary>
        /// <param name="before">
        /// Cursor from a previous page's <see cref="FamilyChatPage.NextCursor"/>, or null for the
        /// newest page.
        /// </param>
        /// <param name="limit">How many messages to return, clamped to <see cref="MaxPageSize"/>.</param>
        /// <param name="cancellationToken">Request cancellation.</param>
        /// <remarks>
        /// The cursor is the <c>(SentAt, PublicId)</c> pair encoded by <see cref="ActivityCursor"/>
        /// - reused rather than reimplemented, since it is a codec for exactly that pair and its
        /// own remarks explain why a timestamp alone is not a safe page boundary. That matters
        /// more here than anywhere else it is used: a burst of messages routinely shares a tick.
        /// </remarks>
        public async Task<FamilyChatPage> GetMessagesAsync(string? before, int limit, CancellationToken cancellationToken = default)
        {
            var (familyId, _) = RequireFamily();
            var size = Math.Clamp(limit <= 0 ? DefaultPageSize : limit, 1, MaxPageSize);

            using var context = _contextFactory.CreateForReading();

            var query = context.Set<FamilyChatMessage>()
                .Where(m => m.FamilyId == familyId);

            if (!string.IsNullOrEmpty(before))
            {
                if (!ActivityCursor.TryDecode(before, out var sentAt, out var publicId))
                {
                    throw new FamilyChatMessageInvalidException("Malformed chat cursor");
                }

                // The compound comparison, not `SentAt < sentAt`: rows tied on the timestamp have
                // to be split by the public id, or the page boundary falls inside the tied group
                // non-deterministically and the reader loses or repeats a message.
                query = query.Where(m =>
                    m.SentAt < sentAt
                    || (m.SentAt == sentAt && m.PublicId.CompareTo(publicId) < 0));
            }

            // One extra row, to learn whether there is an older page without a second query.
            var rows = await query
                .OrderByDescending(m => m.SentAt)
                .ThenByDescending(m => m.PublicId)
                .Take(size + 1)
                .ToListAsync(cancellationToken);

            var hasMore = rows.Count > size;
            if (hasMore) rows.RemoveAt(rows.Count - 1);

            var last = rows.Count > 0 ? rows[^1] : null;
            var senders = LoadSenders(rows.Select(r => r.SenderUserId));

            return new FamilyChatPage
            {
                Items = rows.Select(r => ToInfo(r, senders)).ToList(),
                NextCursor = hasMore && last != null
                    ? ActivityCursor.Encode(last.SentAt, last.PublicId)
                    : null
            };
        }

        #endregion

        #region Writes

        /// <summary>
        /// Posts a text message to the caller's family conversation and broadcasts it.
        /// </summary>
        /// <returns>The committed message, as the sender's own stream should render it.</returns>
        public async Task<FamilyChatMessageInfo> SendMessageAsync(
            SendFamilyChatMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            var (familyId, familyPublicId) = RequireFamily();
            var userId = RequireUserId();

            var body = request.Body?.Trim();
            if (string.IsNullOrEmpty(body))
            {
                throw new FamilyChatMessageInvalidException("A message cannot be empty");
            }

            if (body.Length > MaxBodyLength)
            {
                throw new FamilyChatMessageInvalidException($"A message cannot be longer than {MaxBodyLength} characters");
            }

            RequireSendAllowance(userId);

            var message = new FamilyChatMessage
            {
                FamilyId = familyId,
                SenderUserId = userId,
                Kind = FamilyChatMessageKind.Text,
                Body = body,
                SentAt = DateTime.UtcNow
            };

            using (var context = _contextFactory.CreateDbContext())
            {
                context.Set<FamilyChatMessage>().Add(message);
                await context.SaveChangesAsync(cancellationToken);
            }

            var info = ToInfo(message, LoadSenders([userId]));

            // Broadcast after the commit, never before: a client that renders a message the
            // database went on to reject has no way to find out it is gone.
            await _runtime.FamilyChat.MessageCreatedAsync(familyPublicId, info, request.CorrelationId, cancellationToken);

            Log.Debug("User {UserId} sent chat message {PublicId} to family {FamilyId}", userId, message.PublicId, familyId);
            return info;
        }

        /// <summary>
        /// Soft-deletes one of the caller's own messages and tells the family's group it is gone.
        /// </summary>
        /// <exception cref="FamilyChatMessageNotFoundException">
        /// The message does not exist, is already deleted, belongs to another family, or was sent
        /// by somebody else. Deliberately one answer for all four - see the exception's own notes.
        /// </exception>
        public async Task DeleteMessageAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var (familyId, familyPublicId) = RequireFamily();
            var userId = RequireUserId();

            using (var context = _contextFactory.CreateDbContext())
            {
                var message = await context.Set<FamilyChatMessage>()
                    .FirstOrDefaultAsync(m => m.PublicId == publicId && m.FamilyId == familyId, cancellationToken);

                if (message == null || message.SenderUserId != userId)
                {
                    throw new FamilyChatMessageNotFoundException();
                }

                message.DeleteRecord(userId);
                await context.SaveChangesAsync(cancellationToken);
            }

            await _runtime.FamilyChat.MessageDeletedAsync(familyPublicId, publicId, SessionInfo.GetPublicId(), cancellationToken);
            Log.Debug("User {UserId} deleted chat message {PublicId}", userId, publicId);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Throws if this user has already spent their text allowance for the current window.
        /// </summary>
        /// <remarks>
        /// Note for whoever picks up #82: this reaches into the same process-local
        /// <see cref="RateLimitService"/> the middleware uses, so it inherits that layer's
        /// single-instance scope. A per-user limit that holds across replicas needs the shared
        /// store that issue is about; until then this is exactly as correct as the middleware
        /// next to it.
        /// </remarks>
        private static void RequireSendAllowance(int userId)
        {
            if (RateLimitService.IsRateLimited($"family-chat:send:{userId}", SendMaxPerWindow, RateWindow))
            {
                throw new FamilyChatRateLimitedException();
            }
        }

        /// <summary>
        /// Sender display data for a set of internal user ids, keyed by id.
        /// </summary>
        /// <remarks>
        /// Two cache-backed batch reads rather than a join per row: the users and their profiles
        /// are both already in the Functions layer's process-wide caches, and a page of forty
        /// messages from three people is three distinct senders.
        /// </remarks>
        private Dictionary<int, FamilyChatSenderInfo> LoadSenders(IEnumerable<int> userIds)
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return [];

            var userFunctions = new UserFunctions(_contextFactory);
            var nullableIds = ids.Cast<int?>().ToList();
            var users = userFunctions.GetUsersByIds(nullableIds);
            var profiles = userFunctions.GetUserProfilesByUserIds(nullableIds);

            return users.ToDictionary(u => u.Id, u =>
            {
                var profile = profiles.FirstOrDefault(p => p.UserId == u.Id);
                return new FamilyChatSenderInfo
                {
                    PublicId = u.PublicId,
                    DisplayName = string.IsNullOrWhiteSpace(profile?.DisplayName) ? u.Name : profile!.DisplayName,
                    ProfilePictureUrl = MediaUrls.ProfilePicture(u.PublicId, profile?.ProfilePictureVersion),
                    IdentityColor = profile?.IdentityColor
                };
            });
        }

        private static FamilyChatMessageInfo ToInfo(
            FamilyChatMessage message,
            IReadOnlyDictionary<int, FamilyChatSenderInfo> senders)
        {
            senders.TryGetValue(message.SenderUserId, out var sender);

            return new FamilyChatMessageInfo
            {
                PublicId = message.PublicId,
                Kind = message.Kind,
                Body = message.Body,
                SentAt = message.SentAt,
                EditedAt = message.EditedAt,
                // A sender who cannot be resolved (a member deleted outright) still leaves a
                // readable message rather than taking the whole page down with it.
                Sender = sender ?? new FamilyChatSenderInfo()
            };
        }

        internal static int RequireUserId()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            return userId.Value;
        }

        #endregion
    }
}
