using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.User
{
    public class UserAuthentication : RecordChangeEntity
    {
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        // Email verification
        public string? VerificationCode { get; set; }
        public DateTime? VerificationCodeExpiry { get; set; }

        // JWT tokens
        public string? AccessToken { get; set; }
        public DateTime? AccessTokenExpiry { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }

        // Security tracking
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LastFailedLoginAt { get; set; }
        public DateTime? LockedOutUntil { get; set; }
        public DateTime? BanExpiresAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
    }
}
