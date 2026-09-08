using System.Reflection;
using Homassy.API.Hubs;
using Homassy.API.Models.ShoppingList;

namespace Homassy.Tests.Unit
{
    public class ShoppingListPresenceTests
    {
        private readonly ShoppingListPresence _presence;

        public ShoppingListPresenceTests()
        {
            _presence = new ShoppingListPresence();
        }

        private static PresenceMemberInfo Member(Guid publicId, string displayName = "Member") => new()
        {
            PublicId = publicId,
            DisplayName = displayName,
            ProfilePictureUrl = null,
            IdentityColor = null,
            DeviceCount = 1
        };

        #region Join
        [Fact]
        public void Join_SingleConnection_ReturnsSnapshotContainingMember()
        {
            var listId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var snapshot = _presence.Join(listId, "conn-1", Member(userId, "Alice"));

            var entry = Assert.Single(snapshot);
            Assert.Equal(userId, entry.PublicId);
            Assert.Equal("Alice", entry.DisplayName);
            Assert.Equal(1, entry.DeviceCount);
        }

        [Fact]
        public void Join_SameUserTwoConnections_CollapsesToOneEntryWithDeviceCountTwo()
        {
            var listId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            _presence.Join(listId, "conn-1", Member(userId, "Alice"));
            var snapshot = _presence.Join(listId, "conn-2", Member(userId, "Alice"));

            var entry = Assert.Single(snapshot);
            Assert.Equal(userId, entry.PublicId);
            Assert.Equal(2, entry.DeviceCount);
        }
        #endregion

        #region Leave
        [Fact]
        public void Leave_OneOfTwoConnectionsForSameUser_KeepsEntryWithDeviceCountOne()
        {
            var listId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            _presence.Join(listId, "conn-1", Member(userId, "Alice"));
            _presence.Join(listId, "conn-2", Member(userId, "Alice"));

            var snapshot = _presence.Leave(listId, "conn-2");

            var entry = Assert.Single(snapshot);
            Assert.Equal(userId, entry.PublicId);
            Assert.Equal(1, entry.DeviceCount);
        }

        [Fact]
        public void Leave_LastConnection_RemovesEntryEntirely()
        {
            var listId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            _presence.Join(listId, "conn-1", Member(userId, "Alice"));

            var snapshot = _presence.Leave(listId, "conn-1");

            Assert.Empty(snapshot);
        }

        [Fact]
        public void Leave_UnknownConnectionId_IsNoOpAndDoesNotThrow()
        {
            var listId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            _presence.Join(listId, "conn-1", Member(userId, "Alice"));

            var exception = Record.Exception(() => _presence.Leave(listId, "unknown-connection"));

            Assert.Null(exception);

            var snapshot = _presence.Snapshot(listId);
            var entry = Assert.Single(snapshot);
            Assert.Equal(userId, entry.PublicId);
        }
        #endregion

        #region Disconnect
        [Fact]
        public void Disconnect_RemovesConnectionFromEveryJoinedList_ReturnsOnePairPerAffectedList()
        {
            var listA = Guid.NewGuid();
            var listB = Guid.NewGuid();
            var listC = Guid.NewGuid();
            var userId = Guid.NewGuid();

            _presence.Join(listA, "conn-1", Member(userId));
            _presence.Join(listB, "conn-1", Member(userId));
            // conn-1 never joined listC — it must not appear in the affected set below.
            _presence.Join(listC, "conn-2", Member(Guid.NewGuid()));

            var affected = _presence.Disconnect("conn-1");

            Assert.Equal(2, affected.Count);
            Assert.All(affected, pair => Assert.Empty(pair.Members));
            Assert.Contains(affected, pair => pair.ListPublicId == listA);
            Assert.Contains(affected, pair => pair.ListPublicId == listB);
            Assert.DoesNotContain(affected, pair => pair.ListPublicId == listC);

            // listC was never touched by the disconnect — conn-2 is still present.
            Assert.Single(_presence.Snapshot(listC));
        }
        #endregion

        #region Snapshot
        [Fact]
        public void Snapshot_ListNobodyJoined_IsEmptyNotNull()
        {
            var snapshot = _presence.Snapshot(Guid.NewGuid());

            Assert.NotNull(snapshot);
            Assert.Empty(snapshot);
        }
        #endregion

