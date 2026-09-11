using System.Collections.Concurrent;
using Homassy.API.Models.FamilyChat;

namespace Homassy.API.Hubs
{
    // PROCESS-LOCAL BY DESIGN, exactly like ShoppingListPresence next to it. Everything here lives
    // in this process's memory, so with more than one instance each one only knows about the
    // connections it personally holds. Making this correct across replicas needs the SignalR
    // backplane that #68 adds - the same caveat #97 records. The app runs single-instance today,
    // so this is a complete picture as it stands; that is a scope statement, not a TODO.
    //
    // The two flags fail differently if a replica's view is missing, which is worth knowing before
    // anyone splits this:
    //   - a typing flag nobody sees means a missing "…is typing", which is invisible;
    //   - an active flag nobody sees means an extra notification, never a lost one (#149).
    /// <summary>
    /// What the chat hub remembers about each live connection: whether it is typing (#148) and
    /// whether it is actively watching the conversation (#149), each with a refreshed expiry.
    /// </summary>
    /// <remarks>
    /// <b>One bag for both flags, on purpose.</b> They need identical bookkeeping - per connection,
    /// per family, collapsed per user, expiring on their own - and two registries would mean two
    /// places to remember to clean up on disconnect, which is how one of them ends up stale.
    /// <para>
    /// <b>Every flag has a TTL and nothing here is trusted to be told it is over.</b> A closed
    /// laptop lid, a dropped connection or a tab killed mid-keystroke all end the same way: no
    /// "stopped" call ever arrives. An expiry means the worst case is a few seconds of staleness
    /// rather than somebody typing forever.
    /// </para>
    /// <para>
    /// Registered as a singleton and hit from arbitrary concurrent hub invocations, so every
    /// mutation and every snapshot is taken under one lock. The maps are small - one entry per live
    /// connection - so a single lock is cheaper than the reasoning a finer-grained one would cost.
    /// </para>
    /// </remarks>
    public sealed class FamilyChatConnectionState
    {
        /// <summary>
        /// How long a typing flag survives without being refreshed.
        /// </summary>
        /// <remarks>
        /// A little over the client's ~2s throttle, so a steady typist never flickers, and short
        /// enough that a dropped connection stops "typing" before anybody notices it is stuck.
        /// </remarks>
        public static readonly TimeSpan TypingTtl = TimeSpan.FromSeconds(5);

        /// <summary>
        /// How long an "actively watching" flag survives without a heartbeat (#149).
        /// </summary>
        /// <remarks>
        /// Longer than the typing TTL because the client refreshes it on a slow heartbeat rather
        /// than on keystrokes, and because the cost of being wrong is asymmetric: an expired flag
        /// means one extra notification, while a flag that outlives the reader means a message
        /// they never hear about.
        /// </remarks>
        public static readonly TimeSpan ActiveTtl = TimeSpan.FromSeconds(45);

        private sealed class ConnectionEntry
        {
            public required Guid FamilyPublicId { get; init; }
            public required Guid UserPublicId { get; init; }
            public required int UserId { get; init; }
            public required string DisplayName { get; set; }

            /// <summary>When the typing flag expires; null when this connection is not typing.</summary>
            public DateTime? TypingUntil { get; set; }

            /// <summary>When the "actively watching" flag expires; null when it is not set (#149).</summary>
            public DateTime? ActiveUntil { get; set; }
        }

        private readonly object _gate = new();
        private readonly Dictionary<string, ConnectionEntry> _connections = [];

        /// <summary>
        /// Registers a connection against a family, or refreshes what is known about it.
        /// </summary>
        /// <remarks>
        /// Called when a connection joins the chat. Re-joining (after a reconnect) overwrites
        /// rather than duplicates: a connection id belongs to exactly one connection, and a
        /// rebuilt connection gets a new one.
        /// </remarks>
        public void Register(string connectionId, Guid familyPublicId, Guid userPublicId, int userId, string displayName)
        {
            lock (_gate)
            {
                if (_connections.TryGetValue(connectionId, out var existing))
                {
                    existing.DisplayName = displayName;
                    return;
                }

                _connections[connectionId] = new ConnectionEntry
                {
                    FamilyPublicId = familyPublicId,
                    UserPublicId = userPublicId,
                    UserId = userId,
                    DisplayName = displayName
                };
            }
        }

        /// <summary>
        /// Forgets a connection entirely - it left the chat, or the socket dropped.
        /// </summary>
        /// <returns>The family it was in, or null if it was never registered.</returns>
        public Guid? Remove(string connectionId)
        {
            lock (_gate)
            {
                if (!_connections.Remove(connectionId, out var entry)) return null;
                return entry.FamilyPublicId;
            }
        }

        /// <summary>Sets or clears this connection's typing flag, refreshing its expiry (#148).</summary>
        /// <returns>The family to broadcast to, or null for a connection that is not registered.</returns>
        public Guid? SetTyping(string connectionId, bool isTyping, DateTime now)
        {
            lock (_gate)
            {
                if (!_connections.TryGetValue(connectionId, out var entry)) return null;

                entry.TypingUntil = isTyping ? now.Add(TypingTtl) : null;
                return entry.FamilyPublicId;
            }
        }

