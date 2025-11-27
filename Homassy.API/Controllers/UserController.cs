using Asp.Versioning;
using Homassy.API.Context;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Homassy.API.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Security.Claims;

namespace Homassy.API.Controllers
{
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        [HttpGet("profile")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> GetProfile()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            FamilyInfo? familyInfo = null;
            if (user.FamilyId.HasValue)
            {
                var family = new FamilyFunctions().GetFamilyById(user.FamilyId.Value);
                if (family != null)
                {
                    familyInfo = new FamilyInfo
                    {
                        Name = family.Name,
                        ShareCode = family.ShareCode
                    };
                }
            }

            var profileResponse = new UserProfileResponse
            {
                Email = user.Email,
                Name = user.Name,
                DisplayName = user.DisplayName,
                ProfilePictureBase64 = user.ProfilePictureBase64,
                TimeZone = user.DefaultTimeZone.ToTimeZoneId(),
                Language = user.DefaultLanguage.ToLanguageCode(),
                Currency = user.DefaultCurrency.ToCurrencyCode(),
                Family = familyInfo
            };

            return Ok(ApiResponse<UserProfileResponse>.SuccessResponse(profileResponse));
        }

        [HttpPut("settings")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
            {
                var existingUser = new UserFunctions().GetUserByEmailAddress(request.Email);
                if (existingUser != null && existingUser.Id != userId.Value)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Email address is already in use"));
                }
            }

            try
            {
                await new UserFunctions().UpdateUserSettingsAsync(user, request);
                Log.Information($"User {userId.Value} updated their settings");
                return Ok(ApiResponse.SuccessResponse("Settings updated successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error updating settings for user {userId.Value}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while updating settings"));
            }
        }

        [HttpPost("profile-picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UploadProfilePicture([FromBody] UploadProfilePictureRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            try
            {
                await new UserFunctions().UploadProfilePictureAsync(user, request.ProfilePictureBase64);
                Log.Information($"User {userId.Value} uploaded profile picture");
                return Ok(ApiResponse.SuccessResponse("Profile picture uploaded successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error uploading profile picture for user {userId.Value}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while uploading the profile picture"));
            }
        }

        [HttpDelete("profile-picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteProfilePicture()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            if (string.IsNullOrEmpty(user.ProfilePictureBase64))
            {
                return BadRequest(ApiResponse.ErrorResponse("No profile picture to delete"));
            }

            try
            {
                await new UserFunctions().DeleteProfilePictureAsync(user);
                Log.Information($"User {userId.Value} deleted profile picture");
                return Ok(ApiResponse.SuccessResponse("Profile picture deleted successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error deleting profile picture for user {userId.Value}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the profile picture"));
            }
        }
    }
}
