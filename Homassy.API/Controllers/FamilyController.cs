using Asp.Versioning;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Family;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
            var response = new FamilyFunctions().GetFamilyAsync();
            return Ok(ApiResponse<FamilyDetailsResponse>.SuccessResponse(response));
        }

        [HttpPut]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateFamily([FromBody] UpdateFamilyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new FamilyFunctions().UpdateFamilyAsync(request);
            return Ok(ApiResponse.SuccessResponse("Family updated successfully"));
        }

        [HttpPost("create")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateFamily([FromBody] CreateFamilyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var response = await new FamilyFunctions().CreateFamilyAsync(request);
            return Ok(ApiResponse<FamilyInfo>.SuccessResponse(response, "Family created successfully"));
        }

        [HttpPost("join")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> JoinFamily([FromBody] JoinFamilyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var response = await new UserFunctions().JoinFamilyAsync(request);
            return Ok(ApiResponse<FamilyInfo>.SuccessResponse(response, "Successfully joined the family"));
        }

        [HttpPost("leave")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> LeaveFamily()
        {
            await new UserFunctions().RemoveUserFromFamilyAsync();
            return Ok(ApiResponse.SuccessResponse("Successfully left the family"));
        }

        [HttpPost("picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UploadFamilyPicture([FromBody] UploadFamilyPictureRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new FamilyFunctions().UploadFamilyPictureAsync(request.FamilyPictureBase64);
            return Ok(ApiResponse.SuccessResponse("Family picture uploaded successfully"));
        }

        [HttpDelete("picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteFamilyPicture()
        {
            await new FamilyFunctions().DeleteFamilyPictureAsync();
            return Ok(ApiResponse.SuccessResponse("Family picture deleted successfully"));
        }
    }
}
