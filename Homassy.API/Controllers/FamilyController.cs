using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Extensions;
using Homassy.API.Models.Family;
using Homassy.API.Models.ImageUpload;
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
        private readonly FamilyFunctions _familyFunctions;
        private readonly FamilyJoinRequestFunctions _familyJoinRequestFunctions;
        private readonly UserFunctions _userFunctions;
        private readonly ImageFunctions _imageFunctions;

        public FamilyController(
            FamilyFunctions familyFunctions,
            FamilyJoinRequestFunctions familyJoinRequestFunctions,
            UserFunctions userFunctions,
            ImageFunctions imageFunctions)
        {
            _familyFunctions = familyFunctions;
            _familyJoinRequestFunctions = familyJoinRequestFunctions;
            _userFunctions = userFunctions;
            _imageFunctions = imageFunctions;
        }

        /// <summary>
        /// Gets the current user's family information including members.
        /// </summary>
        [HttpGet]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<FamilyDetailsResponse>), StatusCodes.Status200OK)]
        public IActionResult GetFamily()
        {
            var response = _familyFunctions.GetFamilyAsync();
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
            var response = _familyFunctions.GetFamilyMembersAsync();
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

            await _familyFunctions.UpdateFamilyAsync(request, cancellationToken);
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

            var response = await _familyFunctions.CreateFamilyAsync(request, cancellationToken);
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

            var response = await _familyJoinRequestFunctions.CreateJoinRequestAsync(request, cancellationToken);
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
            var response = _familyJoinRequestFunctions.GetMyJoinRequest();
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
            await _familyJoinRequestFunctions.CancelMyJoinRequestAsync(cancellationToken);
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
            var response = _familyJoinRequestFunctions.GetFamilyJoinRequests();
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
            await _familyJoinRequestFunctions.ApproveJoinRequestAsync(publicId, cancellationToken);
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
            await _familyJoinRequestFunctions.RejectJoinRequestAsync(publicId, cancellationToken);
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
            await _userFunctions.RemoveUserFromFamilyAsync(cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Serves the caller's family picture as image bytes.
        /// </summary>
        /// <remarks>
        /// Bytes, not an <c>ApiResponse</c> envelope, for the same reasons the avatar endpoint
        /// gives: a picture inside JSON cannot be cached by the browser or the service worker and
        /// is re-downloaded with every payload that mentions the family.
        /// <para>
        /// There is no id in the path: a user has one family, so the session already names which
        /// picture is being asked for, and being in that family is the whole access check. The
        /// <c>v</c> query parameter is not read - it is the stored picture's content hash, present
        /// so a changed picture is a changed URL.
        /// </para>
        /// </remarks>
        /// <param name="size">Which rendition: <c>thumb</c> (default) or <c>full</c>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFamilyPicture(
            [FromQuery] ImageVariant size = ImageVariant.Thumb,
            CancellationToken cancellationToken = default)
        {
            var image = await _imageFunctions.GetFamilyPictureAsync(size, this.AcceptsWebp(), cancellationToken);

            if (image == null)
            {
                return NotFound(ApiResponse.ErrorResponse(ErrorCodes.FamilyNoPicture));
            }

            return this.CacheableImage(image);
        }

        /// <summary>
        /// Uploads a new family picture, replacing whatever the family had.
        /// </summary>
        [HttpPost("picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<FamilyImageInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadFamilyPicture([FromBody] UploadFamilyPictureRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var imageInfo = await _imageFunctions.UploadFamilyPictureAsync(request, cancellationToken);
            return Ok(ApiResponse<FamilyImageInfo>.SuccessResponse(imageInfo));
        }

        /// <summary>
        /// Deletes the family picture.
        /// </summary>
        [HttpDelete("picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteFamilyPicture(CancellationToken cancellationToken)
        {
            await _imageFunctions.DeleteFamilyPictureAsync(cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }
    }
}
