using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.User
{
    public class UserAuthentication : RecordChangeEntity
    {
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        public string? VerificationCode { get; set; }
        public DateTime? VerificationCodeExpiry { get; set; }

        public string? AccessToken { get; set; }
        public DateTime? AccessTokenExpiry { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }

        public Guid? TokenFamily { get; set; }
        public string? PreviousRefreshToken { get; set; }
        public DateTime? PreviousRefreshTokenExpiry { get; set; }

        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LastFailedLoginAt { get; set; }
        public DateTime? LockedOutUntil { get; set; }
        public DateTime? BanExpiresAt { get; set; }

        public User User { get; set; } = null!;
    }
}
