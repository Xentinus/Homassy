namespace Homassy.API.Models.Notification
{
    /// <summary>One row of the notification centre (#116).</summary>
    public record NotificationInfo
    {
        public Guid PublicId { get; init; }

        /// <summary>
        /// The <see cref="Enums.NotificationType"/> member's <em>name</em>, e.g.
        /// <c>"ShoppingListItemsAdded"</c>.
        /// </summary>
        /// <remarks>
        /// A name, not the numeric value the column stores: it is the key the client looks the
        /// localized template up under, and a locale file full of
        /// <c>notifications.types.13.title</c> would be unreadable and impossible to review. The
        /// numbers stay an implementation detail of the column.
        /// </remarks>
        public string Type { get; init; } = string.Empty;

        /// <summary>
        /// The values the localized template interpolates. See <see cref="NotificationEnvelope"/>
        /// for which keys each type carries.
        /// </summary>
        public IReadOnlyDictionary<string, string> Parameters { get; init; }
            = NotificationEnvelope.NoParameters;

        /// <summary>App-relative path to open when the row is tapped, or null.</summary>
        public string? TargetUrl { get; init; }

        /// <summary>When the notification was emitted (UTC).</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>Whether this user has read it.</summary>
        public bool IsRead { get; init; }
    }
}
