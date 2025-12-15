using Asp.Versioning;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
            var profileResponse = new UserFunctions().GetProfileAsync();
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

            await new UserFunctions().UpdateUserProfileAsync(request);
            return Ok(ApiResponse.SuccessResponse("Settings updated successfully"));
        }

        [HttpPost("profile-picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UploadProfilePicture([FromBody] UploadProfilePictureRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new UserFunctions().UploadProfilePictureAsync(request.ProfilePictureBase64);
            return Ok(ApiResponse.SuccessResponse("Profile picture uploaded successfully"));
        }

        [HttpDelete("profile-picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteProfilePicture()
        {
            await new UserFunctions().DeleteProfilePictureAsync();
            return Ok(ApiResponse.SuccessResponse("Profile picture deleted successfully"));
        }
    }
}