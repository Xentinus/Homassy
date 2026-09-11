namespace Homassy.API.Models.Notification
{
    /// <summary>
    /// The caller's unread notification count (#116).
    /// </summary>
    /// <remarks>
    /// A wrapper rather than a bare <c>int</c> in the <c>ApiResponse</c> envelope, and it is what
    /// every mutating endpoint answers with too: marking one row read, marking all read and
    /// dismissing a row all change the badge, so each returns the new number and the client never
    /// has to guess it or re-fetch to find out.
    /// </remarks>
    public record UnreadCountResponse
    {
        public int UnreadCount { get; init; }
    }
}
