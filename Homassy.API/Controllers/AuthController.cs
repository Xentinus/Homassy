using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.Models.Auth;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Authentication and authorization endpoints.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        /// <summary>
        /// Requests a verification code to be sent to the user's email address.
        /// </summary>
        [HttpPost("request-code")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RequestVerificationCode([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            try
            {
                await new UserFunctions().RequestVerificationCodeAsync(request.Email, cancellationToken);
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.ErrorCode));
            }
            catch (AuthException ex)
            {
                Log.Warning($"Auth error during request-code: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unexpected error during request-code");
            }

            // Security: constant time response to prevent user enumeration
            await Task.Delay(Random.Shared.Next(100, 300), cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Verifies the email verification code and returns authentication tokens.
        /// </summary>
        [HttpPost("verify-code")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyLoginRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var authResponse = await new UserFunctions().VerifyCodeAsync(request.Email, request.VerificationCode, cancellationToken);
            return Ok(ApiResponse<AuthResponse>.SuccessResponse(authResponse));
        }

        /// <summary>
        /// Refreshes the access token using a valid refresh token.
        /// </summary>
        [Authorize]
        [HttpPost("refresh")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var refreshResponse = await new UserFunctions().RefreshTokenAsync(request.RefreshToken, cancellationToken);
            return Ok(ApiResponse<RefreshTokenResponse>.SuccessResponse(refreshResponse));
        }

        /// <summary>
        /// Logs out the current user and invalidates their refresh token.
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            try
            {
                await new UserFunctions().LogoutAsync(cancellationToken);
            }
            catch (UserNotFoundException ex)
            {
                // User already logged out or deleted - still return success
                Log.Warning($"User not found during logout: {ex.Message}");
            }
            
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Gets the current authenticated user's information.
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<UserInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
        public IActionResult GetCurrentUser()
        {
            var userInfo = new UserFunctions().GetCurrentUser();
            return Ok(ApiResponse<UserInfo>.SuccessResponse(userInfo));
        }

        /// <summary>
        /// Registers a new user account.
        /// </summary>
        [HttpPost("register")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Register([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            // Check if registration is enabled
            var registrationEnabledValue = ConfigService.GetValueOrDefault("RegistrationEnabled", "false");
            var registrationEnabled = !string.IsNullOrEmpty(registrationEnabledValue) && bool.Parse(registrationEnabledValue);

            if (!registrationEnabled)
            {
                Log.Warning("Registration attempt while registration is disabled");
                return StatusCode(403, ApiResponse.ErrorResponse(ErrorCodes.AuthRegistrationDisabled));
            }

            // Extract browser language from Accept-Language header
            var acceptLanguage = Request.Headers.AcceptLanguage.FirstOrDefault();
            var browserLanguage = ParseAcceptLanguage(acceptLanguage);

            // Extract timezone from X-Timezone header
            var timezoneHeader = Request.Headers["X-Timezone"].FirstOrDefault();
            var browserTimeZone = ParseTimeZone(timezoneHeader);

            try
            {
                await new UserFunctions().RegisterAsync(request, browserLanguage, browserTimeZone, cancellationToken);
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.ErrorCode));
            }
            catch (AuthException ex)
            {
                Log.Warning($"Auth error during registration: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unexpected error during registration");
            }

            // Security: constant time response to prevent user enumeration
            await Task.Delay(Random.Shared.Next(100, 300), cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Parses the Accept-Language header and returns the corresponding Language enum.
        /// </summary>
        private static Language ParseAcceptLanguage(string? acceptLanguage)
        {
            if (string.IsNullOrWhiteSpace(acceptLanguage))
            {
                return Language.English;
            }

            // Accept-Language format: "hu-HU,hu;q=0.9,en-US;q=0.8,en;q=0.7"
            // Take the first language code (highest priority)
            var primaryLanguage = acceptLanguage
                .Split(',')
                .FirstOrDefault()?
                .Split(';')
                .FirstOrDefault()?
                .Trim();

            if (string.IsNullOrWhiteSpace(primaryLanguage))
            {
                return Language.English;
            }

            var languageCode = primaryLanguage.Split('-').FirstOrDefault()?.ToLowerInvariant();

            return LanguageExtensions.FromLanguageCode(languageCode ?? "en");
        }

        /// <summary>
        /// Parses the X-Timezone header and returns the corresponding UserTimeZone enum.
        /// </summary>
        private static UserTimeZone ParseTimeZone(string? timezoneId)
        {
            if (string.IsNullOrWhiteSpace(timezoneId))
            {
                return UserTimeZone.CentralEuropeStandardTime;
            }

            return UserTimeZoneExtensions.FromTimeZoneId(timezoneId.Trim());
        }
    }
}