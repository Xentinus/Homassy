using Asp.Versioning;
using Homassy.API.Exceptions;
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
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        [HttpPost("request-code")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> RequestVerificationCode([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var genericMessage = "If this email is registered, a verification code will be sent.";

            try
            {
                await new UserFunctions().RequestVerificationCodeAsync(request.Email);
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (AuthException ex)
            {
                Log.Warning("Auth error during request-code: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during request-code");
            }

            // Security: constant time response to prevent user enumeration
            await Task.Delay(Random.Shared.Next(100, 300));
            return Ok(ApiResponse.SuccessResponse(genericMessage));
        }

        [HttpPost("verify-code")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyLoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var authResponse = await new UserFunctions().VerifyCodeAsync(request.Email, request.VerificationCode);
            return Ok(ApiResponse<AuthResponse>.SuccessResponse(authResponse, "Login successful"));
        }

        [Authorize]
        [HttpPost("refresh")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var refreshResponse = await new UserFunctions().RefreshTokenAsync(request.RefreshToken);
            return Ok(ApiResponse<RefreshTokenResponse>.SuccessResponse(refreshResponse, "Token refreshed successfully"));
        }

        [Authorize]
        [HttpPost("logout")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await new UserFunctions().LogoutAsync();
            }
            catch (UserNotFoundException ex)
            {
                // User already logged out or deleted - still return success
                Log.Warning("User not found during logout: {Message}", ex.Message);
            }
            
            return Ok(ApiResponse.SuccessResponse("Logged out successfully"));
        }

        [Authorize]
        [HttpGet("me")]
        [MapToApiVersion(1.0)]
        public IActionResult GetCurrentUser()
        {
            var userInfo = new UserFunctions().GetCurrentUserAsync();
            return Ok(ApiResponse<UserInfo>.SuccessResponse(userInfo));
        }

        [HttpPost("register")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> Register([FromBody] CreateUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            // Check if registration is enabled
            var registrationEnabledValue = ConfigService.GetValueOrDefault("RegistrationEnabled", "false");
            var registrationEnabled = !string.IsNullOrEmpty(registrationEnabledValue) && bool.Parse(registrationEnabledValue);

            if (!registrationEnabled)
            {
                Log.Warning("Registration attempt while registration is disabled");
                return StatusCode(403, ApiResponse.ErrorResponse("Registration is currently disabled"));
            }

            var genericMessage = "Registration request received. If the email is not already in use, a verification code will be sent.";

            try
            {
                await new UserFunctions().RegisterAsync(request);
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (AuthException ex)
            {
                Log.Warning("Auth error during registration: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during registration");
            }

            // Security: constant time response to prevent user enumeration
            await Task.Delay(Random.Shared.Next(100, 300));
            return Ok(ApiResponse.SuccessResponse(genericMessage));
        }
    }
}