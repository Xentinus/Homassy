namespace Homassy.API.Entities
{
    public class User : BaseEntity
    {
        public int? FamilyId { get; set; }
        public required string Email { get; set; }
        public required string Name { get; set; }
        public required string DisplayName { get; set; }
        public string? ProfilePictureBase64 { get; set; }
        public DateTime LastLoginAt { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Authentication fields
        public string? VerificationCode { get; set; }
        public DateTime? VerificationCodeExpiry { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }
    }
}
