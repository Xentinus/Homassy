using Homassy.API.Models.Family;

namespace Homassy.API.Hubs
{
    // PROCESS-LOCAL BY DESIGN, exactly like ShoppingListPresence and FamilyChatConnectionState
    // next to it. Everything here lives in this process's memory, so with more than one instance
    // each one only knows about the connections it personally holds and "who is online" would be
    // incomplete. Making it correct across replicas needs the SignalR backplane that #68 adds.
    // The app runs single-instance today, so this is a complete picture as it stands — a scope
    // statement, not a TODO.
    //
    // How it fails when a replica's view is missing is worth knowing before anyone splits this:
    // a member who is online shows as offline, which reads as "nobody else is around". That is
    // the same direction the shopping-list strip already fails in, and it never invents presence
    // that is not there.
    /// <summary>
    /// Who in a household has the app open right now, and which of them are in in-store shopping
    /// mode. One entry per live connection, collapsed per member when a snapshot is built.
    /// </summary>
    /// <remarks>
    /// <b>One lock, not a bucket per household.</b> Unlike <see cref="ShoppingListPresence"/> —
    /// where a connection can be present on many lists at once and buckets come and go — a
    /// connection here belongs to exactly one scope for its whole life, and the map holds one
    /// entry per live connection. That is small enough that a single lock is cheaper than the
    /// reasoning finer-grained locking would cost.
    /// </remarks>
    public sealed class FamilyPresence
    {
        private sealed class ConnectionEntry
        {
            /// <summary>The household (or lone user) group this connection belongs to.</summary>
            public required string ScopeKey { get; init; }

            public required FamilyPresenceMemberInfo Member { get; init; }

            /// <summary>Non-null while this connection is in shopping mode: the list's name.</summary>
            public string? ShoppingContext { get; set; }
        }

        private readonly object _gate = new();

        private readonly Dictionary<string, ConnectionEntry> _connections = [];

        /// <summary>
        /// Every connection id <see cref="Disconnect"/> has been called for. SignalR does not wait
        /// for in-flight hub invocations before calling <c>OnDisconnectedAsync</c>, so a tab closed
        /// mid-connect can have <see cref="Disconnect"/> run before the <see cref="Join"/> it
        /// raced, which would otherwise register a connection nothing will ever retire. Both
        /// methods take <see cref="_gate"/>, so their order is total: a Join that arrives after
        /// the Disconnect sees the id here and declines. Entries are never removed — a fresh
        /// connection always gets a fresh id — so this grows by a few bytes per connection for the
        /// life of the process, the same process-local scope this class already accepts.
        /// </summary>
        private readonly HashSet<string> _deadConnections = [];

        /// <summary>
        /// Registers a connection as present in <paramref name="scopeKey"/> and returns the
        /// post-join snapshot, or null when the connection has already disconnected (see
        /// <see cref="_deadConnections"/>) and nothing was registered.
        /// </summary>
        public IReadOnlyList<FamilyPresenceMemberInfo>? Join(string scopeKey, string connectionId, FamilyPresenceMemberInfo member)
        {
            lock (_gate)
            {
                if (_deadConnections.Contains(connectionId))
                {
                    return null;
                }

                _connections[connectionId] = new ConnectionEntry
                {
                    ScopeKey = scopeKey,
                    Member = member
                };

                return BuildSnapshot(scopeKey);
            }
        }

        /// <summary>
        /// Marks a connection as shopping (<paramref name="shoppingContext"/> = the list's name) or
        /// no longer shopping (null), and returns the affected scope with its post-change snapshot.
        /// Null for a connection that is not registered — a no-op, never a throw.
        /// </summary>
        public (string ScopeKey, IReadOnlyList<FamilyPresenceMemberInfo> Members)? SetShopping(string connectionId, string? shoppingContext)
        {
            lock (_gate)
            {
                if (!_connections.TryGetValue(connectionId, out var entry))
                {
                    return null;
                }

                entry.ShoppingContext = shoppingContext;
                return (entry.ScopeKey, BuildSnapshot(entry.ScopeKey));
            }
        }

        /// <summary>
        /// Drops a connection and returns its scope with the post-removal snapshot, or null when
        /// the connection was never registered.
        /// </summary>
        public (string ScopeKey, IReadOnlyList<FamilyPresenceMemberInfo> Members)? Disconnect(string connectionId)
        {
            lock (_gate)
            {
                // Recorded unconditionally, before the lookup: see _deadConnections' own comment
                // for why a Join racing this exact id must be able to see it even when there is
                // nothing here to clean up yet.
                _deadConnections.Add(connectionId);

                if (!_connections.Remove(connectionId, out var entry))
                {
                    return null;
                }

                return (entry.ScopeKey, BuildSnapshot(entry.ScopeKey));
            }
        }

        /// <summary>The scope's current snapshot, or an empty (never null) list when nobody is connected.</summary>
        public IReadOnlyList<FamilyPresenceMemberInfo> Snapshot(string scopeKey)
        {
            lock (_gate)
            {
                return BuildSnapshot(scopeKey);
            }
        }

        /// <summary>
        /// Collapses a scope's connections by member: one entry per distinct
        /// <see cref="FamilyPresenceMemberInfo.PublicId"/>, carrying that member's display data
        /// with <see cref="FamilyPresenceMemberInfo.DeviceCount"/> set to how many of their
        /// connections are live, and shopping true when <em>any</em> of them is shopping — a phone
        /// in the shop and a laptop at home is still "shopping" to everyone else. Must be called
        /// with <see cref="_gate"/> held.
        /// </summary>
        private IReadOnlyList<FamilyPresenceMemberInfo> BuildSnapshot(string scopeKey)
        {
            return _connections.Values
                .Where(e => e.ScopeKey == scopeKey)
                .GroupBy(e => e.Member.PublicId)
                .Select(g =>
                {
                    var shopping = g.FirstOrDefault(e => e.ShoppingContext != null);

                    return g.First().Member with
                    {
                        DeviceCount = g.Count(),
                        IsShopping = shopping != null,
                        ShoppingContext = shopping?.ShoppingContext
                    };
                })
                .ToList();
        }
    }
}
