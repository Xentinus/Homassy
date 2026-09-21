extern alias NotificationsProject;
using System.Net;
using Homassy.Data.Enums;
using NotificationsProject::Homassy.Notifications.Services;
using NotificationsProject::Homassy.Notifications.Workers;
using WebPush;

namespace Homassy.Tests.Unit;

/// <summary>
/// The two decisions that stand between a notification and the device it is meant for: whether a
/// failed send means the subscription is finished, and which channels a chat burst may use.
/// </summary>
/// <remarks>
/// Both used to be all-or-nothing and both silently cost a user their notifications. A transient
/// push failure deleted the subscription, so one bad minute at the push service muted a device
/// permanently; and muting chat notifications removed the member from the recipient list entirely,
/// taking their notification-centre row with the push they had actually asked to silence.
/// </remarks>
public class PushDeliveryTests
{
    // =========================================================================
    // WebPushService.Classify - what a failed send says about the subscription
    // =========================================================================

    private static WebPushException Failure(HttpStatusCode statusCode) =>
        new(
            $"Push failed with {statusCode}",
            new PushSubscription("https://push.example/endpoint", "p256dh", "auth"),
            new HttpResponseMessage(statusCode));

    [Theory]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.NotFound)]
    public void Classify_EndpointIsFinished_ReportsSubscriptionGone(HttpStatusCode statusCode)
    {
        Assert.Equal(PushSendResult.SubscriptionGone, WebPushService.Classify(Failure(statusCode)));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public void Classify_PushServiceMisbehaved_KeepsTheSubscription(HttpStatusCode statusCode)
    {
        Assert.Equal(PushSendResult.TransientFailure, WebPushService.Classify(Failure(statusCode)));
    }

    [Fact]
    public void Classify_NetworkFault_KeepsTheSubscription()
    {
        Assert.Equal(PushSendResult.TransientFailure, WebPushService.Classify(new HttpRequestException("no route to host")));
        Assert.Equal(PushSendResult.TransientFailure, WebPushService.Classify(new TaskCanceledException("timed out")));
    }

    // =========================================================================
    // FamilyChatNotificationService.ResolveCandidates - which channels a burst may use
    // =========================================================================

    private const int MutedUser = 1;
    private const int PlainUser = 2;

    private static readonly DateTime BurstSentAt = new(2026, 9, 21, 18, 0, 0, DateTimeKind.Utc);

    private static RecipientInfo Recipient(int id, bool push, bool inApp) =>
        new(id, Language.Hungarian, UserTimeZone.CentralEuropeStandardTime, push, inApp);

    private static List<RecipientInfo> Resolve(
        IEnumerable<RecipientInfo> recipients,
        IEnumerable<int>? muted = null,
        IDictionary<int, DateTime>? readAt = null) =>
        FamilyChatNotificationService.ResolveCandidates(
            recipients.ToList(),
            (muted ?? []).ToHashSet(),
            readAt is null ? new Dictionary<int, DateTime>() : new Dictionary<int, DateTime>(readAt),
            BurstSentAt);

    [Fact]
    public void ResolveCandidates_MutedChat_KeepsTheInboxAndDropsOnlyThePush()
    {
        var candidates = Resolve(
            [Recipient(MutedUser, push: true, inApp: true)],
            muted: [MutedUser]);

        var candidate = Assert.Single(candidates);
        Assert.False(candidate.PushEnabled);
        Assert.True(candidate.InAppEnabled);
    }

    [Fact]
    public void ResolveCandidates_PushOffAndInboxOn_StillGetsTheInboxRow()
    {
        // The case that made this a bug report: the settings drawer cleared the chat flag whenever
        // the master push switch was turned off, so this user was muted *and* push-less - and the
        // old filter then removed them from the only channel they had left.
        var candidates = Resolve(
            [Recipient(MutedUser, push: false, inApp: true)],
            muted: [MutedUser]);

        var candidate = Assert.Single(candidates);
        Assert.False(candidate.PushEnabled);
        Assert.True(candidate.InAppEnabled);
    }

    [Fact]
    public void ResolveCandidates_MutedWithNoInbox_IsDropped()
    {
        var candidates = Resolve(
            [Recipient(MutedUser, push: true, inApp: false)],
            muted: [MutedUser]);

        Assert.Empty(candidates);
    }

    [Fact]
    public void ResolveCandidates_NotMuted_KeepsBothChannels()
    {
        var candidate = Assert.Single(Resolve([Recipient(PlainUser, push: true, inApp: true)]));

        Assert.True(candidate.PushEnabled);
        Assert.True(candidate.InAppEnabled);
    }

    [Fact]
    public void ResolveCandidates_AlreadyRead_IsDroppedFromBothChannels()
    {
        // Unlike a mute, this is not a channel preference: there is nothing to tell somebody who
        // has read the messages, on either surface.
        var candidates = Resolve(
            [Recipient(PlainUser, push: true, inApp: true)],
            readAt: new Dictionary<int, DateTime> { [PlainUser] = BurstSentAt.AddSeconds(1) });

        Assert.Empty(candidates);
    }

    [Fact]
    public void ResolveCandidates_ReadBeforeTheBurst_IsStillNotified()
    {
        Assert.Single(Resolve(
            [Recipient(PlainUser, push: true, inApp: true)],
            readAt: new Dictionary<int, DateTime> { [PlainUser] = BurstSentAt.AddSeconds(-1) }));
    }

    [Fact]
    public void ResolveCandidates_MutesOnePersonOnly()
    {
        var candidates = Resolve(
            [Recipient(MutedUser, push: true, inApp: true), Recipient(PlainUser, push: true, inApp: true)],
            muted: [MutedUser]);

        Assert.Equal(2, candidates.Count);
        Assert.False(candidates.Single(c => c.Id == MutedUser).PushEnabled);
        Assert.True(candidates.Single(c => c.Id == PlainUser).PushEnabled);
    }
}
