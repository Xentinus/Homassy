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
        /// Gets all members of the current user's family.
        /// </summary>
        [HttpGet("members")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<FamilyMemberResponse>>), StatusCodes.Status200OK)]
        public IActionResult GetFamilyMembers()
        {
            var response = new FamilyFunctions().GetFamilyMembersAsync();
            return Ok(ApiResponse<List<FamilyMemberResponse>>.SuccessResponse(response));
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
        /// Creates a request to join an existing family using its share code. Joining requires
        /// approval from an existing family member; the request stays pending until then.
        /// </summary>
        [HttpPost("join-requests")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<MyJoinRequestResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateJoinRequest([FromBody] JoinFamilyRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var response = await new FamilyJoinRequestFunctions().CreateJoinRequestAsync(request, cancellationToken);
            return Ok(ApiResponse<MyJoinRequestResponse>.SuccessResponse(response));
        }

        /// <summary>
        /// Gets the current user's pending join request, if any.
        /// </summary>
        [HttpGet("join-requests/mine")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<MyJoinRequestResponse>), StatusCodes.Status200OK)]
        public IActionResult GetMyJoinRequest()
        {
            var response = new FamilyJoinRequestFunctions().GetMyJoinRequest();
            return Ok(ApiResponse<MyJoinRequestResponse?>.SuccessResponse(response));
        }

        /// <summary>
        /// Withdraws the current user's pending join request.
        /// </summary>
        [HttpDelete("join-requests/mine")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CancelMyJoinRequest(CancellationToken cancellationToken)
        {
            await new FamilyJoinRequestFunctions().CancelMyJoinRequestAsync(cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Lists the pending join requests for the current user's family.
        /// </summary>
        [HttpGet("join-requests")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<FamilyJoinRequestResponse>>), StatusCodes.Status200OK)]
        public IActionResult GetFamilyJoinRequests()
        {
            var response = new FamilyJoinRequestFunctions().GetFamilyJoinRequests();
            return Ok(ApiResponse<List<FamilyJoinRequestResponse>>.SuccessResponse(response));
        }

        /// <summary>
        /// Approves a pending join request, adding the requester to the family.
        /// </summary>
        [HttpPost("join-requests/{publicId:guid}/approve")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ApproveJoinRequest(Guid publicId, CancellationToken cancellationToken)
        {
            await new FamilyJoinRequestFunctions().ApproveJoinRequestAsync(publicId, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Declines a pending join request.
        /// </summary>
        [HttpPost("join-requests/{publicId:guid}/reject")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RejectJoinRequest(Guid publicId, CancellationToken cancellationToken)
        {
            await new FamilyJoinRequestFunctions().RejectJoinRequestAsync(publicId, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
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
