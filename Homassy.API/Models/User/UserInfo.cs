namespace Homassy.API.Models.User
{
    public record UserInfo
    {
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? ProfilePictureBase64 { get; init; }
        public string TimeZone { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string Currency { get; init; } = string.Empty;
    }
}