        /// <summary>Sets or clears this connection's "actively watching" flag (#149).</summary>
        /// <returns>The family the connection belongs to, or null when it is not registered.</returns>
        public Guid? SetActive(string connectionId, bool isActive, DateTime now)
        {
            lock (_gate)
            {
                if (!_connections.TryGetValue(connectionId, out var entry)) return null;

                entry.ActiveUntil = isActive ? now.Add(ActiveTtl) : null;
                return entry.FamilyPublicId;
            }
        }

        /// <summary>
        /// Clears every one of this user's typing flags, across all their connections (#148).
        /// </summary>
        /// <remarks>
        /// What a sent message calls: having said the thing, you are no longer typing it - on the
        /// phone you sent it from and on the laptop you left open.
        /// </remarks>
        public void ClearTypingForUser(int userId)
        {
            lock (_gate)
            {
                foreach (var entry in _connections.Values)
                {
                    if (entry.UserId == userId) entry.TypingUntil = null;
                }
            }
        }

        /// <summary>
        /// Who is currently typing in a family, collapsed to one entry per person.
        /// </summary>
        /// <remarks>
        /// Collapsed by user, not by connection: somebody with the chat open on a phone and a
        /// laptop is one person typing, and listing them twice is a bug the reader can see.
        /// Expired flags are simply not included - the sweep that broadcasts their disappearance
        /// is separate, because this method is also called from paths that are already
        /// broadcasting.
        /// </remarks>
        public IReadOnlyList<FamilyChatTypingMember> TypingIn(Guid familyPublicId, DateTime now)
        {
            lock (_gate)
            {
                return _connections.Values
                    .Where(e => e.FamilyPublicId == familyPublicId && e.TypingUntil > now)
                    .GroupBy(e => e.UserPublicId)
                    .Select(g => new FamilyChatTypingMember
                    {
                        PublicId = g.Key,
                        DisplayName = g.First().DisplayName
                    })
                    .ToList();
            }
        }

        /// <summary>
        /// Whether this user has at least one connection currently flagged as actively watching
        /// the conversation (#149).
        /// </summary>
        /// <remarks>
        /// Any one device is enough: a laptop with the panel open must suppress the push to the
        /// phone in your pocket, because you have already seen the message.
        /// </remarks>
        /// <summary>
        /// Who is currently watching a family's conversation, collapsed to one entry per person.
        /// </summary>
        /// <remarks>
        /// The roster behind the count on the chat bubble. Collapsed by user for the same reason
        /// <see cref="TypingIn"/> is: somebody reading on a phone and a laptop is one person in the
        /// conversation, and counting them twice would tell the rest of the family there is a
        /// reader who is not there.
        /// <para>
        /// This is the same flag the notification decision reads, so what the bubble shows and what
        /// suppresses a push cannot disagree: if it says two people are here, those are exactly the
        /// two people a message will not notify.
        /// </para>
        /// </remarks>
        public IReadOnlyList<FamilyChatActiveMember> ActiveIn(Guid familyPublicId, DateTime now)
        {
            lock (_gate)
            {
                return _connections.Values
                    .Where(e => e.FamilyPublicId == familyPublicId && e.ActiveUntil > now)
                    .GroupBy(e => e.UserPublicId)
                    .Select(g => new FamilyChatActiveMember
                    {
                        PublicId = g.Key,
                        DisplayName = g.First().DisplayName
                    })
                    .ToList();
            }
        }

        public bool IsUserActive(int userId, DateTime now)
        {
            lock (_gate)
            {
                return _connections.Values.Any(e => e.UserId == userId && e.ActiveUntil > now);
            }
        }

        /// <summary>
        /// The families whose typing set has just shrunk because a flag expired.
        /// </summary>
        /// <remarks>
        /// The self-healing half of the feature: nothing else notices an expiry, so without this
        /// sweep a dropped connection would leave "Anna is typing…" on everyone's screen until the
        /// next unrelated event. Clearing the expired flags here - rather than only reading past
        /// them - is what makes the result "families that need a fresh broadcast" rather than
        /// "families that have ever had a typist".
        /// </remarks>
        public IReadOnlyList<Guid> SweepExpiredTyping(DateTime now)
        {
            lock (_gate)
            {
                var affected = new HashSet<Guid>();

                foreach (var entry in _connections.Values)
                {
                    if (entry.TypingUntil is { } until && until <= now)
                    {
                        entry.TypingUntil = null;
                        affected.Add(entry.FamilyPublicId);
                    }
                }

                return [.. affected];
            }
        }

        /// <summary>
        /// The families whose watching set has just shrunk because an active flag expired.
        /// </summary>
        /// <remarks>
        /// The same self-healing argument as <see cref="SweepExpiredTyping"/>, and it matters more
        /// here than there: a stale typing flag is a line of text that reads oddly for five
        /// seconds, while a stale active flag is a reader the rest of the family is told is present
        /// - and, until #149's TTL expires it, one whose messages are also not being notified. The
        /// two are the same fact, so they expire in the same sweep.
        /// </remarks>
        public IReadOnlyList<Guid> SweepExpiredActive(DateTime now)
        {
            lock (_gate)
            {
                var affected = new HashSet<Guid>();

                foreach (var entry in _connections.Values)
                {
                    if (entry.ActiveUntil is { } until && until <= now)
                    {
                        entry.ActiveUntil = null;
                        affected.Add(entry.FamilyPublicId);
                    }
                }

                return [.. affected];
            }
        }

        /// <summary>Connections currently registered. Diagnostics only.</summary>
        public int ConnectionCount
        {
            get
            {
                lock (_gate) return _connections.Count;
            }
        }
    }
}
