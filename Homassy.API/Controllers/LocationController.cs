using Asp.Versioning;
using Homassy.API.Exceptions;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Location;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class LocationController : ControllerBase
    {
        #region Shopping Locations
        [HttpGet("shopping")]
        [MapToApiVersion(1.0)]
        public IActionResult GetShoppingLocations()
        {
            try
            {
                var shoppingLocations = new LocationFunctions().GetAllShoppingLocations();
                return Ok(ApiResponse<List<ShoppingLocationInfo>>.SuccessResponse(shoppingLocations));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error retrieving shopping locations: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving shopping locations"));
            }
        }

        [HttpPost("shopping")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateShoppingLocation([FromBody] ShoppingLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var shoppingLocationInfo = await new LocationFunctions().CreateShoppingLocationAsync(request);
                return Ok(ApiResponse<ShoppingLocationInfo>.SuccessResponse(shoppingLocationInfo, "Shopping location created successfully"));
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
                Log.Error($"Error creating shopping location: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while creating the shopping location"));
            }
        }

        [HttpPut("shopping/{shoppingLocationPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateShoppingLocation(Guid shoppingLocationPublicId, [FromBody] ShoppingLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var shoppingLocationInfo = await new LocationFunctions().UpdateShoppingLocationAsync(shoppingLocationPublicId, request);
                return Ok(ApiResponse<ShoppingLocationInfo>.SuccessResponse(shoppingLocationInfo, "Shopping location updated successfully"));
            }
            catch (ShoppingLocationNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error updating shopping location {shoppingLocationPublicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while updating the shopping location"));
            }
        }

        [HttpDelete("shopping/{shoppingLocationPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteShoppingLocation(Guid shoppingLocationPublicId)
        {
            try
            {
                await new LocationFunctions().DeleteShoppingLocationAsync(shoppingLocationPublicId);
                Log.Information($"Shopping location {shoppingLocationPublicId} deleted successfully");
                return Ok(ApiResponse.SuccessResponse("Shopping location deleted successfully"));
            }
            catch (ShoppingLocationNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error deleting shopping location {shoppingLocationPublicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the shopping location"));
            }
        }
        #endregion

        #region Storage Locations
        [HttpGet("storage")]
        [MapToApiVersion(1.0)]
        public IActionResult GetStorageLocations()
        {
            try
            {
                var storageLocations = new LocationFunctions().GetAllStorageLocations();
                return Ok(ApiResponse<List<StorageLocationInfo>>.SuccessResponse(storageLocations));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error retrieving storage locations: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving storage locations"));
            }
        }

        [HttpPost("storage")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateStorageLocation([FromBody] StorageLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var storageLocationInfo = await new LocationFunctions().CreateStorageLocationAsync(request);
                return Ok(ApiResponse<StorageLocationInfo>.SuccessResponse(storageLocationInfo, "Storage location created successfully"));
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
                Log.Error($"Error creating storage location: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while creating the storage location"));
            }
        }

        [HttpPut("storage/{storageLocationPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateStorageLocation(Guid storageLocationPublicId, [FromBody] StorageLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var storageLocationInfo = await new LocationFunctions().UpdateStorageLocationAsync(storageLocationPublicId, request);
                return Ok(ApiResponse<StorageLocationInfo>.SuccessResponse(storageLocationInfo, "Storage location updated successfully"));
            }
            catch (StorageLocationNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error updating storage location {storageLocationPublicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while updating the storage location"));
            }
        }

        [HttpDelete("storage/{storageLocationPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteStorageLocation(Guid storageLocationPublicId)
        {
            try
            {
                await new LocationFunctions().DeleteStorageLocationAsync(storageLocationPublicId);
                Log.Information($"Storage location {storageLocationPublicId} deleted successfully");
                return Ok(ApiResponse.SuccessResponse("Storage location deleted successfully"));
            }
            catch (StorageLocationNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UnauthorizedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error deleting storage location {storageLocationPublicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the storage location"));
            }
        }
        #endregion
    }
}
