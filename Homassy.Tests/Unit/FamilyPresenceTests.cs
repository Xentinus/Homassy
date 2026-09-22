using Homassy.API.Hubs;
using Homassy.API.Models.Family;

namespace Homassy.Tests.Unit;

/// <summary>
/// The household presence registry behind <see cref="PresenceHub"/>: one entry per live
/// connection, collapsed per member when anyone asks who is around.
/// </summary>
public class FamilyPresenceTests
{
    private const string FamilyScope = "family:1";
    private const string OtherScope = "family:2";

    private static FamilyPresenceMemberInfo Member(Guid publicId, string displayName) => new()
    {
        PublicId = publicId,
        DisplayName = displayName,
        DeviceCount = 1
    };

    [Fact]
    public void Join_ReturnsSnapshotContainingTheJoiner()
    {
        var presence = new FamilyPresence();
        var eszter = Guid.NewGuid();

        var snapshot = presence.Join(FamilyScope, "conn-1", Member(eszter, "Eszter"));

        var member = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<FamilyPresenceMemberInfo>>(snapshot));
        Assert.Equal(eszter, member.PublicId);
        Assert.Equal(1, member.DeviceCount);
        Assert.False(member.IsShopping);
    }

    [Fact]
    public void Join_SameMemberTwice_CollapsesIntoOneEntryWithADeviceCount()
    {
        var presence = new FamilyPresence();
        var eszter = Guid.NewGuid();

        presence.Join(FamilyScope, "phone", Member(eszter, "Eszter"));
        presence.Join(FamilyScope, "laptop", Member(eszter, "Eszter"));

        var member = Assert.Single(presence.Snapshot(FamilyScope));
        Assert.Equal(2, member.DeviceCount);
    }

    [Fact]
    public void Snapshot_IsScopedToOneHousehold()
    {
        var presence = new FamilyPresence();

        presence.Join(FamilyScope, "ours", Member(Guid.NewGuid(), "Eszter"));
        presence.Join(OtherScope, "theirs", Member(Guid.NewGuid(), "Stranger"));

        Assert.Single(presence.Snapshot(FamilyScope));
        Assert.Single(presence.Snapshot(OtherScope));
    }

    [Fact]
    public void Snapshot_UnknownScope_IsEmptyRatherThanNull()
    {
        Assert.Empty(new FamilyPresence().Snapshot("family:404"));
    }

    [Fact]
    public void SetShopping_MarksTheMemberAndCarriesTheListName()
    {
        var presence = new FamilyPresence();
        var eszter = Guid.NewGuid();
        presence.Join(FamilyScope, "phone", Member(eszter, "Eszter"));

        var changed = presence.SetShopping("phone", "Aldi");

        Assert.NotNull(changed);
        Assert.Equal(FamilyScope, changed!.Value.ScopeKey);
        var member = Assert.Single(changed.Value.Members);
        Assert.True(member.IsShopping);
        Assert.Equal("Aldi", member.ShoppingContext);
    }

    [Fact]
    public void SetShopping_OnOneDevice_MakesTheWholeMemberShopping()
    {
        // The phone is in the shop and the laptop is on the kitchen table. To everyone else that
        // member is at the shop — which is the only fact the strip is claiming.
        var presence = new FamilyPresence();
        var eszter = Guid.NewGuid();
        presence.Join(FamilyScope, "phone", Member(eszter, "Eszter"));
        presence.Join(FamilyScope, "laptop", Member(eszter, "Eszter"));

        presence.SetShopping("phone", "Aldi");

        var member = Assert.Single(presence.Snapshot(FamilyScope));
        Assert.True(member.IsShopping);
        Assert.Equal("Aldi", member.ShoppingContext);
        Assert.Equal(2, member.DeviceCount);
    }

    [Fact]
    public void SetShopping_Null_ClearsIt()
    {
        var presence = new FamilyPresence();
        presence.Join(FamilyScope, "phone", Member(Guid.NewGuid(), "Eszter"));
        presence.SetShopping("phone", "Aldi");

        presence.SetShopping("phone", null);

        var member = Assert.Single(presence.Snapshot(FamilyScope));
        Assert.False(member.IsShopping);
        Assert.Null(member.ShoppingContext);
    }

    [Fact]
    public void SetShopping_UnknownConnection_IsANoOp()
    {
        Assert.Null(new FamilyPresence().SetShopping("ghost", "Aldi"));
    }

    [Fact]
    public void SetShopping_LeavesTheRestOfTheHouseholdAlone()
    {
        var presence = new FamilyPresence();
        var eszter = Guid.NewGuid();
        var bela = Guid.NewGuid();
        presence.Join(FamilyScope, "eszter-phone", Member(eszter, "Eszter"));
        presence.Join(FamilyScope, "bela-phone", Member(bela, "Béla"));

        presence.SetShopping("eszter-phone", "Aldi");

        var snapshot = presence.Snapshot(FamilyScope);
        Assert.True(snapshot.Single(m => m.PublicId == eszter).IsShopping);
        Assert.False(snapshot.Single(m => m.PublicId == bela).IsShopping);
    }

    [Fact]
    public void Disconnect_RemovesOneDeviceButKeepsTheMemberWhileAnotherIsLive()
    {
        var presence = new FamilyPresence();
        var eszter = Guid.NewGuid();
        presence.Join(FamilyScope, "phone", Member(eszter, "Eszter"));
        presence.Join(FamilyScope, "laptop", Member(eszter, "Eszter"));

        var changed = presence.Disconnect("phone");

        Assert.NotNull(changed);
        var member = Assert.Single(changed!.Value.Members);
        Assert.Equal(1, member.DeviceCount);
    }

    [Fact]
    public void Disconnect_LastDevice_EmptiesTheRoster()
    {
        var presence = new FamilyPresence();
        presence.Join(FamilyScope, "phone", Member(Guid.NewGuid(), "Eszter"));

        var changed = presence.Disconnect("phone");

        Assert.NotNull(changed);
        Assert.Empty(changed!.Value.Members);
        Assert.Empty(presence.Snapshot(FamilyScope));
    }

    [Fact]
    public void Disconnect_UnknownConnection_IsANoOp()
    {
        Assert.Null(new FamilyPresence().Disconnect("never-joined"));
    }

    [Fact]
    public void Join_AfterThatConnectionAlreadyDisconnected_IsRefused()
    {
        // SignalR does not wait for in-flight invocations before calling OnDisconnectedAsync, so
        // a tab closed mid-connect can disconnect before the join it raced. Nothing will ever
        // disconnect that id again, so a late join must not register somebody who has gone.
        var presence = new FamilyPresence();
        presence.Disconnect("doomed");

        var snapshot = presence.Join(FamilyScope, "doomed", Member(Guid.NewGuid(), "Eszter"));

        Assert.Null(snapshot);
        Assert.Empty(presence.Snapshot(FamilyScope));
    }

    [Fact]
    public void Join_IsSafeUnderConcurrentConnectsAndDisconnects()
    {
        var presence = new FamilyPresence();
        var members = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();

        Parallel.For(0, 64, i =>
        {
            var connectionId = $"conn-{i}";
            presence.Join(FamilyScope, connectionId, Member(members[i % members.Length], $"Member {i % members.Length}"));
            presence.SetShopping(connectionId, i % 2 == 0 ? "Aldi" : null);

            if (i % 4 == 0)
            {
                presence.Disconnect(connectionId);
            }
        });

        var snapshot = presence.Snapshot(FamilyScope);
        Assert.Equal(48, snapshot.Sum(m => m.DeviceCount));
        Assert.All(snapshot, m => Assert.Contains(m.PublicId, members));
    }
}
