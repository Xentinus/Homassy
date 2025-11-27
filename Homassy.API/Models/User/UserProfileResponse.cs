using Homassy.API.Models.Family;

namespace Homassy.API.Models.User
{
    public class UserProfileResponse
    {
        public string Email { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? ProfilePictureBase64 { get; init; }
        public string TimeZone { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string Currency { get; init; } = string.Empty;
        public FamilyInfo? Family { get; init; }
    }
}
