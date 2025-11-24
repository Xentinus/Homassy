namespace Homassy.API.Models.User
{
    public record UserInfo
    {
        public string Email { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? ProfilePictureBase64 { get; init; }
    }
}
