using System.Text.Json.Serialization;

namespace Homassy.Email.Models;

public sealed record KratosTemplateData(
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("recovery_code")] string? RecoveryCode,
    [property: JsonPropertyName("verification_code")] string? VerificationCode,
    [property: JsonPropertyName("login_code")] string? LoginCode,
    [property: JsonPropertyName("registration_code")] string? RegistrationCode,
    [property: JsonPropertyName("identity")] KratosIdentity? Identity
);

public sealed record KratosIdentity(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("traits")] KratosIdentityTraits Traits
);

public sealed record KratosIdentityTraits(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("default_language")] string? DefaultLanguage   // "hu" | "de" | "en"
);
