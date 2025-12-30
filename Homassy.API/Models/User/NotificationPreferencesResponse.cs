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

        // In-app notifications
        public bool InAppNotificationsEnabled { get; init; }
    }
}
