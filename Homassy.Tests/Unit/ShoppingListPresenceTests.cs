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

                _presence.Join(listId, connectionId, Member(Guid.NewGuid(), $"User{i}"));
                _presence.Leave(listId, connectionId);
            }));

            var exception = await Record.ExceptionAsync(() => Task.WhenAll(tasks));

            Assert.Null(exception);
            foreach (var listId in lists)
            {
                Assert.Empty(_presence.Snapshot(listId));
            }
        }
        #endregion
    }
}
