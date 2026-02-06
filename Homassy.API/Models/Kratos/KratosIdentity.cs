using System.Text.Json.Serialization;

namespace Homassy.API.Models.Kratos
{
    /// <summary>
    /// Represents a Kratos identity returned from the Admin API.
    /// </summary>
    public class KratosIdentity
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("schema_id")]
        public string SchemaId { get; set; } = string.Empty;

        [JsonPropertyName("schema_url")]
        public string? SchemaUrl { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("state_changed_at")]
        public DateTime? StateChangedAt { get; set; }

        [JsonPropertyName("traits")]
        public KratosTraits Traits { get; set; } = new();

        [JsonPropertyName("verifiable_addresses")]
        public List<KratosVerifiableAddress>? VerifiableAddresses { get; set; }

        [JsonPropertyName("recovery_addresses")]
        public List<KratosRecoveryAddress>? RecoveryAddresses { get; set; }

        [JsonPropertyName("metadata_public")]
        public Dictionary<string, object>? MetadataPublic { get; set; }

        [JsonPropertyName("metadata_admin")]
        public Dictionary<string, object>? MetadataAdmin { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// User traits stored in Kratos identity.
    /// Maps to Homassy.Kratos/identity.schema.json
    /// </summary>
    public class KratosTraits
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("profile_picture_base64")]
        public string? ProfilePictureBase64 { get; set; }

        [JsonPropertyName("date_of_birth")]
        public string? DateOfBirth { get; set; }

        [JsonPropertyName("gender")]
        public string? Gender { get; set; }

        [JsonPropertyName("default_currency")]
        public string DefaultCurrency { get; set; } = "HUF";

        [JsonPropertyName("default_timezone")]
        public string DefaultTimezone { get; set; } = "Europe/Budapest";

        [JsonPropertyName("default_language")]
        public string DefaultLanguage { get; set; } = "hu";

        [JsonPropertyName("family_id")]
        public int? FamilyId { get; set; }
    }

    /// <summary>
    /// Verifiable address (e.g., email) in Kratos.
    /// </summary>
    public class KratosVerifiableAddress
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("verified")]
        public bool Verified { get; set; }

        [JsonPropertyName("via")]
        public string Via { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("verified_at")]
        public DateTime? VerifiedAt { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Recovery address in Kratos.
    /// </summary>
    public class KratosRecoveryAddress
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("via")]
        public string Via { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
