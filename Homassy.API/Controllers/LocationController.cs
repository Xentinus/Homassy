using Asp.Versioning;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Location;
using Microsoft.AspNetCore.Authorization;
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
            var shoppingLocations = new LocationFunctions().GetAllShoppingLocations();
            return Ok(ApiResponse<List<ShoppingLocationInfo>>.SuccessResponse(shoppingLocations));
        }

        [HttpPost("shopping")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateShoppingLocation([FromBody] ShoppingLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var shoppingLocationInfo = await new LocationFunctions().CreateShoppingLocationAsync(request);
            return Ok(ApiResponse<ShoppingLocationInfo>.SuccessResponse(shoppingLocationInfo, "Shopping location created successfully"));
        }

        [HttpPut("shopping/{shoppingLocationPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateShoppingLocation(Guid shoppingLocationPublicId, [FromBody] ShoppingLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var shoppingLocationInfo = await new LocationFunctions().UpdateShoppingLocationAsync(shoppingLocationPublicId, request);
            return Ok(ApiResponse<ShoppingLocationInfo>.SuccessResponse(shoppingLocationInfo, "Shopping location updated successfully"));
        }

        [HttpDelete("shopping/{shoppingLocationPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteShoppingLocation(Guid shoppingLocationPublicId)
        {
            await new LocationFunctions().DeleteShoppingLocationAsync(shoppingLocationPublicId);
            Log.Information("Shopping location {ShoppingLocationPublicId} deleted successfully", shoppingLocationPublicId);
            return Ok(ApiResponse.SuccessResponse("Shopping location deleted successfully"));
        }

        [HttpPost("shopping/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateMultipleShoppingLocations([FromBody] CreateMultipleShoppingLocationsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var shoppingLocationInfos = await new LocationFunctions().CreateMultipleShoppingLocationsAsync(request.Locations);
            return Ok(ApiResponse<List<ShoppingLocationInfo>>.SuccessResponse(shoppingLocationInfos, $"{shoppingLocationInfos.Count} shopping locations created successfully"));
        }

        [HttpDelete("shopping/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteMultipleShoppingLocations([FromBody] DeleteMultipleShoppingLocationsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new LocationFunctions().DeleteMultipleShoppingLocationsAsync(request);
            return Ok(ApiResponse.SuccessResponse($"{request.LocationPublicIds.Count} shopping locations deleted successfully"));
        }
        #endregion

        #region Storage Locations
        [HttpGet("storage")]
        [MapToApiVersion(1.0)]
        public IActionResult GetStorageLocations()
        {
            var storageLocations = new LocationFunctions().GetAllStorageLocations();
            return Ok(ApiResponse<List<StorageLocationInfo>>.SuccessResponse(storageLocations));
        }

        [HttpPost("storage")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateStorageLocation([FromBody] StorageLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var storageLocationInfo = await new LocationFunctions().CreateStorageLocationAsync(request);
            return Ok(ApiResponse<StorageLocationInfo>.SuccessResponse(storageLocationInfo, "Storage location created successfully"));
        }

        [HttpPut("storage/{storageLocationPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateStorageLocation(Guid storageLocationPublicId, [FromBody] StorageLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var storageLocationInfo = await new LocationFunctions().UpdateStorageLocationAsync(storageLocationPublicId, request);
            return Ok(ApiResponse<StorageLocationInfo>.SuccessResponse(storageLocationInfo, "Storage location updated successfully"));
        }

        [HttpDelete("storage/{storageLocationPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteStorageLocation(Guid storageLocationPublicId)
        {
            await new LocationFunctions().DeleteStorageLocationAsync(storageLocationPublicId);
            Log.Information("Storage location {StorageLocationPublicId} deleted successfully", storageLocationPublicId);
            return Ok(ApiResponse.SuccessResponse("Storage location deleted successfully"));
        }

        [HttpPost("storage/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateMultipleStorageLocations([FromBody] CreateMultipleStorageLocationsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var storageLocationInfos = await new LocationFunctions().CreateMultipleStorageLocationsAsync(request.Locations);
            return Ok(ApiResponse<List<StorageLocationInfo>>.SuccessResponse(storageLocationInfos, $"{storageLocationInfos.Count} storage locations created successfully"));
        }

        [HttpDelete("storage/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteMultipleStorageLocations([FromBody] DeleteMultipleStorageLocationsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new LocationFunctions().DeleteMultipleStorageLocationsAsync(request);
            return Ok(ApiResponse.SuccessResponse($"{request.LocationPublicIds.Count} storage locations deleted successfully"));
        }
        #endregion
    }
}
