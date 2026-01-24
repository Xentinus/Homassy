using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.User
{
    public class User : RecordChangeEntity
    {
        public int? FamilyId { get; set; }

        [EmailAddress]
        public required string Email { get; set; }

        [StringLength(128, MinimumLength = 2)]
        public required string Name { get; set; }

        [EnumDataType(typeof(UserStatus))]
        public UserStatus Status { get; set; } = UserStatus.PendingVerification;

        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

       // Navigation properties
       public UserProfile? Profile { get; set; }
       public UserNotificationPreferences? NotificationPreferences { get; set; }
       public UserAuthentication? Authentication { get; set; }
       public ICollection<UserPushSubscription> PushSubscriptions { get; set; } = new List<UserPushSubscription>();
    }
}
