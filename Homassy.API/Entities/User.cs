using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities
{
    public class User : BaseEntity
    {
        public Guid PublicId { get; init; } = Guid.NewGuid();
        public int? FamilyId { get; set; }
        [EmailAddress]
        public required string Email { get; set; }
        [StringLength(128, MinimumLength = 2)]
        public required string Name { get; set; }
        [StringLength(128, MinimumLength = 2)]
        public required string DisplayName { get; set; }
        [Base64String]
        public string? ProfilePictureBase64 { get; set; }
        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? BanExpiresAt { get; set; }
        public bool IsEmailVerified { get; set; } = false;

        // Authentication fields
        public string? VerificationCode { get; set; }
        public DateTime? VerificationCodeExpiry { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }

        // Personal settings
        [EnumDataType(typeof(Currency))]
        public Currency DefaultCurrency { get; set; } = Currency.Huf;
        [EnumDataType(typeof(UserTimeZone))]
        public UserTimeZone DefaultTimeZone { get; set; } = UserTimeZone.CentralEuropeStandardTime;
        [EnumDataType(typeof(Language))]
        public Language DefaultLanguage { get; set; } = Language.Hungarian;
    }
}
