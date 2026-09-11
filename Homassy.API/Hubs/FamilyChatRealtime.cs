using Homassy.API.Models.FamilyChat;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Broadcast helper for pushing family chat changes to connected clients over SignalR (#144).
    /// </summary>
    /// <remarks>
    /// Same shape and same stance as <see cref="ShoppingListRealtime"/>: writes flow through the
    /// REST endpoints and <see cref="Functions.FamilyChatFunctions"/>, and after a successful
    /// commit the Functions layer calls in here to notify everyone in the family's group. Takes
    /// its hub context through the constructor, is registered as a singleton, and is reached from
    /// the Functions layer through <see cref="Functions.FunctionsRuntime"/>.
    /// <para>
    /// A broadcast failure is logged and swallowed. A message that is committed but not delivered
    /// is a message the next page load shows; a write that throws because a socket was unhappy is
    /// a message the sender believes they never sent.
    /// </para>
    /// </remarks>
    public sealed class FamilyChatRealtime
    {
        public const string MessageCreatedEvent = "MessageCreated";
        public const string MessageDeletedEvent = "MessageDeleted";
        public const string TypingChangedEvent = "TypingChanged";
        public const string ActiveChangedEvent = "ActiveChanged";

        /// <summary>
        /// SignalR group name for one family's conversation. Shared with <see cref="FamilyChatHub"/>.
        /// </summary>
        public static string GroupName(Guid familyPublicId) => $"family-chat:{familyPublicId}";

        private readonly IHubContext<FamilyChatHub>? _hubContext;

        /// <remarks>
        /// Optional, so a host that maps no hubs (the notifications service borrows parts of this
        /// layer) resolves the helper and skips the broadcast instead of failing to start.
        /// </remarks>
        public FamilyChatRealtime(IHubContext<FamilyChatHub>? hubContext = null)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Pushes a newly committed message to the family's group.
        /// </summary>
        /// <param name="familyPublicId">Which family's group to push to.</param>
        /// <param name="message">The committed message, already projected for the stream.</param>
        /// <param name="cancellationToken">Cancellation of the originating write.</param>
        /// <param name="correlationId">
        /// The sender's own id for this message, echoed straight back. The sender appended it
        /// optimistically before the request and receives this broadcast like everyone else, so
        /// without the echo they would render their own message twice. Null for a message with no
        /// client behind it.
        /// </param>
        public Task MessageCreatedAsync(Guid familyPublicId, FamilyChatMessageInfo message, string? correlationId = null, CancellationToken cancellationToken = default)
            => SendAsync(familyPublicId, MessageCreatedEvent, new { message, correlationId }, cancellationToken);

        /// <summary>Notifies the family's group that a message was deleted, and by whom.</summary>
        public Task MessageDeletedAsync(Guid familyPublicId, Guid messagePublicId, Guid? actorPublicId = null, CancellationToken cancellationToken = default)
            => SendAsync(familyPublicId, MessageDeletedEvent, new { publicId = messagePublicId, actorPublicId }, cancellationToken);

        /// <summary>
        /// Pushes the family's current set of typists (#148).
        /// </summary>
        /// <param name="familyPublicId">Which family's group to push to.</param>
        /// <param name="members">Everyone currently typing, collapsed to one entry per person.</param>
        /// <param name="exceptConnectionId">
        /// The connection that caused this change, if any. A typist must never be told that they
        /// are typing, so their own connection is excluded from the broadcast rather than left to
        /// filter itself out.
        /// </param>
        /// <param name="cancellationToken">Cancellation of the originating invocation.</param>
        public Task TypingChangedAsync(
            Guid familyPublicId,
            IReadOnlyList<FamilyChatTypingMember> members,
            string? exceptConnectionId = null,
            CancellationToken cancellationToken = default)
            => SendAsync(familyPublicId, TypingChangedEvent, members, cancellationToken, exceptConnectionId);

        /// <summary>
        /// Pushes the family's current set of members watching the conversation.
        /// </summary>
        /// <remarks>
        /// Sent to the whole group, the caller included - unlike typing, where telling somebody
        /// they are typing is noise. Here the sender is part of the answer: a client that has just
        /// opened the chat wants the roster it has now joined, and every client filters itself out
        /// for display rather than being sent a different list each.
        /// </remarks>
        /// <param name="familyPublicId">Which family's group to push to.</param>
        /// <param name="members">Everyone currently watching, collapsed to one entry per person.</param>
        /// <param name="cancellationToken">Cancellation of the originating invocation.</param>
        public Task ActiveChangedAsync(
            Guid familyPublicId,
            IReadOnlyList<FamilyChatActiveMember> members,
            CancellationToken cancellationToken = default)
            => SendAsync(familyPublicId, ActiveChangedEvent, members, cancellationToken);

        private async Task SendAsync(
            Guid familyPublicId,
            string eventName,
            object payload,
            CancellationToken cancellationToken,
            string? exceptConnectionId = null)
        {
            var hub = _hubContext;
            if (hub == null)
            {
                Log.Warning("FamilyChatHub context unavailable; skipping {Event} broadcast for family {FamilyPublicId}", eventName, familyPublicId);
                return;
            }

            try
            {
                var clients = exceptConnectionId == null
                    ? hub.Clients.Group(GroupName(familyPublicId))
                    : hub.Clients.GroupExcept(GroupName(familyPublicId), exceptConnectionId);

                await clients.SendAsync(eventName, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                // A broadcast failure must never break the write that triggered it.
                Log.Error(ex, "Failed to broadcast {Event} for family chat {FamilyPublicId}", eventName, familyPublicId);
            }
        }
    }
}
