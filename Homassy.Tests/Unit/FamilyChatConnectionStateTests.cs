using Homassy.API.Hubs;

namespace Homassy.Tests.Unit;

/// <summary>
/// The chat hub's per-connection state bag (#148, #149).
/// </summary>
/// <remarks>
/// Everything here is about the cases no client ever reports: a connection that stops sending, a
/// user with two devices, a flag nobody clears. Those are what the TTL and the sweep exist for, so
/// they are what this pins.
/// </remarks>
public class FamilyChatConnectionStateTests
{
    private static readonly Guid Family = Guid.NewGuid();
    private static readonly Guid AnnaPublicId = Guid.NewGuid();
    private static readonly Guid BelaPublicId = Guid.NewGuid();
    private const int AnnaUserId = 1;
    private const int BelaUserId = 2;

    private static readonly DateTime Now = new(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);

    private static FamilyChatConnectionState WithAnnaAndBela(out string annaConnection, out string belaConnection)
    {
        var state = new FamilyChatConnectionState();
        annaConnection = "conn-anna";
        belaConnection = "conn-bela";

        state.Register(annaConnection, Family, AnnaPublicId, AnnaUserId, "Anna");
        state.Register(belaConnection, Family, BelaPublicId, BelaUserId, "Bela");

        return state;
    }

    #region Typing

    [Fact]
    public void TypingIn_ReportsAConnectionThatSaidItIsTyping()
    {
        var state = WithAnnaAndBela(out var anna, out _);

        state.SetTyping(anna, true, Now);

        var typing = state.TypingIn(Family, Now);
        Assert.Equal("Anna", Assert.Single(typing).DisplayName);
    }

    [Fact]
    public void TypingIn_DropsTheFlagOnceItsTtlHasPassed()
    {
        // The point of the TTL: a client that never sends "stopped" - a closed tab, a dropped
        // connection - stops being a typist by itself.
        var state = WithAnnaAndBela(out var anna, out _);
        state.SetTyping(anna, true, Now);

        var after = Now + FamilyChatConnectionState.TypingTtl + TimeSpan.FromSeconds(1);

        Assert.Empty(state.TypingIn(Family, after));
    }

    [Fact]
    public void TypingIn_CollapsesAUsersDevicesIntoOneEntry()
    {
        var state = new FamilyChatConnectionState();
        state.Register("phone", Family, AnnaPublicId, AnnaUserId, "Anna");
        state.Register("laptop", Family, AnnaPublicId, AnnaUserId, "Anna");

        state.SetTyping("phone", true, Now);
        state.SetTyping("laptop", true, Now);

        Assert.Single(state.TypingIn(Family, Now));
    }

    [Fact]
    public void TypingIn_IgnoresAnotherFamilysTypists()
    {
        var state = WithAnnaAndBela(out var anna, out _);
        var otherFamily = Guid.NewGuid();
        state.Register("conn-other", otherFamily, Guid.NewGuid(), 3, "Someone else");

        state.SetTyping(anna, true, Now);
        state.SetTyping("conn-other", true, Now);

        Assert.Single(state.TypingIn(Family, Now));
        Assert.Single(state.TypingIn(otherFamily, Now));
    }

    [Fact]
    public void ClearTypingForUser_ClearsEveryDeviceOfThatUserOnly()
    {
        var state = new FamilyChatConnectionState();
        state.Register("phone", Family, AnnaPublicId, AnnaUserId, "Anna");
        state.Register("laptop", Family, AnnaPublicId, AnnaUserId, "Anna");
        state.Register("bela", Family, BelaPublicId, BelaUserId, "Bela");

        state.SetTyping("phone", true, Now);
        state.SetTyping("laptop", true, Now);
        state.SetTyping("bela", true, Now);

        // Having sent the message, Anna is no longer typing it - on either device.
        state.ClearTypingForUser(AnnaUserId);

        var typing = state.TypingIn(Family, Now);
        Assert.Equal("Bela", Assert.Single(typing).DisplayName);
    }

