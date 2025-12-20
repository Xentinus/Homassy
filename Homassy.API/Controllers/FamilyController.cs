using Asp.Versioning;
using Homassy.API.Enums;
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
        public async Task<IActionResult> UpdateFamily([FromBody] UpdateFamilyRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            await new FamilyFunctions().UpdateFamilyAsync(request, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Creates a new family for the current user.
        /// </summary>
        [HttpPost("create")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<FamilyInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateFamily([FromBody] CreateFamilyRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var response = await new FamilyFunctions().CreateFamilyAsync(request, cancellationToken);
            return Ok(ApiResponse<FamilyInfo>.SuccessResponse(response));
        }

        /// <summary>
        /// Joins an existing family using an invite code.
        /// </summary>
        [HttpPost("join")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<FamilyInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> JoinFamily([FromBody] JoinFamilyRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var response = await new UserFunctions().JoinFamilyAsync(request, cancellationToken);
            return Ok(ApiResponse<FamilyInfo>.SuccessResponse(response));
        }

        /// <summary>
        /// Leaves the current family.
        /// </summary>
        [HttpPost("leave")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> LeaveFamily(CancellationToken cancellationToken)
        {
            await new UserFunctions().RemoveUserFromFamilyAsync(cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Uploads a new family picture.
        /// </summary>
        [HttpPost("picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadFamilyPicture([FromBody] UploadFamilyPictureRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            await new FamilyFunctions().UploadFamilyPictureAsync(request.FamilyPictureBase64, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Deletes the family picture.
        /// </summary>
        [HttpDelete("picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteFamilyPicture(CancellationToken cancellationToken)
        {
            await new FamilyFunctions().DeleteFamilyPictureAsync(cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }
    }
}
