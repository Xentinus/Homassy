namespace Homassy.API.Models.Auth
{
    public record RefreshTokenResponse
    {
        public required string AccessToken { get; init; }
        public required string RefreshToken { get; init; }
        public required DateTime AccessTokenExpiresAt { get; init; }
        public required DateTime RefreshTokenExpiresAt { get; init; }
    }
}
