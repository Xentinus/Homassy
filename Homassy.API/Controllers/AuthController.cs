using Asp.Versioning;
using Homassy.API.Functions;
using Homassy.API.Models.Auth;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
using Homassy.API.Security;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid email address"));
            }

            var email = request.Email.ToLowerInvariant().Trim();
            var genericMessage = "If this email is registered, a verification code will be sent.";

            try
            {
                var user = await new UserFunctions().GetUserByEmailAsync(email);

                if (user == null)
                {
                    Log.Information($"User not found for {email}");
                    await Task.Delay(Random.Shared.Next(100, 300));
                    return Ok(ApiResponse.SuccessResponse(genericMessage));
                }

                var code = EmailService.GenerateVerificationCode();
                var expirationMinutes = int.Parse(ConfigService.GetValue("EmailVerification:CodeExpirationMinutes"));
                var expiry = DateTime.UtcNow.AddMinutes(expirationMinutes);

                await new UserFunctions().SetVerificationCodeAsync(user, code, expiry);
                await EmailService.SendVerificationCodeAsync(user.Email, code, user.DefaultTimeZone);

                Log.Information($"Verification code sent to {email}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error processing request-code for {email}: {ex.Message}");
            }

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

            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.VerificationCode))
            {
                return BadRequest(ApiResponse.ErrorResponse("Email and code are required"));
            }

            var email = request.Email.ToLowerInvariant().Trim();
            var code = request.VerificationCode.Trim();

            var user = await new UserFunctions().GetUserByEmailAsync(email);

            if (user == null)
            {
                await Task.Delay(Random.Shared.Next(200, 400));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid email or code"));
            }

            if (user.VerificationCodeExpiry == null || user.VerificationCodeExpiry < DateTime.UtcNow)
            {
                Log.Warning($"Expired verification code for {email}");
                await Task.Delay(Random.Shared.Next(200, 400));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid or expired code"));
            }

            if (!SecureCompare.ConstantTimeEquals(user.VerificationCode, code))
            {
                Log.Warning($"Invalid verification code for {email}");
                await Task.Delay(Random.Shared.Next(200, 400));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid or expired code"));
            }

            var accessToken = JwtService.GenerateAccessToken(user.Id, user.Email, user.FamilyId, user.DefaultCurrency);
            var refreshToken = JwtService.GenerateRefreshToken();
            var accessTokenExpiry = JwtService.GetAccessTokenExpiration();
            var refreshTokenExpiry = JwtService.GetRefreshTokenExpiration();

            await new UserFunctions().CompleteAuthenticationAsync(user, refreshToken, refreshTokenExpiry);

            Log.Information($"User {email} successfully authenticated");

            var authResponse = new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessTokenExpiry,
                RefreshTokenExpiresAt = refreshTokenExpiry,
                User = new UserInfo
                {
                    Email = user.Email,
                    Name = user.Name,
                    DisplayName = user.DisplayName,
                    ProfilePictureBase64 = user.ProfilePictureBase64
                }
            };

            return Ok(ApiResponse<AuthResponse>.SuccessResponse(authResponse, "Login successful"));
        }

        [HttpPost("refresh")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(ApiResponse.ErrorResponse("Tokens are required"));
            }

            var principal = JwtService.GetPrincipalFromExpiredToken(request.AccessToken);
            if (principal == null)
            {
                await Task.Delay(Random.Shared.Next(100, 200));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid access token"));
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            {
                await Task.Delay(Random.Shared.Next(100, 200));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid token claims"));
            }

            var user = await new UserFunctions().GetUserByIdAsync(userId);
            if (user == null || user.IsDeleted)
            {
                await Task.Delay(Random.Shared.Next(100, 200));
                return Unauthorized(ApiResponse.ErrorResponse("User not found"));
            }

            if (!SecureCompare.ConstantTimeEquals(user.RefreshToken, request.RefreshToken))
            {
                Log.Warning($"Invalid refresh token for user {userId}");
                await Task.Delay(Random.Shared.Next(100, 200));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid refresh token"));
            }

            if (user.RefreshTokenExpiry == null || user.RefreshTokenExpiry < DateTime.UtcNow)
            {
                Log.Warning($"Expired refresh token for user {userId}");
                await Task.Delay(Random.Shared.Next(100, 200));
                return Unauthorized(ApiResponse.ErrorResponse("Expired refresh token"));
            }

            var newAccessToken = JwtService.GenerateAccessToken(user.Id, user.Email, user.FamilyId, user.DefaultCurrency);
            var newRefreshToken = JwtService.GenerateRefreshToken();
            var accessTokenExpiry = JwtService.GetAccessTokenExpiration();
            var refreshTokenExpiry = JwtService.GetRefreshTokenExpiration();

            await new UserFunctions().SetRefreshTokenAsync(user, newRefreshToken, refreshTokenExpiry);

            Log.Information($"Token refreshed for user {userId}");

            var refreshResponse = new
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                AccessTokenExpiresAt = accessTokenExpiry,
                RefreshTokenExpiresAt = refreshTokenExpiry
            };

            return Ok(ApiResponse<object>.SuccessResponse(refreshResponse, "Token refreshed successfully"));
        }

        [Authorize]
        [HttpPost("logout")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = await new UserFunctions().GetUserByIdAsync(userId);
            if (user != null)
            {
                await new UserFunctions().ClearRefreshTokenAsync(user);
            }

            Log.Information($"User {userId} logged out");

            return Ok(ApiResponse.SuccessResponse("Logged out successfully"));
        }

        [Authorize]
        [HttpGet("me")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = await new UserFunctions().GetUserByIdAsync(userId);
            if (user == null || user.IsDeleted)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            var userInfo = new UserInfo
            {
                Email = user.Email,
                Name = user.Name,
                DisplayName = user.DisplayName,
                ProfilePictureBase64 = user.ProfilePictureBase64
            };

            return Ok(ApiResponse<UserInfo>.SuccessResponse(userInfo));
        }

        [HttpPost("register")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> Register([FromBody] CreateUserRequest request)
        {
            var registrationEnabledValue = ConfigService.GetValueOrDefault("RegistrationEnabled", "false");
            var registrationEnabled = !string.IsNullOrEmpty(registrationEnabledValue) && bool.Parse(registrationEnabledValue);

            if (!registrationEnabled)
            {
                Log.Warning("Registration attempt while registration is disabled");
                return StatusCode(403, ApiResponse.ErrorResponse("Registration is currently disabled"));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var email = request.Email.ToLowerInvariant().Trim();
            var genericMessage = "Registration request received. If the email is not already in use, a verification code will be sent.";

            try
            {
                var existingUser = await new UserFunctions().GetUserByEmailAsync(email);

                if (existingUser != null)
                {
                    Log.Information($"Registration attempt for existing email {email}");
                    await Task.Delay(Random.Shared.Next(100, 300));
                    return Ok(ApiResponse.SuccessResponse(genericMessage));
                }

                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(ApiResponse.ErrorResponse("Name is required"));
                }

                var user = await new UserFunctions().CreateUserAsync(request);

                var code = EmailService.GenerateVerificationCode();
                var expirationMinutes = int.Parse(ConfigService.GetValue("EmailVerification:CodeExpirationMinutes"));
                var expiry = DateTime.UtcNow.AddMinutes(expirationMinutes);

                await new UserFunctions().SetVerificationCodeAsync(user, code, expiry);
                await EmailService.SendVerificationCodeAsync(user.Email, code, user.DefaultTimeZone);

                Log.Information($"New user registered: {email}, verification code sent");
            }
            catch (Exception ex)
            {
                Log.Error($"Error during registration for {email}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred during registration"));
            }

            await Task.Delay(Random.Shared.Next(100, 300));
            return Ok(ApiResponse.SuccessResponse(genericMessage));
        }
    }
}