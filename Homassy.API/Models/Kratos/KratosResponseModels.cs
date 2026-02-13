namespace Homassy.API.Models.Kratos
{
    /// <summary>
    /// Session information returned to clients.
    /// </summary>
    public class KratosSessionInfo
    {
        /// <summary>
        /// The session ID.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// The identity ID.
        /// </summary>
        public string IdentityId { get; set; } = string.Empty;

        /// <summary>
        /// The user's email address.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// The user's name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// When the session expires.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// When the session was authenticated.
        /// </summary>
        public DateTime AuthenticatedAt { get; set; }

        /// <summary>
        /// The authentication method used.
        /// </summary>
        public string AuthenticationMethod { get; set; } = "unknown";
    }

    /// <summary>
    /// Kratos configuration URLs for frontend use.
    /// </summary>
    public class KratosConfig
    {
        /// <summary>
        /// The public Kratos URL.
        /// </summary>
        public string PublicUrl { get; set; } = string.Empty;

        /// <summary>
        /// Path to the login flow.
        /// </summary>
        public string LoginUrl { get; set; } = "/self-service/login/browser";

        /// <summary>
        /// Path to the registration flow.
        /// </summary>
        public string RegistrationUrl { get; set; } = "/self-service/registration/browser";

        /// <summary>
        /// Path to the logout flow.
        /// </summary>
        public string LogoutUrl { get; set; } = "/self-service/logout/browser";

        /// <summary>
        /// Path to the settings flow.
        /// </summary>
        public string SettingsUrl { get; set; } = "/self-service/settings/browser";

        /// <summary>
        /// Path to the recovery flow.
        /// </summary>
        public string RecoveryUrl { get; set; } = "/self-service/recovery/browser";

        /// <summary>
        /// Path to the verification flow.
        /// </summary>
        public string VerificationUrl { get; set; } = "/self-service/verification/browser";
    }
}
