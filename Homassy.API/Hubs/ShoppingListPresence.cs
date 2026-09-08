using System.Collections.Concurrent;
using Homassy.API.Models.ShoppingList;

namespace Homassy.API.Hubs
{
    // PROCESS-LOCAL BY DESIGN. This registry lives entirely in this process's memory: if the app
    // ever runs as more than one instance, each instance only knows about the connections it
    // personally holds, so "who is looking at this list" would be incomplete — and could flicker
    // as clients land on different instances. Making presence correct across replicas needs the
    // SignalR backplane that issue #68 adds, which is out of scope here. The app runs
    // single-instance today, so this is a complete and correct picture of presence as it stands —
    // that is a scope statement, not an oversight to fix later in this file.
    /// <summary>
    /// Tracks, per shopping list, which connections currently have that list open, so
    /// <see cref="ShoppingListHub"/> can tell every viewer who else is looking at it right now.
    /// Registered as a singleton; hit from arbitrary concurrent hub invocations (join/leave/
    /// disconnect can all race across many connections and many lists at once), so every mutation
    /// is taken under a lock scoped to the single list (or single connection) it touches, and the
    /// snapshot handed back is always built inside that same lock — a caller can never observe a
    /// join or leave that is only half-applied.
    /// </summary>
    public sealed class ShoppingListPresence
    {
        /// <summary>
        /// One shopping list's present connections. Every read and mutation happens under
        /// <c>lock (this)</c>, including the moment the map goes empty and the bucket is retired
        /// from <see cref="_lists"/> — see <see cref="Retired"/>.
        /// </summary>
        private sealed class ListPresence
        {
            public readonly Dictionary<string, PresenceMemberInfo> Connections = new();

            /// <summary>
            /// Set — under this instance's own lock, in the same critical section that removes it
            /// from <see cref="_lists"/> — the moment its connection map empties. A concurrent Join
            /// can fetch this exact instance from <see cref="_lists"/> a moment before that removal
            /// and then block on the lock held while it happens; when that Join finally gets the
            /// lock, <see cref="Retired"/> tells it the instance it is holding is no longer
            /// reachable through <see cref="_lists"/>, so writing into it would silently vanish.
            /// It retries instead, which re-fetches (or creates) whatever bucket is current.
            /// </summary>
            public bool Retired;
        }

        /// <summary>A connection's own set of joined lists, guarded the same way a <see cref="ListPresence"/> is.</summary>
        private sealed class ConnectionJoins
        {
            public readonly HashSet<Guid> Lists = new();
            public bool Retired;
        }

        private readonly ConcurrentDictionary<Guid, ListPresence> _lists = new();

        /// <summary>
        /// Reverse index — connection id to every list it has joined — so <see cref="Disconnect"/>
        /// can drop a connection without scanning every list the process has ever served.
        /// </summary>
        private readonly ConcurrentDictionary<string, ConnectionJoins> _connectionLists = new();

        /// <summary>
        /// Every connection id <see cref="Disconnect"/> has ever been called for. SignalR does not
        /// wait for in-flight hub invocations before calling <c>OnDisconnectedAsync</c>, so a tab
        /// closed mid-<see cref="Join"/> (already past the hub's group-add, not yet into this
        /// class) can have <see cref="Disconnect"/> run first, find nothing yet in
        /// <see cref="_connectionLists"/> for it, and return — before that in-flight
        /// <see cref="Join"/> goes on to write into both maps for a connection that is already
        /// gone. Since no further <see cref="Disconnect"/> will ever arrive for that connection id
        /// (a fresh connection always gets a fresh one), <see cref="Join"/> checks here — inside
        /// the same lock it writes <see cref="_connectionLists"/> under, the first point since the
        /// two methods' separate locks where they are ever checked against each other — and undoes
        /// its own registration rather than stranding it forever. Entries are never removed: that
        /// is what lets a late <see cref="Join"/> be rejected even when it runs strictly after
        /// <see cref="Disconnect"/> already finished and tore down every bucket it could see. Grows
        /// for the life of the process (a few bytes per connection that ever disconnected) — the
        /// same process-local scope this whole class already accepts, and it resets on restart the
        /// same way every bucket above does.
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> _deadConnections = new();

        /// <summary>Registers <paramref name="connectionId"/> as present on the list and returns the post-join snapshot.</summary>
        public IReadOnlyList<PresenceMemberInfo> Join(Guid listPublicId, string connectionId, PresenceMemberInfo member)
        {
            IReadOnlyList<PresenceMemberInfo> snapshot;

            while (true)
            {
                var list = _lists.GetOrAdd(listPublicId, _ => new ListPresence());

                lock (list)
                {
                    if (list.Retired)
                    {
                        continue;
                    }

                    list.Connections[connectionId] = member;
                    snapshot = BuildSnapshot(list);
                    break;
                }
            }

            while (true)
            {
                var joins = _connectionLists.GetOrAdd(connectionId, _ => new ConnectionJoins());

                lock (joins)
                {
                    if (joins.Retired)
                    {
                        continue;
                    }

                    // See _deadConnections' own doc comment: a Disconnect for this exact
                    // connection id may already have run - and, finding nothing registered yet,
                    // already returned - between the write into _lists above and this one.
                    // Nothing will ever call Disconnect again for this id, so undo that write
                    // rather than leave a presence entry no one will ever retire.
                    if (_deadConnections.ContainsKey(connectionId))
                    {
                        // Retire and remove this bucket too, exactly as Disconnect would have -
                        // otherwise this GetOrAdd's own (empty, or another in-flight Join's still
                        // legitimate) bucket is what leaks instead. Lists is deliberately left
                        // untouched rather than cleared: if Disconnect is concurrently blocked on
                        // this very lock holding an older reference to this same object, it still
                        // needs whatever is in there to finish cleaning up the lists it saw.
                        joins.Retired = true;
                        _connectionLists.TryRemove(new KeyValuePair<string, ConnectionJoins>(connectionId, joins));
                        return RemoveConnectionFromList(listPublicId, connectionId);
                    }

                    joins.Lists.Add(listPublicId);
                    break;
                }
            }

            return snapshot;
        }

