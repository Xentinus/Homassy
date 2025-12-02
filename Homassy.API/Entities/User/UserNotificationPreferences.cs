using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.User
{
    public class UserNotificationPreferences : RecordChangeEntity
    {
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        // Email notifications
        public bool EmailNotificationsEnabled { get; set; } = true;
        public bool EmailWeeklySummaryEnabled { get; set; } = true;

        // Push notifications
        public bool PushNotificationsEnabled { get; set; } = true;
        public bool PushWeeklySummaryEnabled { get; set; } = true;

        // In-app notifications
        public bool InAppNotificationsEnabled { get; set; } = true;

        // Navigation
        public User User { get; set; } = null!;
    }
}
