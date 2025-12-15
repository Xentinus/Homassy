using Asp.Versioning;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// User profile and settings management endpoints.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        /// <summary>
        /// Gets the current user's profile information.
        /// </summary>
        [HttpGet("profile")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status200OK)]
        public IActionResult GetProfile()
        {
            var profileResponse = new UserFunctions().GetProfileAsync();
            return Ok(ApiResponse<UserProfileResponse>.SuccessResponse(profileResponse));
        }

        /// <summary>
        /// Updates the current user's settings and preferences.
        /// </summary>
        [HttpPut("settings")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new UserFunctions().UpdateUserProfileAsync(request);
            return Ok(ApiResponse.SuccessResponse("Settings updated successfully"));
        }

        /// <summary>
        /// Uploads a new profile picture for the current user.
        /// </summary>
        [HttpPost("profile-picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadProfilePicture([FromBody] UploadProfilePictureRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new UserFunctions().UploadProfilePictureAsync(request.ProfilePictureBase64);
            return Ok(ApiResponse.SuccessResponse("Profile picture uploaded successfully"));
        }

        /// <summary>
        /// Deletes the current user's profile picture.
        /// </summary>
        [HttpDelete("profile-picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteProfilePicture()
        {
            await new UserFunctions().DeleteProfilePictureAsync();
            return Ok(ApiResponse.SuccessResponse("Profile picture deleted successfully"));
        }
    }
}