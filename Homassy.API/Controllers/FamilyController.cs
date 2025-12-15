using Asp.Versioning;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Family management endpoints.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class FamilyController : ControllerBase
    {
        /// <summary>
        /// Gets the current user's family information including members.
        /// </summary>
        [HttpGet]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<FamilyDetailsResponse>), StatusCodes.Status200OK)]
        public IActionResult GetFamily()
        {
            var response = new FamilyFunctions().GetFamilyAsync();
            return Ok(ApiResponse<FamilyDetailsResponse>.SuccessResponse(response));
        }

        /// <summary>
        /// Updates the current user's family information.
        /// </summary>
        [HttpPut]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateFamily([FromBody] UpdateFamilyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new FamilyFunctions().UpdateFamilyAsync(request);
            return Ok(ApiResponse.SuccessResponse("Family updated successfully"));
        }

        /// <summary>
        /// Creates a new family for the current user.
        /// </summary>
        [HttpPost("create")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<FamilyInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateFamily([FromBody] CreateFamilyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var response = await new FamilyFunctions().CreateFamilyAsync(request);
            return Ok(ApiResponse<FamilyInfo>.SuccessResponse(response, "Family created successfully"));
        }

        /// <summary>
        /// Joins an existing family using an invite code.
        /// </summary>
        [HttpPost("join")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<FamilyInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> JoinFamily([FromBody] JoinFamilyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var response = await new UserFunctions().JoinFamilyAsync(request);
            return Ok(ApiResponse<FamilyInfo>.SuccessResponse(response, "Successfully joined the family"));
        }

        /// <summary>
        /// Leaves the current family.
        /// </summary>
        [HttpPost("leave")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> LeaveFamily()
        {
            await new UserFunctions().RemoveUserFromFamilyAsync();
            return Ok(ApiResponse.SuccessResponse("Successfully left the family"));
        }

        /// <summary>
        /// Uploads a new family picture.
        /// </summary>
        [HttpPost("picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadFamilyPicture([FromBody] UploadFamilyPictureRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new FamilyFunctions().UploadFamilyPictureAsync(request.FamilyPictureBase64);
            return Ok(ApiResponse.SuccessResponse("Family picture uploaded successfully"));
        }

        /// <summary>
        /// Deletes the family picture.
        /// </summary>
        [HttpDelete("picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteFamilyPicture()
        {
            await new FamilyFunctions().DeleteFamilyPictureAsync();
            return Ok(ApiResponse.SuccessResponse("Family picture deleted successfully"));
        }
    }
}