    [Fact]
    public void Remove_TakesTheConnectionOutOfTheTypingSet()
    {
        var state = WithAnnaAndBela(out var anna, out _);
        state.SetTyping(anna, true, Now);

        var family = state.Remove(anna);

        Assert.Equal(Family, family);
        Assert.Empty(state.TypingIn(Family, Now));
    }

    [Fact]
    public void SetTyping_ForAnUnregisteredConnection_ReportsNoFamily()
    {
        var state = new FamilyChatConnectionState();

        Assert.Null(state.SetTyping("never-joined", true, Now));
    }

    #endregion

    #region Sweeping

    [Fact]
    public void SweepExpiredTyping_NamesTheFamilyWhoseTypistExpired()
    {
        var state = WithAnnaAndBela(out var anna, out _);
        state.SetTyping(anna, true, Now);

        var after = Now + FamilyChatConnectionState.TypingTtl + TimeSpan.FromSeconds(1);
        var affected = state.SweepExpiredTyping(after);

        Assert.Equal(Family, Assert.Single(affected));
    }

    [Fact]
    public void SweepExpiredTyping_IsQuietWhenNothingHasExpired()
    {
        var state = WithAnnaAndBela(out var anna, out _);
        state.SetTyping(anna, true, Now);

        // A tick that finds nothing must broadcast nothing - this loop runs every couple of
        // seconds for the life of the process.
        Assert.Empty(state.SweepExpiredTyping(Now + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void SweepExpiredTyping_ClearsTheFlagSoASecondSweepIsQuiet()
    {
        var state = WithAnnaAndBela(out var anna, out _);
        state.SetTyping(anna, true, Now);

        var after = Now + FamilyChatConnectionState.TypingTtl + TimeSpan.FromSeconds(1);
        state.SweepExpiredTyping(after);

        Assert.Empty(state.SweepExpiredTyping(after));
    }

    #endregion

    #region Active (#149)

    [Fact]
    public void IsUserActive_IsTrueWhileTheFlagIsFresh()
    {
        var state = WithAnnaAndBela(out var anna, out _);
        state.SetActive(anna, true, Now);

        Assert.True(state.IsUserActive(AnnaUserId, Now));
    }

    [Fact]
    public void IsUserActive_IsTrueWhenAnyOneDeviceIsActive()
    {
        // A laptop with the panel open must suppress the push to the phone in your pocket.
        var state = new FamilyChatConnectionState();
        state.Register("phone", Family, AnnaPublicId, AnnaUserId, "Anna");
        state.Register("laptop", Family, AnnaPublicId, AnnaUserId, "Anna");

        state.SetActive("laptop", true, Now);

        Assert.True(state.IsUserActive(AnnaUserId, Now));
    }

    [Fact]
    public void IsUserActive_ExpiresWithoutAHeartbeat()
    {
        // A dropped connection has to stop counting as "watching", or the next message is never
        // announced to somebody who is no longer there.
        var state = WithAnnaAndBela(out var anna, out _);
        state.SetActive(anna, true, Now);

        var after = Now + FamilyChatConnectionState.ActiveTtl + TimeSpan.FromSeconds(1);

        Assert.False(state.IsUserActive(AnnaUserId, after));
    }

    [Fact]
    public void IsUserActive_IsFalseOnceTheConnectionIsRemoved()
    {
        var state = WithAnnaAndBela(out var anna, out _);
        state.SetActive(anna, true, Now);

        state.Remove(anna);

        Assert.False(state.IsUserActive(AnnaUserId, Now));
    }

    [Fact]
    public void SetActive_False_ClearsTheFlagImmediately()
    {
        var state = WithAnnaAndBela(out var anna, out _);
        state.SetActive(anna, true, Now);

        state.SetActive(anna, false, Now);

        Assert.False(state.IsUserActive(AnnaUserId, Now));
    }

    #endregion
}
