namespace Homassy.API.Models.User
{
    public record UserInfo
    {
        /// <summary>Stable public identifier; the client derives this member's fallback identity colour from it.</summary>
        public Guid PublicId { get; init; }

        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>
        /// Path to this user's avatar thumbnail, versioned so it can be cached forever, or null
        /// when they have no picture. See <see cref="Constants.MediaUrls"/>.
        /// </summary>
        public string? ProfilePictureUrl { get; init; }

        public string TimeZone { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string Currency { get; init; } = string.Empty;

        /// <summary>Chosen identity-colour key from the curated palette, or null for the deterministic pick.</summary>
        public string? IdentityColor { get; init; }

        /// <summary>
        /// When this user was last seen, as the server knows it - or null if it has never been
        /// written for them (#127).
        /// </summary>
        /// <remarks>
        /// Sent so a client with no last-seen of its own can still ask a meaningful "what changed
        /// while I was away" - the first launch on a new device, which is the one case a device-local
        /// value cannot cover. A client that has its own value prefers it: this one lags by up to a
        /// flush interval and counts every device the user has (see
        /// <c>Entities.User.UserProfile.LastSeenAt</c>).
        /// <para>
        /// It is the value <em>before</em> this request's own stamp: the stamp lives in
        /// <see cref="Services.LastSeenTracker"/>'s memory until the next flush, so a caller reading
        /// this at boot gets the moment they were previously here rather than "now".
        /// </para>
        /// </remarks>
        public DateTime? LastSeenAt { get; init; }

        /// <summary>
        /// When this user finished or skipped the first-run tour, or null if they have not (#98).
        /// Null is what makes the client start the tour, so this has to ride on the payload the
        /// app already fetches at boot rather than needing a request of its own.
        /// </summary>
        public DateTime? OnboardingCompletedAt { get; init; }
    }
}
