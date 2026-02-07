// DEPRECATED: This file is kept for backward compatibility only.
// JWT authentication is now handled by Ory Kratos session tokens.
// This file should be deleted in future cleanup.

namespace Homassy.API.Services
{
    [Obsolete("JWT authentication is now handled by Ory Kratos. This class will be removed.")]
    public static class JwtService
    {
        // All JWT functionality has been migrated to Ory Kratos session management.
        // See: Homassy.API/Security/KratosAuthenticationHandler.cs for the new auth flow.
    }
}