        #region Concurrency
        /// <summary>
        /// 200 Join/Leave pairs, each its own connection id, fired at once across only 4 lists —
        /// heavy contention on each list's bucket, including the moment a list's connection map
        /// empties and its bucket is retired from the outer dictionary while another pair is
        /// concurrently trying to join the same list. A registry that is not genuinely thread-safe
        /// either throws (a plain `Dictionary` mutated from two threads) or silently drops a
        /// membership (a naive "remove the bucket when empty" that resurrects a retired bucket).
        ///
        /// A dropped join is not visible in its own <see cref="ShoppingListPresence.Join"/> return
        /// value: if a regression ever drops the `Retired` recheck, the write and the snapshot
        /// build still happen back-to-back under the same lock, on whatever bucket the caller is
        /// holding — orphaned or not — so that returned snapshot contains the caller's own member
        /// either way. What actually breaks is visibility through the registry: an orphaned bucket
        /// is unreachable from `_lists`, so every iteration also re-resolves the list via an
        /// independent <see cref="ShoppingListPresence.Snapshot"/> call right after joining, which
        /// walks `_lists` from scratch and can only ever see a bucket that is still current. Both
        /// checks are collected per iteration and asserted after the fact, alongside the original
        /// "nothing threw" / "every list ends up empty" checks.
        /// </summary>
        [Fact]
        public async Task JoinAndLeave_TwoHundredParallelPairsAcrossFourLists_LeaveEveryListEmpty()
        {
            var lists = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
            const int pairCount = 200;

            var tasks = Enumerable.Range(0, pairCount).Select(i => Task.Run(() =>
            {
                var listId = lists[i % lists.Length];
                var connectionId = $"conn-{i}";
                var member = Member(Guid.NewGuid(), $"User{i}");

                var joinSnapshot = _presence.Join(listId, connectionId, member);
                var sawSelfInJoinSnapshot = joinSnapshot.Any(m => m.PublicId == member.PublicId);
                var sawSelfInLiveSnapshot = _presence.Snapshot(listId).Any(m => m.PublicId == member.PublicId);

                _presence.Leave(listId, connectionId);

                return sawSelfInJoinSnapshot && sawSelfInLiveSnapshot;
            })).ToArray();

            var observedSelf = Array.Empty<bool>();
            var exception = await Record.ExceptionAsync(async () =>
            {
                observedSelf = await Task.WhenAll(tasks);
            });

            Assert.Null(exception);
            Assert.Equal(pairCount, observedSelf.Length);
            Assert.All(observedSelf, sawSelf => Assert.True(sawSelf, "a Join must observe its own membership both in its returned snapshot and in an independent live Snapshot() call"));

            foreach (var listId in lists)
            {
                Assert.Empty(_presence.Snapshot(listId));
            }
        }
        #endregion

        #region Join racing its own Disconnect
        /// <summary>
        /// Reads the private reverse index's size via reflection. There is no public accessor for
        /// it - <see cref="ShoppingListPresence"/> deliberately exposes only per-list state
        /// (<see cref="ShoppingListPresence.Snapshot"/>) - but this is exactly the map the bug
        /// leaked an entry into per occurrence, so a test claiming "no entry survives in either
        /// map" has to look at it directly.
        /// </summary>
        private static int ConnectionRegistrySize(ShoppingListPresence presence)
        {
            var field = typeof(ShoppingListPresence).GetField("_connectionLists", BindingFlags.NonPublic | BindingFlags.Instance);
            var dictionary = (System.Collections.ICollection)field!.GetValue(presence)!;
            return dictionary.Count;
        }

        /// <summary>
        /// SignalR does not wait for in-flight hub invocations before calling
        /// <c>OnDisconnectedAsync</c>: a tab can close while <c>ShoppingListHub.JoinList</c> is
        /// still between its group-add and its call into <see cref="ShoppingListPresence.Join"/>,
        /// so <see cref="ShoppingListPresence.Disconnect"/> can run - and, finding nothing yet
        /// registered for the connection, return - before that in-flight <see cref="ShoppingListPresence.Join"/>
        /// goes on to write into both of this class's maps for a connection nothing will ever call
        /// Disconnect for again. 200 independent (Join, Disconnect) pairs, each racing on its own
        /// connection id, launched together rather than one at a time: Disconnect's early-return
        /// path is far cheaper than Join's two-map registration, so the scheduler reliably
        /// interleaves plenty of these pairs into exactly the bad order without any artificial
        /// synchronization forcing it - the same "just launch them and see" shape as the existing
        /// 200-pair concurrency test above, which pairs a Join with its own later Leave and so
        /// never exercises a Disconnect racing the Join for the same connection at all.
        /// </summary>
        [Fact]
        public async Task JoinRacingDisconnect_ForTheSameConnection_StrandsNoEntryInEitherMap()
        {
            const int pairCount = 200;

            var tasks = Enumerable.Range(0, pairCount).Select(i => Task.Run(() =>
            {
                var listId = Guid.NewGuid();
                var connectionId = $"race-conn-{i}";
                var member = Member(Guid.NewGuid(), $"Racer{i}");

                var joinTask = Task.Run(() => _presence.Join(listId, connectionId, member));
                var disconnectTask = Task.Run(() => _presence.Disconnect(connectionId));
                Task.WaitAll(joinTask, disconnectTask);

                return listId;
            })).ToArray();

            var listIds = await Task.WhenAll(tasks);

            Assert.All(listIds, listId => Assert.Empty(_presence.Snapshot(listId)));
            Assert.Equal(0, ConnectionRegistrySize(_presence));
        }
        #endregion
    }
}
