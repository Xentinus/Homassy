namespace Homassy.API.Models.Notification
{
    /// <summary>One page of the notification centre, newest first (#116).</summary>
    /// <remarks>
    /// Cursor-paged rather than page-numbered, for the same reason the activity timeline is: new
    /// notifications arrive at the top while the list is open, and a numeric offset would shift
    /// rows out from under the reader - a page 2 that re-shows what page 1 already did, or skips
    /// past something never shown.
    /// </remarks>
    public record NotificationPage
    {
        public IReadOnlyList<NotificationInfo> Items { get; init; } = [];

        /// <summary>
        /// Opaque cursor for the next page (see <c>Models.Activity.ActivityCursor</c>), or null
        /// when this page reached the end.
        /// </summary>
        public string? NextCursor { get; init; }

        /// <summary>
        /// The caller's unread count across the whole inbox, not just this page.
        /// </summary>
        /// <remarks>
        /// Sent with every page so the header badge is corrected by the act of reading the list -
        /// the one moment the client is guaranteed to be looking. It is also available on its own
        /// (<c>GET /notification/unread-count</c>) for the boot and push-arrival cases, where
        /// fetching a page just to learn a number would be wasteful.
        /// </remarks>
        public int UnreadCount { get; init; }
    }
}
