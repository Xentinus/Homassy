namespace Homassy.API.Models.User
{
    public class NotificationPreferencesResponse
    {
        // Email notifications
        public bool EmailNotificationsEnabled { get; init; }
        public bool EmailWeeklySummaryEnabled { get; init; }

        // Push notifications
        public bool PushNotificationsEnabled { get; init; }
        public bool PushWeeklySummaryEnabled { get; init; }

        /// <summary>Whether family chat messages may notify this user (#149). Subordinate to <see cref="PushNotificationsEnabled"/>.</summary>
        public bool PushFamilyChatEnabled { get; init; }

        // In-app notifications
        public bool InAppNotificationsEnabled { get; init; }
    }
}
