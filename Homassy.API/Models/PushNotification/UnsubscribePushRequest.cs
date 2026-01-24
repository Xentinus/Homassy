using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.PushNotification
{
    public record UnsubscribePushRequest
    {
        [Required]
        [StringLength(2048)]
        public required string Endpoint { get; init; }
    }
}
