using Asp.Versioning;
using Homassy.API.Context;
using Homassy.API.Exceptions;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class FamilyController : ControllerBase
    {
        [HttpGet]
        [MapToApiVersion(1.0)]
        public IActionResult GetFamily()
        {
            try
            {
                var response = new FamilyFunctions().GetFamilyAsync();
                return Ok(ApiResponse<FamilyDetailsResponse>.SuccessResponse(response));
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (FamilyNotFoundException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error getting family: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving family information"));
            }
        }

        [HttpPut]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateFamily([FromBody] UpdateFamilyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                await new FamilyFunctions().UpdateFamilyAsync(request);
                return Ok(ApiResponse.SuccessResponse("Family updated successfully"));
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
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
                Log.Error($"Unexpected error updating family: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while updating the family"));
            }
        }

        [HttpPost("create")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateFamily([FromBody] CreateFamilyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var response = await new FamilyFunctions().CreateFamilyAsync(request);
                return Ok(ApiResponse<FamilyInfo>.SuccessResponse(response, "Family created successfully"));
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
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
                Log.Error($"Unexpected error creating family: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while creating the family"));
            }
        }

        [HttpPost("join")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> JoinFamily([FromBody] JoinFamilyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var response = await new UserFunctions().JoinFamilyAsync(request);
                return Ok(ApiResponse<FamilyInfo>.SuccessResponse(response, "Successfully joined the family"));
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (FamilyNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error joining family: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while joining the family"));
            }
        }

        [HttpPost("leave")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> LeaveFamily()
        {
            try
            {
                await new UserFunctions().RemoveUserFromFamilyAsync();
                return Ok(ApiResponse.SuccessResponse("Successfully left the family"));
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
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
                Log.Error($"Unexpected error leaving family: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while leaving the family"));
            }
        }

        [HttpPost("picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UploadFamilyPicture([FromBody] UploadFamilyPictureRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                await new FamilyFunctions().UploadFamilyPictureAsync(request.FamilyPictureBase64);
                return Ok(ApiResponse.SuccessResponse("Family picture uploaded successfully"));
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (FamilyNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error uploading family picture: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while uploading the family picture"));
            }
        }

        [HttpDelete("picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteFamilyPicture()
        {
            try
            {
                await new FamilyFunctions().DeleteFamilyPictureAsync();
                return Ok(ApiResponse.SuccessResponse("Family picture deleted successfully"));
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (FamilyNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error deleting family picture: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the family picture"));
            }
        }
    }
}
