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
