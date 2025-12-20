using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.User;
using Homassy.API.Models.ImageUpload;
using Homassy.API.Services;
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
        private readonly IImageProcessingService _imageProcessingService;

        public UserController(IImageProcessingService imageProcessingService)
        {
            _imageProcessingService = imageProcessingService;
        }

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
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingsRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            await new UserFunctions().UpdateUserProfileAsync(request, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Uploads and processes a new profile picture for the current user.
        /// </summary>
        [HttpPost("profile-picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<UserProfileImageInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadProfilePicture([FromBody] UploadUserProfileImageRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var imageInfo = await new ImageFunctions(_imageProcessingService).UploadUserProfileImageAsync(request, cancellationToken);
            return Ok(ApiResponse<UserProfileImageInfo>.SuccessResponse(imageInfo));
        }

        /// <summary>
        /// Deletes the current user's profile picture.
        /// </summary>
        [HttpDelete("profile-picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteProfilePicture(CancellationToken cancellationToken)
        {
            await new ImageFunctions(_imageProcessingService).DeleteUserProfileImageAsync(cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }
    }
}