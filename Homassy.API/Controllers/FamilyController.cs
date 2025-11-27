using Asp.Versioning;
using Homassy.API.Context;
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
        public async Task<IActionResult> GetFamily()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = await new UserFunctions().GetUserByIdAsync(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            if (!user.FamilyId.HasValue)
            {
                return BadRequest(ApiResponse.ErrorResponse("You are not a member of any family"));
            }

            var family = await new FamilyFunctions().GetFamilyByIdAsync(user.FamilyId.Value);
            if (family == null)
            {
                return NotFound(ApiResponse.ErrorResponse("Family not found"));
            }

            var response = new FamilyDetailsResponse
            {
                Name = family.Name,
                Description = family.Description,
                ShareCode = family.ShareCode,
                FamilyPictureBase64 = family.FamilyPictureBase64
            };

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

            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = await new UserFunctions().GetUserByIdAsync(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            if (!user.FamilyId.HasValue)
            {
                return BadRequest(ApiResponse.ErrorResponse("You are not a member of any family"));
            }

            var family = await new FamilyFunctions().GetFamilyByIdAsync(user.FamilyId.Value);
            if (family == null)
            {
                return NotFound(ApiResponse.ErrorResponse("Family not found"));
            }

            try
            {
                await new FamilyFunctions().UpdateFamilyAsync(family, request);
                Log.Information($"User {userId.Value} updated family {family.Id}");
                return Ok(ApiResponse.SuccessResponse("Family updated successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error updating family for user {userId.Value}: {ex.Message}");
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

            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = await new UserFunctions().GetUserByIdAsync(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            if (user.FamilyId.HasValue)
            {
                return BadRequest(ApiResponse.ErrorResponse("You are already a member of a family. Please leave your current family first."));
            }

            try
            {
                var family = await new FamilyFunctions().CreateFamilyAsync(request);
                await new FamilyFunctions().AddUserToFamilyAsync(user, family.Id);

                Log.Information($"User {userId.Value} created family {family.Id} with share code {family.ShareCode}");

                var response = new FamilyInfo
                {
                    Name = family.Name,
                    ShareCode = family.ShareCode
                };

                return Ok(ApiResponse<object>.SuccessResponse(response, "Family created successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error creating family for user {userId.Value}: {ex.Message}");
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

            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = await new UserFunctions().GetUserByIdAsync(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            if (user.FamilyId.HasValue)
            {
                return BadRequest(ApiResponse.ErrorResponse("You are already a member of a family. Please leave your current family first."));
            }

            var family = await new FamilyFunctions().GetFamilyByShareCodeAsync(request.ShareCode);
            if (family == null)
            {
                return NotFound(ApiResponse.ErrorResponse("Family not found with the provided share code"));
            }

            try
            {
                await new FamilyFunctions().AddUserToFamilyAsync(user, family.Id);
                Log.Information($"User {userId.Value} joined family {family.Id}");

                var response = new FamilyInfo
                {
                    Name = family.Name,
                    ShareCode = family.ShareCode
                };

                return Ok(ApiResponse<object>.SuccessResponse(response, "Successfully joined the family"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error joining family for user {userId.Value}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while joining the family"));
            }
        }

        [HttpPost("leave")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> LeaveFamily()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = await new UserFunctions().GetUserByIdAsync(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            if (!user.FamilyId.HasValue)
            {
                return BadRequest(ApiResponse.ErrorResponse("You are not a member of any family"));
            }

            try
            {
                var familyId = user.FamilyId.Value;
                await new FamilyFunctions().RemoveUserFromFamilyAsync(user);
                Log.Information($"User {userId.Value} left family {familyId}");

                return Ok(ApiResponse.SuccessResponse("Successfully left the family"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error leaving family for user {userId.Value}: {ex.Message}");
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

            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = await new UserFunctions().GetUserByIdAsync(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            if (!user.FamilyId.HasValue)
            {
                return BadRequest(ApiResponse.ErrorResponse("You are not a member of any family"));
            }

            var family = await new FamilyFunctions().GetFamilyByIdAsync(user.FamilyId.Value);
            if (family == null)
            {
                return NotFound(ApiResponse.ErrorResponse("Family not found"));
            }

            try
            {
                await new FamilyFunctions().UploadFamilyPictureAsync(family, request.FamilyPictureBase64);
                Log.Information($"User {userId.Value} uploaded picture for family {family.Id}");
                return Ok(ApiResponse.SuccessResponse("Family picture uploaded successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error uploading family picture for user {userId.Value}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while uploading the family picture"));
            }
        }

        [HttpDelete("picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteFamilyPicture()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = await new UserFunctions().GetUserByIdAsync(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            if (!user.FamilyId.HasValue)
            {
                return BadRequest(ApiResponse.ErrorResponse("You are not a member of any family"));
            }

            var family = await new FamilyFunctions().GetFamilyByIdAsync(user.FamilyId.Value);
            if (family == null)
            {
                return NotFound(ApiResponse.ErrorResponse("Family not found"));
            }

            if (string.IsNullOrEmpty(family.FamilyPictureBase64))
            {
                return BadRequest(ApiResponse.ErrorResponse("No family picture to delete"));
            }

            try
            {
                await new FamilyFunctions().DeleteFamilyPictureAsync(family);
                Log.Information($"User {userId.Value} deleted picture for family {family.Id}");
                return Ok(ApiResponse.SuccessResponse("Family picture deleted successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error deleting family picture for user {userId.Value}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the family picture"));
            }
        }
    }
}
