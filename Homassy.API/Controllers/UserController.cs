using Asp.Versioning;
using Homassy.API.Context;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Homassy.API.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

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
        public IActionResult GetProfile()
        {
            try
            {
                var profileResponse = new UserFunctions().GetProfileAsync();
                return Ok(ApiResponse<UserProfileResponse>.SuccessResponse(profileResponse));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error getting user profile: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving user profile"));
            }
        }

        [HttpPut("settings")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                await new UserFunctions().UpdateUserProfileAsync(request);
                return Ok(ApiResponse.SuccessResponse("Settings updated successfully"));
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error updating settings: {ex.Message}");
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

            try
            {
                await new UserFunctions().UploadProfilePictureAsync(request.ProfilePictureBase64);
                return Ok(ApiResponse.SuccessResponse("Profile picture uploaded successfully"));
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error uploading profile picture: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while uploading the profile picture"));
            }
        }

        [HttpDelete("profile-picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteProfilePicture()
        {
            try
            {
                await new UserFunctions().DeleteProfilePictureAsync();
                return Ok(ApiResponse.SuccessResponse("Profile picture deleted successfully"));
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error deleting profile picture: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the profile picture"));
            }
        }
    }
}