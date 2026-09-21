namespace Homassy.Notifications.Services;

/// <summary>
/// What happened to one Web Push send, told apart by whether the subscription is still worth
/// keeping.
/// </summary>
/// <remarks>
/// This used to be a <c>bool</c>, and every caller read <c>false</c> as "this subscription is
/// dead" and soft-deleted the row. But <c>false</c> also came back for a rate limit, a 5xx from
/// the push service, a DNS hiccup or a cancelled request — so a single bad minute at FCM
/// permanently unsubscribed the device, and the user was never told: the notification centre kept
/// filling while nothing ever reached the phone again.
/// <para>
/// Only <see cref="SubscriptionGone"/> means the endpoint will never accept another push (404 /
/// 410, the two statuses the Web Push spec defines for it). Everything else is
/// <see cref="TransientFailure"/>: this send is lost, the subscription is not.
/// </para>
/// </remarks>
public enum PushSendResult
{
    /// <summary>The push service accepted the message.</summary>
    Delivered,

    /// <summary>
    /// The endpoint is permanently invalid (HTTP 404 or 410). The subscription row should be
    /// removed — the browser has thrown this subscription away and will never receive on it again.
    /// </summary>
    SubscriptionGone,

    /// <summary>
    /// The send failed for a reason that says nothing about the subscription: a rate limit, a
    /// server error at the push service, a network fault. Drop the message, keep the row.
    /// </summary>
    TransientFailure
}
