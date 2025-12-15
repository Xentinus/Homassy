using Asp.Versioning;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Location;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Location management endpoints for shopping and storage locations.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class LocationController : ControllerBase
    {
        #region Shopping Locations
        /// <summary>
        /// Gets all shopping locations for the current user's family.
        /// </summary>
        [HttpGet("shopping")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<ShoppingLocationInfo>>), StatusCodes.Status200OK)]
        public IActionResult GetShoppingLocations()
        {
            var shoppingLocations = new LocationFunctions().GetAllShoppingLocations();
            return Ok(ApiResponse<List<ShoppingLocationInfo>>.SuccessResponse(shoppingLocations));
        }

        /// <summary>
        /// Creates a new shopping location.
        /// </summary>
        [HttpPost("shopping")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ShoppingLocationInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateShoppingLocation([FromBody] ShoppingLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var shoppingLocationInfo = await new LocationFunctions().CreateShoppingLocationAsync(request);
            return Ok(ApiResponse<ShoppingLocationInfo>.SuccessResponse(shoppingLocationInfo, "Shopping location created successfully"));
        }

        /// <summary>
        /// Updates an existing shopping location.
        /// </summary>
        [HttpPut("shopping/{shoppingLocationPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ShoppingLocationInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateShoppingLocation(Guid shoppingLocationPublicId, [FromBody] ShoppingLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var shoppingLocationInfo = await new LocationFunctions().UpdateShoppingLocationAsync(shoppingLocationPublicId, request);
            return Ok(ApiResponse<ShoppingLocationInfo>.SuccessResponse(shoppingLocationInfo, "Shopping location updated successfully"));
        }

        /// <summary>
        /// Deletes a shopping location.
        /// </summary>
        [HttpDelete("shopping/{shoppingLocationPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteShoppingLocation(Guid shoppingLocationPublicId)
        {
            await new LocationFunctions().DeleteShoppingLocationAsync(shoppingLocationPublicId);
            Log.Information("Shopping location {ShoppingLocationPublicId} deleted successfully", shoppingLocationPublicId);
            return Ok(ApiResponse.SuccessResponse("Shopping location deleted successfully"));
        }

        /// <summary>
        /// Creates multiple shopping locations in a single request.
        /// </summary>
        [HttpPost("shopping/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<ShoppingLocationInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateMultipleShoppingLocations([FromBody] CreateMultipleShoppingLocationsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var shoppingLocationInfos = await new LocationFunctions().CreateMultipleShoppingLocationsAsync(request.Locations);
            return Ok(ApiResponse<List<ShoppingLocationInfo>>.SuccessResponse(shoppingLocationInfos, $"{shoppingLocationInfos.Count} shopping locations created successfully"));
        }

        /// <summary>
        /// Deletes multiple shopping locations in a single request.
        /// </summary>
        [HttpDelete("shopping/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
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
        /// <summary>
        /// Gets all storage locations for the current user's family.
        /// </summary>
        [HttpGet("storage")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<StorageLocationInfo>>), StatusCodes.Status200OK)]
        public IActionResult GetStorageLocations()
        {
            var storageLocations = new LocationFunctions().GetAllStorageLocations();
            return Ok(ApiResponse<List<StorageLocationInfo>>.SuccessResponse(storageLocations));
        }

        /// <summary>
        /// Creates a new storage location.
        /// </summary>
        [HttpPost("storage")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<StorageLocationInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateStorageLocation([FromBody] StorageLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var storageLocationInfo = await new LocationFunctions().CreateStorageLocationAsync(request);
            return Ok(ApiResponse<StorageLocationInfo>.SuccessResponse(storageLocationInfo, "Storage location created successfully"));
        }

        /// <summary>
        /// Updates an existing storage location.
        /// </summary>
        [HttpPut("storage/{storageLocationPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<StorageLocationInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStorageLocation(Guid storageLocationPublicId, [FromBody] StorageLocationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var storageLocationInfo = await new LocationFunctions().UpdateStorageLocationAsync(storageLocationPublicId, request);
            return Ok(ApiResponse<StorageLocationInfo>.SuccessResponse(storageLocationInfo, "Storage location updated successfully"));
        }

        /// <summary>
        /// Deletes a storage location.
        /// </summary>
        [HttpDelete("storage/{storageLocationPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteStorageLocation(Guid storageLocationPublicId)
        {
            await new LocationFunctions().DeleteStorageLocationAsync(storageLocationPublicId);
            Log.Information("Storage location {StorageLocationPublicId} deleted successfully", storageLocationPublicId);
            return Ok(ApiResponse.SuccessResponse("Storage location deleted successfully"));
        }

        /// <summary>
        /// Creates multiple storage locations in a single request.
        /// </summary>
        [HttpPost("storage/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<StorageLocationInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateMultipleStorageLocations([FromBody] CreateMultipleStorageLocationsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var storageLocationInfos = await new LocationFunctions().CreateMultipleStorageLocationsAsync(request.Locations);
            return Ok(ApiResponse<List<StorageLocationInfo>>.SuccessResponse(storageLocationInfos, $"{storageLocationInfos.Count} storage locations created successfully"));
        }

        /// <summary>
        /// Deletes multiple storage locations in a single request.
        /// </summary>
        [HttpDelete("storage/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
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
