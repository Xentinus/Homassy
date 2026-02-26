namespace Homassy.Email.Models;

public sealed record SendEmailRequest(
    string To,
    string Type,        // "login_code" | "registration_code" | "verification_code" | "recovery_code"
    string Language,    // "hu" | "de" | "en"
    SendEmailParams Params
);

public sealed record SendEmailParams(
    string? Name,
    string? Code,
    int? ExpiresInMinutes
);
