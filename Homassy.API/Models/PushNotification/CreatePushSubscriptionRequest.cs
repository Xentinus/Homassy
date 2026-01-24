using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.PushNotification
{
    public record CreatePushSubscriptionRequest
    {
        [Required]
        [StringLength(2048)]
        public required string Endpoint { get; init; }

        [Required]
        [StringLength(512)]
        public required string P256dh { get; init; }

        [Required]
        [StringLength(512)]
        public required string Auth { get; init; }

        [StringLength(512)]
        public string? UserAgent { get; init; }
    }
}