        /// <summary>Removes <paramref name="connectionId"/> from the list and returns the post-leave snapshot. A no-op (never throws) for a connection that never joined.</summary>
        public IReadOnlyList<PresenceMemberInfo> Leave(Guid listPublicId, string connectionId)
        {
            var snapshot = RemoveConnectionFromList(listPublicId, connectionId);
            RemoveListFromConnection(connectionId, listPublicId);
            return snapshot;
        }

        /// <summary>
        /// Removes a dropped connection from every list it had joined, using the reverse index
        /// rather than scanning <see cref="_lists"/>. Returns one (list, post-removal snapshot)
        /// pair per affected list, so the hub has exactly one broadcast to make per list.
        /// </summary>
        public IReadOnlyList<(Guid ListPublicId, IReadOnlyList<PresenceMemberInfo> Members)> Disconnect(string connectionId)
        {
            // Recorded first, unconditionally, before the reverse index is even looked up: see
            // _deadConnections' own doc comment for why a Join racing this exact connection id
            // must be able to see this even when the lookup just below finds nothing to clean up.
            _deadConnections[connectionId] = 0;

            if (!_connectionLists.TryGetValue(connectionId, out var joins))
            {
                return Array.Empty<(Guid, IReadOnlyList<PresenceMemberInfo>)>();
            }

            Guid[] listIds;
            lock (joins)
            {
                listIds = [.. joins.Lists];
                joins.Lists.Clear();
                joins.Retired = true;
                _connectionLists.TryRemove(new KeyValuePair<string, ConnectionJoins>(connectionId, joins));
            }

            var results = new List<(Guid, IReadOnlyList<PresenceMemberInfo>)>(listIds.Length);
            foreach (var listPublicId in listIds)
            {
                results.Add((listPublicId, RemoveConnectionFromList(listPublicId, connectionId)));
            }

            return results;
        }

        /// <summary>The list's current snapshot, or an empty (never null) list when nobody has joined it.</summary>
        public IReadOnlyList<PresenceMemberInfo> Snapshot(Guid listPublicId)
        {
            while (true)
            {
                if (!_lists.TryGetValue(listPublicId, out var list))
                {
                    return Array.Empty<PresenceMemberInfo>();
                }

                lock (list)
                {
                    if (list.Retired)
                    {
                        continue;
                    }

                    return BuildSnapshot(list);
                }
            }
        }

        private IReadOnlyList<PresenceMemberInfo> RemoveConnectionFromList(Guid listPublicId, string connectionId)
        {
            while (true)
            {
                if (!_lists.TryGetValue(listPublicId, out var list))
                {
                    return Array.Empty<PresenceMemberInfo>();
                }

                lock (list)
                {
                    if (list.Retired)
                    {
                        continue;
                    }

                    list.Connections.Remove(connectionId);
                    var snapshot = BuildSnapshot(list);

                    if (list.Connections.Count == 0)
                    {
                        // Retire and remove together, under this same lock: anyone who fetched
                        // this instance from _lists a moment ago and is waiting on the lock will
                        // see Retired the instant they get it, before they can write into a bucket
                        // that no longer exists as far as _lists is concerned.
                        list.Retired = true;
                        _lists.TryRemove(new KeyValuePair<Guid, ListPresence>(listPublicId, list));
                    }

                    return snapshot;
                }
            }
        }

        private void RemoveListFromConnection(string connectionId, Guid listPublicId)
        {
            while (true)
            {
                if (!_connectionLists.TryGetValue(connectionId, out var joins))
                {
                    return;
                }

                lock (joins)
                {
                    if (joins.Retired)
                    {
                        continue;
                    }

                    joins.Lists.Remove(listPublicId);

                    if (joins.Lists.Count == 0)
                    {
                        joins.Retired = true;
                        _connectionLists.TryRemove(new KeyValuePair<string, ConnectionJoins>(connectionId, joins));
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// Collapses a list's connections by member: one entry per distinct
        /// <see cref="PresenceMemberInfo.PublicId"/>, carrying the first connection's display data
        /// with <see cref="PresenceMemberInfo.DeviceCount"/> set to how many of that member's
        /// connections are in the map. Must be called with the list's own lock already held, so it
        /// can never run against a connection map a concurrent Join/Leave is only half done with.
        /// </summary>
        private static IReadOnlyList<PresenceMemberInfo> BuildSnapshot(ListPresence list)
        {
            return list.Connections.Values
                .GroupBy(m => m.PublicId)
                .Select(g => g.First() with { DeviceCount = g.Count() })
                .ToList();
        }
    }
}
