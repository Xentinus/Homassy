using Asp.Versioning;
using Homassy.API.Context;
using Homassy.API.Entities;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.Models.Auth;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
using Homassy.API.Security;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Homassy.API.Controllers
{
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly JwtService _jwtService;
        private readonly EmailService _emailService;
        private readonly RateLimitService _rateLimitService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            JwtService jwtService,
            EmailService emailService,
            RateLimitService rateLimitService,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _jwtService = jwtService;
            _emailService = emailService;
            _rateLimitService = rateLimitService;
            _configuration = configuration;
            _logger = logger;
        }

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

            // Rate limiting
            var maxAttempts = int.Parse(_configuration["RateLimiting:RequestCodeMaxAttempts"]!);
            var windowMinutes = int.Parse(_configuration["RateLimiting:RequestCodeWindowMinutes"]!);
            var window = TimeSpan.FromMinutes(windowMinutes);

            if (_rateLimitService.IsRateLimited(rateLimitKey, maxAttempts, window))
            {
                var remaining = _rateLimitService.GetLockoutRemaining(rateLimitKey, window);
                _logger.LogWarning("Rate limit exceeded for email {Email} from IP {IP}", email, clientIp);

                return StatusCode(429, ApiResponse.ErrorResponse(
                    $"Too many attempts. Please try again in {remaining?.TotalMinutes:F0} minutes."));
            }

            // Always respond with same message (prevent email enumeration)
            var genericMessage = "If this email is registered, a verification code will be sent.";

            try
            {
                var user = await new UserFunctions().GetUserByEmailAsync(email);

                if (user == null)
                {
                    _logger.LogInformation("User not found for {Email}", email);
                    await Task.Delay(Random.Shared.Next(100, 300));
                    return Ok(ApiResponse.SuccessResponse(genericMessage));
                }

                // Generate new verification code
                var code = _emailService.GenerateVerificationCode();
                var expirationMinutes = int.Parse(_configuration["EmailVerification:CodeExpirationMinutes"]!);
                var expiry = DateTime.UtcNow.AddMinutes(expirationMinutes);

                await new UserFunctions().SetVerificationCodeAsync(user, code, expiry);
                await _emailService.SendVerificationCodeAsync(user.Email, code, user.DefaultTimeZone);

                _logger.LogInformation("Verification code sent to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request-code for {Email}", email);
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

            // Rate limiting
            var maxAttempts = int.Parse(_configuration["RateLimiting:VerifyCodeMaxAttempts"]!);
            var windowMinutes = int.Parse(_configuration["RateLimiting:VerifyCodeWindowMinutes"]!);
            var window = TimeSpan.FromMinutes(windowMinutes);

            if (_rateLimitService.IsRateLimited(rateLimitKey, maxAttempts, window))
            {
                var remaining = _rateLimitService.GetLockoutRemaining(rateLimitKey, window);
                _logger.LogWarning("Verify code rate limit exceeded for {Email} from IP {IP}", email, clientIp);

                return StatusCode(429, ApiResponse.ErrorResponse(
                    $"Too many failed attempts. Account locked for {remaining?.TotalMinutes:F0} minutes."));
            }

            var user = await new UserFunctions().GetUserByEmailAsync(email);

            if (user == null)
            {
                await Task.Delay(Random.Shared.Next(200, 400));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid email or code"));
            }

            // Check expiration first (before comparing code)
            if (user.VerificationCodeExpiry == null ||
                user.VerificationCodeExpiry < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired verification code for {Email}", email);
                await Task.Delay(Random.Shared.Next(200, 400));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid or expired code"));
            }

            // Constant-time comparison to prevent timing attacks
            if (!SecureCompare.ConstantTimeEquals(user.VerificationCode, code))
            {
                _logger.LogWarning("Invalid verification code for {Email} from IP {IP}", email, clientIp);
                await Task.Delay(Random.Shared.Next(200, 400)); // Timing attack mitigation
                return Unauthorized(ApiResponse.ErrorResponse("Invalid or expired code"));
            }

            // Success - reset rate limit
            _rateLimitService.ResetAttempts(rateLimitKey);

            // Generate tokens
            var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Email, user.FamilyId, user.DefaultCurrency);
            var refreshToken = _jwtService.GenerateRefreshToken();
            var accessTokenExpiry = _jwtService.GetAccessTokenExpiration();
            var refreshTokenExpiry = _jwtService.GetRefreshTokenExpiration();

            // Update user
            await new UserFunctions().CompleteAuthenticationAsync(user, refreshToken, refreshTokenExpiry);

            _logger.LogInformation("User {Email} successfully authenticated from IP {IP}", email, clientIp);

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

            // Rate limiting
            var maxAttempts = int.Parse(_configuration["RateLimiting:RefreshTokenMaxAttempts"]!);
            var windowMinutes = int.Parse(_configuration["RateLimiting:RefreshTokenWindowMinutes"]!);
            var window = TimeSpan.FromMinutes(windowMinutes);

            if (_rateLimitService.IsRateLimited(rateLimitKey, maxAttempts, window))
            {
                _logger.LogWarning("Refresh token rate limit exceeded from IP {IP}", clientIp);
                return StatusCode(429, ApiResponse.ErrorResponse("Too many refresh attempts"));
            }

            var principal = _jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
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

            // Constant-time comparison
            if (!SecureCompare.ConstantTimeEquals(user.RefreshToken, request.RefreshToken))
            {
                _logger.LogWarning("Invalid refresh token for user {UserId} from IP {IP}", userId, clientIp);
                await Task.Delay(Random.Shared.Next(100, 200));
                return Unauthorized(ApiResponse.ErrorResponse("Invalid refresh token"));
            }

            if (user.RefreshTokenExpiry == null || user.RefreshTokenExpiry < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired refresh token for user {UserId}", userId);
                await Task.Delay(Random.Shared.Next(100, 200));
                return Unauthorized(ApiResponse.ErrorResponse("Expired refresh token"));
            }

            // Success - reset rate limit
            _rateLimitService.ResetAttempts(rateLimitKey);

            // Generate new tokens
            var newAccessToken = _jwtService.GenerateAccessToken(user.Id, user.Email, user.FamilyId, user.DefaultCurrency);
            var newRefreshToken = _jwtService.GenerateRefreshToken();
            var accessTokenExpiry = _jwtService.GetAccessTokenExpiration();
            var refreshTokenExpiry = _jwtService.GetRefreshTokenExpiration();

            await new UserFunctions().SetRefreshTokenAsync(user, newRefreshToken, refreshTokenExpiry);

            _logger.LogInformation("Token refreshed for user {UserId} from IP {IP}", userId, clientIp);

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

            _logger.LogInformation($"User {userId} logged out");

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