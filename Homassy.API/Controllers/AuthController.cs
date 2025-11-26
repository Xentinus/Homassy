using Asp.Versioning;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.Models.Auth;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
using Homassy.API.Security;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid email address"));
            }

            var email = request.Email.ToLowerInvariant().Trim();
            var clientIp = HttpContext.GetClientIpAddress();
            var rateLimitKey = $"request-code:{email}:{clientIp}";

            var maxAttempts = int.Parse(ConfigService.GetValue("RateLimiting:RequestCodeMaxAttempts"));
            var windowMinutes = int.Parse(ConfigService.GetValue("RateLimiting:RequestCodeWindowMinutes"));
            var window = TimeSpan.FromMinutes(windowMinutes);

            if (RateLimitService.IsRateLimited(rateLimitKey, maxAttempts, window))
            {
                var remaining = RateLimitService.GetLockoutRemaining(rateLimitKey, window);
                Console.WriteLine($"[WARNING] Rate limit exceeded for email {email} from IP {clientIp}");

                return StatusCode(429, ApiResponse.ErrorResponse(
                    $"Too many attempts. Please try again in {remaining?.TotalMinutes:F0} minutes."));
            }

            var genericMessage = "If this email is registered, a verification code will be sent.";

            try
            {
                var user = await new UserFunctions().GetUserByEmailAsync(email);

                if (user == null)
                {
                    Console.WriteLine($"[INFO] User not found for {email}");
                    await Task.Delay(Random.Shared.Next(100, 300));
                    return Ok(ApiResponse.SuccessResponse(genericMessage));
                }

                var code = EmailService.GenerateVerificationCode();
                var expirationMinutes = int.Parse(ConfigService.GetValue("EmailVerification:CodeExpirationMinutes"));
                var expiry = DateTime.UtcNow.AddMinutes(expirationMinutes);

                await new UserFunctions().SetVerificationCodeAsync(user, code, expiry);
                await EmailService.SendVerificationCodeAsync(user.Email, code, user.DefaultTimeZone);

                Console.WriteLine($"[INFO] Verification code sent to {email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error processing request-code for {email}: {ex.Message}");
            }

            await Task.Delay(Random.Shared.Next(100, 300));
            return Ok(ApiResponse.SuccessResponse(genericMessage));
        }

        [HttpPost("verify-code")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.VerificationCode))
            {
                return BadRequest(ApiResponse.ErrorResponse("Email and code are required"));
            }

            var email = request.Email.ToLowerInvariant().Trim();
            var code = request.VerificationCode.Trim();
            var clientIp = HttpContext.GetClientIpAddress();
            var rateLimitKey = $"verify-code:{email}:{clientIp}";

            var maxAttempts = int.Parse(ConfigService.GetValue("RateLimiting:VerifyCodeMaxAttempts"));
            var windowMinutes = int.Parse(ConfigService.GetValue("RateLimiting:VerifyCodeWindowMinutes"));
            var window = TimeSpan.FromMinutes(windowMinutes);

            if (RateLimitService.IsRateLimited(rateLimitKey, maxAttempts, window))
            {
                var remaining = RateLimitService.GetLockoutRemaining(rateLimitKey, window);
                Console.WriteLine($"[WARNING] Verify code rate limit exceeded for {email} from IP {clientIp}");

                return StatusCode(429, ApiResponse.ErrorResponse(
                    $"Too many failed attempts. Account locked for {remaining?.TotalMinutes:F0} minutes."));
            }

            var user = await new UserFunctions().GetUserByEmailAsync(email);

            if (user == null)
            {
                await Task.Delay(Random.Shared.Next(200, 400));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid email or code"));
            }

            if (user.VerificationCodeExpiry == null || user.VerificationCodeExpiry < DateTime.UtcNow)
            {
                Console.WriteLine($"[WARNING] Expired verification code for {email}");
                await Task.Delay(Random.Shared.Next(200, 400));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid or expired code"));
            }

            if (!SecureCompare.ConstantTimeEquals(user.VerificationCode, code))
            {
                Console.WriteLine($"[WARNING] Invalid verification code for {email} from IP {clientIp}");
                await Task.Delay(Random.Shared.Next(200, 400));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid or expired code"));
            }

            RateLimitService.ResetAttempts(rateLimitKey);

            var accessToken = JwtService.GenerateAccessToken(user.Id, user.Email, user.FamilyId, user.DefaultCurrency);
            var refreshToken = JwtService.GenerateRefreshToken();
            var accessTokenExpiry = JwtService.GetAccessTokenExpiration();
            var refreshTokenExpiry = JwtService.GetRefreshTokenExpiration();

            await new UserFunctions().CompleteAuthenticationAsync(user, refreshToken, refreshTokenExpiry);

            Console.WriteLine($"[INFO] User {email} successfully authenticated from IP {clientIp}");

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
            if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(ApiResponse.ErrorResponse("Tokens are required"));
            }

            var clientIp = HttpContext.GetClientIpAddress();
            var rateLimitKey = $"refresh-token:{clientIp}";

            var maxAttempts = int.Parse(ConfigService.GetValue("RateLimiting:RefreshTokenMaxAttempts"));
            var windowMinutes = int.Parse(ConfigService.GetValue("RateLimiting:RefreshTokenWindowMinutes"));
            var window = TimeSpan.FromMinutes(windowMinutes);

            if (RateLimitService.IsRateLimited(rateLimitKey, maxAttempts, window))
            {
                Console.WriteLine($"[WARNING] Refresh token rate limit exceeded from IP {clientIp}");
                return StatusCode(429, ApiResponse.ErrorResponse("Too many refresh attempts"));
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
                Console.WriteLine($"[WARNING] Invalid refresh token for user {userId} from IP {clientIp}");
                await Task.Delay(Random.Shared.Next(100, 200));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid refresh token"));
            }

            if (user.RefreshTokenExpiry == null || user.RefreshTokenExpiry < DateTime.UtcNow)
            {
                Console.WriteLine($"[WARNING] Expired refresh token for user {userId}");
                await Task.Delay(Random.Shared.Next(100, 200));
                return Unauthorized(ApiResponse.ErrorResponse("Expired refresh token"));
            }

            RateLimitService.ResetAttempts(rateLimitKey);

            var newAccessToken = JwtService.GenerateAccessToken(user.Id, user.Email, user.FamilyId, user.DefaultCurrency);
            var newRefreshToken = JwtService.GenerateRefreshToken();
            var accessTokenExpiry = JwtService.GetAccessTokenExpiration();
            var refreshTokenExpiry = JwtService.GetRefreshTokenExpiration();

            await new UserFunctions().SetRefreshTokenAsync(user, newRefreshToken, refreshTokenExpiry);

            Console.WriteLine($"[INFO] Token refreshed for user {userId} from IP {clientIp}");

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

            Console.WriteLine($"[INFO] User {userId} logged out");

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
    }
}