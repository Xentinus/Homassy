using Homassy.API.Constants;
using Homassy.API.Context;
using Homassy.API.Entities.Family;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Hubs;
using Homassy.API.Models.Activity;
using Homassy.API.Models.ImageUpload;
using Homassy.API.Models.FamilyChat;
using Homassy.API.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

        /// <summary>
        /// Pictures one user may send per <see cref="RateWindow"/> (#147).
        /// </summary>
        /// <remarks>
        /// Its own bucket, far lower than the text one: an image costs a decode, a resize and a
        /// few hundred kilobytes of storage, so the two paths must not share an allowance - a
        /// chatty evening would otherwise spend the budget that stops an upload loop.
        /// </remarks>
        public const int ImageMaxPerWindow = 8;

        /// <summary>
        /// Largest picture the chat accepts, before decoding (#147).
        /// </summary>
        /// <remarks>
        /// Checked against the base64's decoded length *before* the bytes are handed to the image
        /// decoder, which is the point: a decoder is the expensive, attackable part, and the size
        /// of the payload is knowable without running it.
        /// </remarks>
        public const long MaxImageBytes = 8 * 1024 * 1024;

        /// <summary>Longest caption an image message may carry.</summary>
        public const int MaxCaptionLength = 500;

        private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);

        private readonly FunctionsRuntime _runtime;
        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly FamilyChatConnectionState _connectionState;

        public FamilyChatFunctions(
            FunctionsRuntime runtime,
            IImageProcessingService imageProcessingService,
            FamilyChatConnectionState connectionState)
        {
            _runtime = runtime;
            _contextFactory = runtime.ContextFactory;
            _imageProcessingService = imageProcessingService;
            _connectionState = connectionState;
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
            var images = await LoadImageRenditionsAsync(context, rows, cancellationToken);

            var messageIds = rows.Select(r => r.Id).ToList();
            var referenceRows = messageIds.Count == 0
                ? []
                : await context.Set<FamilyChatMessageReference>()
                    .Where(r => messageIds.Contains(r.FamilyChatMessageId))
                    .ToListAsync(cancellationToken);

            var references = HydrateReferences(referenceRows);

            return new FamilyChatPage
            {
                Items = rows.Select(r => ToInfo(r, senders, images, references)).ToList(),
                NextCursor = hasMore && last != null
                    ? ActivityCursor.Encode(last.SentAt, last.PublicId)
                    : null
            };
        }

        /// <summary>
        /// How many messages the caller has not read yet (#149).
        /// </summary>
        /// <remarks>
        /// Counted against the read marker rather than tracked as a number, so it cannot drift: a
        /// message deleted after it arrived stops counting by itself, and a second device marking
        /// the conversation read changes this answer without anything having to be decremented.
        /// The caller's own messages never count - you have read what you just wrote.
        /// </remarks>
        public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
        {
            var (familyId, _) = RequireFamily();
            var userId = RequireUserId();

            using var context = _contextFactory.CreateForReading();
            return await CountUnreadAsync(context, familyId, userId, cancellationToken);
        }

        private static async Task<int> CountUnreadAsync(
            HomassyDbContext context,
            int familyId,
            int userId,
            CancellationToken cancellationToken)
        {
            var lastReadAt = await context.Set<FamilyChatReadState>()
                .Where(r => r.UserId == userId && r.FamilyId == familyId)
                .Select(r => (DateTime?)r.LastReadAt)
                .FirstOrDefaultAsync(cancellationToken);

            return await context.Set<FamilyChatMessage>()
                .Where(m => m.FamilyId == familyId
                    && m.SenderUserId != userId
                    && (lastReadAt == null || m.SentAt > lastReadAt))
                .CountAsync(cancellationToken);
        }

        #endregion

        #region Read state (#149)

        /// <summary>
        /// Moves the caller's read marker to now, and answers with what is left unread.
        /// </summary>
        /// <remarks>
        /// Idempotent and monotonic: a marker never moves backwards, so a second device reporting
        /// a moment that has already passed cannot un-read messages the first one had read. That
        /// also makes this safe to call as often as the client likes, which it does - the panel
        /// marks read on visibility rather than on mount.
        /// <para>
        /// Answers with the new count, the same way every mutating endpoint on the notification
        /// centre does: the act of reading is what corrects the badge, so the client never has to
        /// guess it or fetch it again.
        /// </para>
        /// </remarks>
        public async Task<int> MarkReadAsync(CancellationToken cancellationToken = default)
        {
            var (familyId, _) = RequireFamily();
            var userId = RequireUserId();
            var now = DateTime.UtcNow;

            using var context = _contextFactory.CreateDbContext();

            var state = await context.Set<FamilyChatReadState>()
                .FirstOrDefaultAsync(r => r.UserId == userId && r.FamilyId == familyId, cancellationToken);

            if (state == null)
            {
                context.Set<FamilyChatReadState>().Add(new FamilyChatReadState
                {
                    UserId = userId,
                    FamilyId = familyId,
                    LastReadAt = now
                });

                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception))
                {
                    // Another of this member's devices created the marker between the read above
                    // and this insert - the exact race the unique index exists to refuse, and one
                    // this feature invites: the panel marks read from every device it is open on.
                    // Both calls mean the same thing, so the loser moves the winner's row forward
                    // instead of failing a request that asked for something already true.
                    Log.Debug("Chat read marker for user {UserId} in family {FamilyId} was created concurrently", userId, familyId);

                    using var retryContext = _contextFactory.CreateDbContext();
                    var existing = await retryContext.Set<FamilyChatReadState>()
                        .FirstOrDefaultAsync(r => r.UserId == userId && r.FamilyId == familyId, cancellationToken);

                    if (existing != null && existing.LastReadAt < now)
                    {
                        existing.LastReadAt = now;
                        await retryContext.SaveChangesAsync(cancellationToken);
                    }

                    return await CountUnreadAsync(retryContext, familyId, userId, cancellationToken);
                }
            }
            else
            {
                // The marker never moves backwards, so a device reporting a moment that has already
                // passed cannot un-read messages another device had read.
                if (state.LastReadAt < now)
                {
                    state.LastReadAt = now;
                }

                await context.SaveChangesAsync(cancellationToken);
            }

            return await CountUnreadAsync(context, familyId, userId, cancellationToken);
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
            var requested = request.References ?? [];

            // Empty is only empty when there is nothing attached either: a message that is just a
            // product and a shop says something.
            if (string.IsNullOrEmpty(body) && requested.Count == 0)
            {
                throw new FamilyChatMessageInvalidException("A message cannot be empty");
            }

            if (body?.Length > MaxBodyLength)
            {
                throw new FamilyChatMessageInvalidException($"A message cannot be longer than {MaxBodyLength} characters");
            }

            if (requested.Count > SendFamilyChatMessageRequest.MaxReferences)
            {
                throw new FamilyChatMessageInvalidException(
                    $"A message cannot point at more than {SendFamilyChatMessageRequest.MaxReferences} things");
            }

            RequireSendAllowance(userId);

            // Resolved before the message is written, so a reference the sender cannot actually see
            // fails the send rather than becoming a chip nobody can open.
            var resolved = ResolveReferences(requested);

            var message = new FamilyChatMessage
            {
                FamilyId = familyId,
                SenderUserId = userId,
                Kind = FamilyChatMessageKind.Text,
                Body = string.IsNullOrEmpty(body) ? null : body,
                SentAt = DateTime.UtcNow
            };

            using (var context = _contextFactory.CreateDbContext())
            {
                // One transaction over both saves. The message has to be saved first for its id -
                // the reference rows' foreign key needs it - but a message that committed while
                // its references did not is a message everyone else sees having silently lost the
                // product it was pointing at, while its sender was told the send failed.
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    context.Set<FamilyChatMessage>().Add(message);
                    await context.SaveChangesAsync(cancellationToken);

                    if (resolved.Count > 0)
                    {
                        foreach (var reference in resolved)
                        {
                            context.Set<FamilyChatMessageReference>().Add(new FamilyChatMessageReference
                            {
                                FamilyChatMessageId = message.Id,
                                Kind = reference.Kind,
                                TargetPublicId = reference.PublicId,
                                Label = reference.Label
                            });
                        }

                        await context.SaveChangesAsync(cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    Log.Error(ex, "Failed to store a chat message for family {FamilyId}", familyId);
                    throw;
                }
            }

            var info = ToInfo(message, LoadSenders([userId]), NoImages, new Dictionary<int, List<FamilyChatReferenceInfo>>
            {
                [message.Id] = resolved
            });

            await ClearTypingAsync(familyPublicId, userId, cancellationToken);

            // Broadcast after the commit, never before: a client that renders a message the
            // database went on to reject has no way to find out it is gone.
            await _runtime.FamilyChat.MessageCreatedAsync(familyPublicId, info, request.CorrelationId, cancellationToken);

            Log.Debug("User {UserId} sent chat message {PublicId} to family {FamilyId}", userId, message.PublicId, familyId);
            return info;
        }

        /// <summary>
        /// Posts a picture to the caller's family conversation and broadcasts it (#147).
        /// </summary>
        /// <remarks>
        /// The message row and its bytes are committed in one transaction, and the broadcast comes
        /// after that commit. That ordering is the whole reason this is one method rather than a
        /// message send followed by an upload: a <c>MessageCreated</c> whose picture is not stored
        /// yet is a message every client renders with an image that 404s.
        /// <para>
        /// The stored bytes are a resized rendition plus a thumbnail, never the camera's original -
        /// a modern phone photo is several megabytes, and a conversation would accumulate them
        /// forever. <c>IImageProcessingService</c> also strips EXIF on the way through, which for a
        /// chat matters more than anywhere else in this app: a photo taken at home carries the
        /// coordinates of the house.
        /// </para>
        /// </remarks>
        public async Task<FamilyChatMessageInfo> SendImageMessageAsync(
            SendFamilyChatImageRequest request,
            CancellationToken cancellationToken = default)
        {
            var (familyId, familyPublicId) = RequireFamily();
            var userId = RequireUserId();

            RequireImageAllowance(userId);

            var caption = request.Caption?.Trim();
            if (caption?.Length > MaxCaptionLength)
            {
                throw new FamilyChatMessageInvalidException($"A caption cannot be longer than {MaxCaptionLength} characters");
            }

            var options = new ImageProcessingOptions
            {
                MaxWidth = 1600,
                MaxHeight = 1600,
                MinWidth = 16,
                MinHeight = 16,
                MaxFileSizeBytes = MaxImageBytes,
                JpegQuality = 80,
                AllowedFormats = [ImageFormat.Jpeg, ImageFormat.Png, ImageFormat.WebP]
            };

            // Size and type are checked before anything decodes the bytes: validation here reads
            // the magic number and the declared length, and only a payload that passes reaches the
            // decoder.
            var validation = _imageProcessingService.ValidateImage(request.ImageBase64, options);
            if (!validation.IsValid)
            {
                throw new FamilyChatMessageInvalidException($"Image validation failed: {validation.ErrorMessage}");
            }

            var processed = await _imageProcessingService.ProcessImageAsync(request.ImageBase64, options, cancellationToken)
                ?? throw new FamilyChatMessageInvalidException("Failed to process image");

            var version = ImageFunctions.ContentVersion(processed.Data);
            var thumbnail = _imageProcessingService.CreateBoundedThumbnail(processed.Data, ImageSizes.ChatThumbnail);

            var message = new FamilyChatMessage
            {
                FamilyId = familyId,
                SenderUserId = userId,
                Kind = FamilyChatMessageKind.Image,
                Body = string.IsNullOrEmpty(caption) ? null : caption,
                SentAt = DateTime.UtcNow
            };

            using (var context = _contextFactory.CreateDbContext())
            {
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    context.Set<FamilyChatMessage>().Add(message);
                    // Saved first, for its id: the image row's foreign key needs it, and one
                    // transaction still makes the pair atomic.
                    await context.SaveChangesAsync(cancellationToken);

                    var image = new FamilyChatImage
                    {
                        FamilyChatMessageId = message.Id,
                        Data = processed.Data,
                        Version = version
                    };

                    ImageFunctions.Apply(image, processed, thumbnail, version);
                    context.Set<FamilyChatImage>().Add(image);

                    await context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    Log.Error(ex, "Failed to store a chat image for family {FamilyId}", familyId);
                    throw;
                }
            }

            var rendition = new FamilyChatImageRendition(message.Id, version, processed.Width, processed.Height);
            var info = ToInfo(
                message,
                LoadSenders([userId]),
                new Dictionary<int, FamilyChatImageRendition> { [message.Id] = rendition },
                NoReferences);

            await ClearTypingAsync(familyPublicId, userId, cancellationToken);
            await _runtime.FamilyChat.MessageCreatedAsync(familyPublicId, info, request.CorrelationId, cancellationToken);

            Log.Debug("User {UserId} sent chat image {PublicId} to family {FamilyId}", userId, message.PublicId, familyId);
            return info;
        }

        /// <summary>
        /// The bytes behind an image message, ready to be written to the response (#147).
        /// </summary>
        /// <remarks>
        /// Addressed by the <b>message's</b> public id, because whether you may see this picture is
        /// entirely the question of whether you may see that message - and the family scope on the
        /// query is what answers it. A message in another family reads as missing, not as
        /// forbidden: the two are indistinguishable to a caller here for the same reason they are
        /// on <see cref="DeleteMessageAsync"/>.
        /// <para>
        /// The rendition itself - thumbnail or full, WebP or a JPEG transcode, and the ETag that
        /// tells them apart - is <see cref="ImageFunctions.Render(IImageProcessingService, Entities.Common.StoredImageEntity, ImageVariant, bool)"/>'s, shared rather than written
        /// again here.
        /// </para>
        /// </remarks>
        public async Task<StoredImageResponse?> GetMessageImageAsync(
            Guid messagePublicId,
            ImageVariant variant,
            bool acceptsWebp,
            CancellationToken cancellationToken = default)
        {
            var (familyId, _) = RequireFamily();

            using var context = _contextFactory.CreateForReading();

            var image = await context.Set<FamilyChatImage>()
                .Where(i => i.Message.PublicId == messagePublicId && i.Message.FamilyId == familyId)
                .FirstOrDefaultAsync(cancellationToken);

            return image == null
                ? null
                : ImageFunctions.Render(_imageProcessingService, image, variant, acceptsWebp);
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
        /// Clears the sender's typing flags and tells the family, after a message goes out (#148).
        /// </summary>
        /// <remarks>
        /// Having said the thing, you are no longer typing it - on every device you have the chat
        /// open on, which is why this clears by user rather than by connection. It runs from the
        /// write path rather than relying on the client's own "stopped" call: the client sends one,
        /// but the message is what actually ends the typing, and the two arriving out of order (or
        /// one not arriving at all) would otherwise leave the indicator up for a full TTL after the
        /// message it was announcing is already on screen.
        /// </remarks>
        private async Task ClearTypingAsync(Guid familyPublicId, int userId, CancellationToken cancellationToken)
        {
            _connectionState.ClearTypingForUser(userId);
            var typing = _connectionState.TypingIn(familyPublicId, DateTime.UtcNow);
            await _runtime.FamilyChat.TypingChangedAsync(familyPublicId, typing, cancellationToken: cancellationToken);
        }

        #region References

        /// <summary>
        /// Turns what the sender asked to attach into what will be stored, or refuses it.
        /// </summary>
        /// <remarks>
        /// Every reference is resolved against the <see cref="SelectValueFunctions"/> list for its
        /// kind, which is the same list the pickers everywhere else in the app read from - so a
        /// reference is valid exactly when the sender could have picked it, and the label is the
        /// server's own rather than the client's. A client that sends an id it was never offered
        /// gets a 400, not a chip naming something it cannot see.
        /// <para>
        /// Duplicates are dropped rather than rejected: attaching the same product twice is a
        /// fumbled tap, not an error worth failing a message over.
        /// </para>
        /// </remarks>
        private List<FamilyChatReferenceInfo> ResolveReferences(IReadOnlyCollection<FamilyChatReferenceRequest> requested)
        {
            if (requested.Count == 0) return [];

            var selectValues = new SelectValueFunctions(_runtime);
            var byKind = new Dictionary<FamilyChatReferenceKind, Dictionary<Guid, string>>();
            var resolved = new List<FamilyChatReferenceInfo>(requested.Count);
            var seen = new HashSet<(FamilyChatReferenceKind, Guid)>();

            foreach (var reference in requested)
            {
                if (!seen.Add((reference.Kind, reference.PublicId))) continue;

                if (!byKind.TryGetValue(reference.Kind, out var lookup))
                {
                    lookup = LabelsFor(
                        selectValues,
                        reference.Kind,
                        requested.Where(r => r.Kind == reference.Kind).Select(r => r.PublicId));
                    byKind[reference.Kind] = lookup;
                }

                if (!lookup.TryGetValue(reference.PublicId, out var label))
                {
                    throw new FamilyChatMessageInvalidException(
                        $"No {reference.Kind} of yours with id {reference.PublicId}");
                }

                resolved.Add(new FamilyChatReferenceInfo
                {
                    Kind = reference.Kind,
                    PublicId = reference.PublicId,
                    Label = Truncate(label, MaxReferenceLabelLength),
                    IsAvailable = true
                });
            }

            return resolved;
        }

        /// <summary>
        /// The references on a page of messages, keyed by message id, with every label resolved
        /// against what <b>this reader</b> can see.
        /// </summary>
        /// <remarks>
        /// The resolution is the point of storing an id at all: a product renamed last week reads
        /// by its new name in a message from last month. A row that no longer resolves - deleted,
        /// or belonging to a family member's own unshared thing - keeps its stored snapshot and is
        /// marked unavailable, so the sentence still reads and the client simply does not make it a
        /// link.
        /// <para>
        /// One select-value read per kind present on the page, not per reference: those lists come
        /// out of the Functions layer's caches, and a page of chat is at most a handful of kinds.
        /// </para>
        /// </remarks>
        private Dictionary<int, List<FamilyChatReferenceInfo>> HydrateReferences(
            IReadOnlyList<FamilyChatMessageReference> rows)
        {
            if (rows.Count == 0) return [];

            var selectValues = new SelectValueFunctions(_runtime);
            var byKind = new Dictionary<FamilyChatReferenceKind, Dictionary<Guid, string>>();

            foreach (var kind in rows.Select(r => r.Kind).Distinct())
            {
                byKind[kind] = LabelsFor(
                    selectValues,
                    kind,
                    rows.Where(r => r.Kind == kind).Select(r => r.TargetPublicId));
            }

            var result = new Dictionary<int, List<FamilyChatReferenceInfo>>();

            foreach (var row in rows)
            {
                var available = byKind[row.Kind].TryGetValue(row.TargetPublicId, out var current);

                if (!result.TryGetValue(row.FamilyChatMessageId, out var list))
                {
                    list = [];
                    result[row.FamilyChatMessageId] = list;
                }

                list.Add(new FamilyChatReferenceInfo
                {
                    Kind = row.Kind,
                    PublicId = row.TargetPublicId,
                    Label = available ? current! : row.Label,
                    IsAvailable = available
                });
            }

            return result;
        }

        /// <summary>
        /// The current label of each of <paramref name="ids"/>, for the ones that still resolve
        /// for this caller. Ids that do not resolve are simply absent.
        /// </summary>
        /// <remarks>
        /// Products are looked up one id at a time; every other kind is answered from its
        /// select-value list.
        /// <para>
        /// The asymmetry is the catalogue's size. The other three lists are a household's own
        /// shops, places and lists - tens of rows, and the list is the same object the picker
        /// reads, so building it is nearly free. <see cref="SelectValueType.ProductCatalog"/> is
        /// every product on the instance: materialising it, formatting a label per row and sorting
        /// the result to answer "what is this one GUID called" is work proportional to the whole
        /// catalogue on every page of chat history that carries a single product chip.
        /// </para>
        /// <para>
        /// The labels are identical either way - both spell a product
        /// <c>"{Brand} - {Name}"</c> - so a chip resolved here reads exactly as one resolved
        /// through the list, and "valid reference" still means "a product that exists".
        /// </para>
        /// </remarks>
        private Dictionary<Guid, string> LabelsFor(
            SelectValueFunctions selectValues,
            FamilyChatReferenceKind kind,
            IEnumerable<Guid> ids)
        {
            if (kind != FamilyChatReferenceKind.Product)
            {
                return selectValues
                    .GetSelectValues(SelectValueTypeOf(kind))
                    .GroupBy(v => v.PublicId)
                    .ToDictionary(g => g.Key, g => g.First().Text);
            }

            var products = new ProductFunctions(_runtime);
            var labels = new Dictionary<Guid, string>();

            foreach (var id in ids.Distinct())
            {
                var product = products.GetProductByPublicId(id);
                if (product == null || product.IsDeleted) continue;

                labels[id] = $"{product.Brand} - {product.Name}";
            }

            return labels;
        }

        /// <summary>
        /// Which select-value list answers for a reference kind.
        /// </summary>
        /// <remarks>
        /// The mapping is what ties a chip to the picker that produced it: both sides read the same
        /// list, so "valid reference" and "thing the sender could pick" are the same statement.
        ///
        /// A product resolves against the whole catalogue, not against
        /// <see cref="SelectValueType.Product"/> (what this family has stock of): the commonest
        /// thing to say about a product in a family chat is "buy this", and that is exactly the
        /// product nobody has at home.
        /// </remarks>
        private static SelectValueType SelectValueTypeOf(FamilyChatReferenceKind kind) => kind switch
        {
            FamilyChatReferenceKind.Product => SelectValueType.ProductCatalog,
            FamilyChatReferenceKind.ShoppingLocation => SelectValueType.ShoppingLocation,
            FamilyChatReferenceKind.StorageLocation => SelectValueType.StorageLocation,
            FamilyChatReferenceKind.ShoppingList => SelectValueType.ShoppingList,
            _ => throw new FamilyChatMessageInvalidException($"Unsupported reference kind {kind}")
        };

        /// <summary>Longest label a stored snapshot keeps, matching the column.</summary>
        private const int MaxReferenceLabelLength = 255;

        private static string Truncate(string value, int maxLength)
            => value.Length <= maxLength ? value : value[..maxLength];

        /// <summary>The empty reference map, for the paths that project a message known to have none.</summary>
        private static readonly Dictionary<int, List<FamilyChatReferenceInfo>> NoReferences = [];

        #endregion

        /// <summary>
        /// Whether a failed save was PostgreSQL refusing a duplicate (<c>23505</c>) rather than
        /// anything else - narrow on purpose, so a genuine failure still surfaces instead of being
        /// quietly read as "somebody else wrote it first".
        /// </summary>
        private static bool IsUniqueViolation(DbUpdateException exception) =>
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

        /// <summary>The image path's own allowance, separate from the text one (#147).</summary>
        private static void RequireImageAllowance(int userId)
        {
            if (RateLimitService.IsRateLimited($"family-chat:image:{userId}", ImageMaxPerWindow, RateWindow))
            {
                throw new FamilyChatRateLimitedException("You are sending pictures too quickly");
            }
        }

        /// <summary>
        /// The stored-image version and shape of every image message on a page, keyed by message id,
        /// so the rows can carry cacheable picture URLs (#147).
        /// </summary>
        /// <remarks>
        /// Only the version and the dimensions are read, never the bytes. That is the whole point
        /// of the separate table: listing a conversation must not load its pictures. The dimensions
        /// ride along so the client can reserve the image's box before the bytes arrive and the
        /// stream does not reflow as they load.
        /// </remarks>
        private static async Task<Dictionary<int, FamilyChatImageRendition>> LoadImageRenditionsAsync(
            HomassyDbContext context,
            IReadOnlyCollection<FamilyChatMessage> messages,
            CancellationToken cancellationToken)
        {
            var imageMessageIds = messages
                .Where(m => m.Kind == FamilyChatMessageKind.Image)
                .Select(m => m.Id)
                .ToList();

            if (imageMessageIds.Count == 0) return [];

            return await context.Set<FamilyChatImage>()
                .Where(i => imageMessageIds.Contains(i.FamilyChatMessageId))
                .Select(i => new FamilyChatImageRendition(i.FamilyChatMessageId, i.Version, i.Width, i.Height))
                .ToDictionaryAsync(i => i.MessageId, cancellationToken);
        }

        /// <summary>What a stored image row contributes to a message payload: a version and a shape.</summary>
        private sealed record FamilyChatImageRendition(int MessageId, string Version, int Width, int Height);

        /// <summary>The empty rendition map, for the paths that project a message known to be text.</summary>
        private static readonly Dictionary<int, FamilyChatImageRendition> NoImages = [];

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
            IReadOnlyDictionary<int, FamilyChatSenderInfo> senders,
            IReadOnlyDictionary<int, FamilyChatImageRendition> images,
            IReadOnlyDictionary<int, List<FamilyChatReferenceInfo>> references)
        {
            senders.TryGetValue(message.SenderUserId, out var sender);
            images.TryGetValue(message.Id, out var image);
            references.TryGetValue(message.Id, out var attached);

            return new FamilyChatMessageInfo
            {
                PublicId = message.PublicId,
                Kind = message.Kind,
                Body = message.Body,
                SentAt = message.SentAt,
                EditedAt = message.EditedAt,
                // A sender who cannot be resolved (a member deleted outright) still leaves a
                // readable message rather than taking the whole page down with it.
                Sender = sender ?? new FamilyChatSenderInfo(),
                ImageUrl = image == null ? null : MediaUrls.FamilyChatImage(message.PublicId, image.Version),
                ImageFullUrl = image == null ? null : MediaUrls.FamilyChatImage(message.PublicId, image.Version, ImageVariant.Full),
                ImageWidth = image?.Width,
                ImageHeight = image?.Height,
                References = attached ?? []
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
