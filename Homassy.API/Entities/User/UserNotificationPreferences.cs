using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.User
{
    public class UserNotificationPreferences : RecordChangeEntity
    {
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        // Email notifications
        public bool EmailNotificationsEnabled { get; set; } = false;
        public bool EmailWeeklySummaryEnabled { get; set; } = false;
        public DateTime? LastWeeklyEmailSentAt { get; set; }

        // Push notifications
        public bool PushNotificationsEnabled { get; set; } = false;
        public bool PushWeeklySummaryEnabled { get; set; } = false;

        // In-app notifications
        public bool InAppNotificationsEnabled { get; set; } = false;

        // Navigation
        public User User { get; set; } = null!;
    }
}
