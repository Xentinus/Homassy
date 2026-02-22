using Asp.Versioning;
using Homassy.API.Context;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.Middleware;
using Homassy.API.Models.Common;
using Homassy.API.Models.Kratos;
using Homassy.API.Models.User;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Authentication and authorization endpoints.
    /// Authentication is handled by Ory Kratos. This controller provides user session management.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IKratosService _kratosService;
        private readonly IConfiguration _configuration;

        public AuthController(IKratosService kratosService, IConfiguration configuration)
        {
            _kratosService = kratosService;
            _configuration = configuration;
        }

        /// <summary>
        /// Gets the current authenticated user's information.
        /// Requires a valid Kratos session.
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<UserInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
        {
            var kratosSession = HttpContext.GetKratosSession();
            
            if (kratosSession == null)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthInvalidCredentials));
            }

            // If registration is disabled, block creation of brand-new local users
            var registrationEnabled = _configuration.GetValue<bool>("RegistrationEnabled", true);
            if (!registrationEnabled)
            {
                var existingUser = new UserFunctions().GetUserByKratosIdentityId(kratosSession.Identity.Id)
                    ?? new UserFunctions().GetUserByEmailAddress(kratosSession.Identity.Traits.Email);

                if (existingUser == null)
                {
                    Log.Warning($"Registration is disabled. Blocking new user creation for Kratos identity {kratosSession.Identity.Id}");
                    return StatusCode(403, ApiResponse.ErrorResponse(ErrorCodes.AuthRegistrationDisabled));
                }
            }

            // Ensure local user record exists and is synced with Kratos
            var user = await EnsureLocalUserAsync(kratosSession, cancellationToken);
            
            if (user == null)
            {
                Log.Error($"Failed to create/sync local user for Kratos identity {kratosSession.Identity.Id}");
                return StatusCode(500, ApiResponse.ErrorResponse(ErrorCodes.SystemUnexpectedError));
            }

            var userInfo = new UserFunctions().GetUserInfoFromKratosSession(kratosSession, user);
            return Ok(ApiResponse<UserInfo>.SuccessResponse(userInfo));
        }

        /// <summary>
        /// Gets the current Kratos session information.
        /// </summary>
        [Authorize]
        [HttpGet("session")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<KratosSessionInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
        public IActionResult GetSession()
        {
            var kratosSession = HttpContext.GetKratosSession();
            
            if (kratosSession == null)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthInvalidCredentials));
            }

            var sessionInfo = new KratosSessionInfo
            {
                SessionId = kratosSession.Id,
                IdentityId = kratosSession.Identity.Id,
                Email = kratosSession.Identity.Traits.Email,
                Name = kratosSession.Identity.Traits.Name,
                ExpiresAt = kratosSession.ExpiresAt,
                AuthenticatedAt = kratosSession.AuthenticatedAt,
                AuthenticationMethod = kratosSession.AuthenticationMethods?.FirstOrDefault()?.Method ?? "unknown"
            };

            return Ok(ApiResponse<KratosSessionInfo>.SuccessResponse(sessionInfo));
        }

        /// <summary>
        /// Synchronizes the local user record with Kratos identity data.
        /// Call this after updating profile in Kratos.
        /// </summary>
        [Authorize]
        [HttpPost("sync")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<UserInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SyncUser(CancellationToken cancellationToken)
        {
            var kratosSession = HttpContext.GetKratosSession();
            
            if (kratosSession == null)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthInvalidCredentials));
            }

            // Force refresh the Kratos identity data
            var identity = await _kratosService.GetIdentityAsync(kratosSession.Identity.Id, cancellationToken);
            
            if (identity == null)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthInvalidCredentials));
            }

            // Update local user with latest Kratos data
            var user = await new UserFunctions().SyncUserFromKratosAsync(identity, cancellationToken);
            
            if (user == null)
            {
                return StatusCode(500, ApiResponse.ErrorResponse(ErrorCodes.SystemUnexpectedError));
            }

            var userInfo = new UserFunctions().GetUserInfoFromKratosSession(kratosSession, user);
            return Ok(ApiResponse<UserInfo>.SuccessResponse(userInfo));
        }

        /// <summary>
        /// Gets the Kratos configuration URLs for frontend use.
        /// Includes RegistrationEnabled flag so the frontend can conditionally render registration UI.
        /// </summary>
        [HttpGet("config")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<KratosConfig>), StatusCodes.Status200OK)]
        public IActionResult GetKratosConfig()
        {
            var config = new KratosConfig
            {
                PublicUrl = _configuration["Kratos:PublicUrl"] ?? "http://localhost:4433",
                LoginUrl = "/self-service/login/browser",
                RegistrationUrl = "/self-service/registration/browser",
                LogoutUrl = "/self-service/logout/browser",
                SettingsUrl = "/self-service/settings/browser",
                RecoveryUrl = "/self-service/recovery/browser",
                VerificationUrl = "/self-service/verification/browser",
                RegistrationEnabled = _configuration.GetValue<bool>("RegistrationEnabled", true)
            };

            return Ok(ApiResponse<KratosConfig>.SuccessResponse(config));
        }

        /// <summary>
        /// Ensures a local User record exists for the Kratos identity.
        /// Creates one if it doesn't exist.
        /// </summary>
        private async Task<User?> EnsureLocalUserAsync(KratosSession session, CancellationToken cancellationToken)
        {
            var user = new UserFunctions().GetUserByKratosIdentityId(session.Identity.Id);

            if (user != null)
            {
                // Update last login time
                await new UserFunctions().UpdateLastLoginAsync(user.Id, cancellationToken);
                return user;
            }

            // Check if user exists by email (migration case)
            user = new UserFunctions().GetUserByEmailAddress(session.Identity.Traits.Email);

            if (user != null)
            {
                // Link existing user to Kratos identity
                await new UserFunctions().LinkUserToKratosAsync(user.Id, session.Identity.Id, cancellationToken);
                await new UserFunctions().UpdateLastLoginAsync(user.Id, cancellationToken);
                return user;
            }

            // Create new local user record
            return await new UserFunctions().CreateUserFromKratosAsync(session.Identity, cancellationToken);
        }
    }

    /// <summary>
    /// Kratos session information for API response.
    /// </summary>
    public class KratosSessionInfo
    {
        public string SessionId { get; set; } = string.Empty;
        public string IdentityId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Name { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? AuthenticatedAt { get; set; }
        public string AuthenticationMethod { get; set; } = string.Empty;
    }

    /// <summary>
    /// Kratos configuration for frontend use.
    /// </summary>
    public class KratosConfig
    {
        public string PublicUrl { get; set; } = string.Empty;
        public string LoginUrl { get; set; } = string.Empty;
        public string RegistrationUrl { get; set; } = string.Empty;
        public string LogoutUrl { get; set; } = string.Empty;
        public string SettingsUrl { get; set; } = string.Empty;
        public string RecoveryUrl { get; set; } = string.Empty;
        public string VerificationUrl { get; set; } = string.Empty;
        public bool RegistrationEnabled { get; set; } = true;
    }
}