using System.Text.Json.Serialization;

namespace Homassy.API.Models.Kratos
{
    /// <summary>
    /// Represents a Kratos session returned from whoami or session endpoints.
    /// </summary>
    public class KratosSession
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("authenticated_at")]
        public DateTime? AuthenticatedAt { get; set; }

        [JsonPropertyName("authenticator_assurance_level")]
        public string? AuthenticatorAssuranceLevel { get; set; }

        [JsonPropertyName("authentication_methods")]
        public List<KratosAuthenticationMethod>? AuthenticationMethods { get; set; }

        [JsonPropertyName("issued_at")]
        public DateTime? IssuedAt { get; set; }

        [JsonPropertyName("identity")]
        public KratosIdentity Identity { get; set; } = new();

        [JsonPropertyName("devices")]
        public List<KratosSessionDevice>? Devices { get; set; }
    }

    /// <summary>
    /// Authentication method used in a Kratos session.
    /// </summary>
    public class KratosAuthenticationMethod
    {
        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("aal")]
        public string? Aal { get; set; }

        [JsonPropertyName("completed_at")]
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Device information for a Kratos session.
    /// </summary>
    public class KratosSessionDevice
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("ip_address")]
        public string? IpAddress { get; set; }

        [JsonPropertyName("user_agent")]
        public string? UserAgent { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }
    }
}
