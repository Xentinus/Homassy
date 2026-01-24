using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.User
{
    public class UserPushSubscription : RecordChangeEntity
    {
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        [Required]
        [StringLength(2048)]
        public required string Endpoint { get; set; }

        [Required]
        [StringLength(512)]
        public required string P256dh { get; set; }

        [Required]
        [StringLength(512)]
        public required string Auth { get; set; }

        [StringLength(512)]
        public string? UserAgent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastDailyNotificationSentAt { get; set; }
        public DateTime? LastWeeklyNotificationSentAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
    }
}
