using Asp.Versioning;
using Homassy.API.Exceptions;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Product;
using Homassy.API.Models.ShoppingList;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class ShoppingListController : ControllerBase
    {
        #region ShoppingList
        [HttpGet]
        [MapToApiVersion(1.0)]
        public IActionResult GetShoppingLists()
        {
            try
            {
                var shoppingLists = new ShoppingListFunctions().GetAllShoppingLists();
                return Ok(ApiResponse<List<ShoppingListInfo>>.SuccessResponse(shoppingLists));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error retrieving shopping lists: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving shopping lists"));
            }
        }

        [HttpGet("{publicId}")]
        [MapToApiVersion(1.0)]
        public IActionResult GetShoppingList(Guid publicId, [FromQuery] bool showPurchased = false)
        {
            try
            {
                var shoppingList = new ShoppingListFunctions().GetDetailedShoppingList(publicId, showPurchased);

                if (shoppingList == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Shopping list not found"));
                }

                return Ok(ApiResponse<DetailedShoppingListInfo>.SuccessResponse(shoppingList));
            }
            catch (ShoppingListAccessDeniedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error retrieving shopping list {publicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving the shopping list"));
            }
        }

        [HttpPost]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateShoppingList([FromBody] CreateShoppingListRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var shoppingListInfo = await new ShoppingListFunctions().CreateShoppingListAsync(request);
                return Ok(ApiResponse<ShoppingListInfo>.SuccessResponse(shoppingListInfo, "Shopping list created successfully"));
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
                Log.Error($"Error creating shopping list: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while creating the shopping list"));
            }
        }

        [HttpPut("{publicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateShoppingList(Guid publicId, [FromBody] UpdateShoppingListRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var shoppingListInfo = await new ShoppingListFunctions().UpdateShoppingListAsync(publicId, request);
                return Ok(ApiResponse<ShoppingListInfo>.SuccessResponse(shoppingListInfo, "Shopping list updated successfully"));
            }
            catch (ShoppingListNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ShoppingListAccessDeniedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error updating shopping list {publicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while updating the shopping list"));
            }
        }

        [HttpDelete("{publicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteShoppingList(Guid publicId)
        {
            try
            {
                await new ShoppingListFunctions().DeleteShoppingListAsync(publicId);

                Log.Information($"Shopping list {publicId} deleted successfully");
                return Ok(ApiResponse.SuccessResponse("Shopping list deleted successfully"));
            }
            catch (ShoppingListNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ShoppingListAccessDeniedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error deleting shopping list {publicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the shopping list"));
            }
        }
        #endregion

        #region ShoppingListItem
        [HttpPost("item")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateShoppingListItem([FromBody] CreateShoppingListItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var shoppingListItemInfo = await new ShoppingListFunctions().CreateShoppingListItemAsync(request);
                return Ok(ApiResponse<ShoppingListItemInfo>.SuccessResponse(shoppingListItemInfo, "Shopping list item created successfully"));
            }
            catch (ShoppingListNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ShoppingListAccessDeniedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (InvalidShoppingListItemException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ProductNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ShoppingLocationNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error creating shopping list item: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while creating the shopping list item"));
            }
        }

        [HttpPut("item/{publicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateShoppingListItem(Guid publicId, [FromBody] UpdateShoppingListItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var shoppingListItemInfo = await new ShoppingListFunctions().UpdateShoppingListItemAsync(publicId, request);
                return Ok(ApiResponse<ShoppingListItemInfo>.SuccessResponse(shoppingListItemInfo, "Shopping list item updated successfully"));
            }
            catch (ShoppingListItemNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ShoppingListNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ShoppingListAccessDeniedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ProductNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ShoppingLocationNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error updating shopping list item {publicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while updating the shopping list item"));
            }
        }

        [HttpDelete("item/{publicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteShoppingListItem(Guid publicId)
        {
            try
            {
                await new ShoppingListFunctions().DeleteShoppingListItemAsync(publicId);

                Log.Information($"Shopping list item {publicId} deleted successfully");
                return Ok(ApiResponse.SuccessResponse("Shopping list item deleted successfully"));
            }
            catch (ShoppingListItemNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ShoppingListNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ShoppingListAccessDeniedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error deleting shopping list item {publicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the shopping list item"));
            }
        }

        [HttpPost("item/quick-purchase")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> QuickPurchaseFromShoppingListItem([FromBody] QuickPurchaseFromShoppingListItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var (shoppingListItem, inventoryItem) = await new ShoppingListFunctions().QuickPurchaseFromShoppingListItemAsync(request);
                return Ok(ApiResponse<QuickPurchaseFromShoppingListItemResponse>.SuccessResponse(
                    new QuickPurchaseFromShoppingListItemResponse
                    {
                        ShoppingListItem = shoppingListItem,
                        InventoryItem = inventoryItem
                    },
                    "Shopping list item purchased and inventory item created successfully"));
            }
            catch (ShoppingListItemNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (InvalidShoppingListItemException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ShoppingListNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ShoppingListAccessDeniedException ex)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (ProductNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (StorageLocationNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error quick purchasing shopping list item: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while processing the purchase"));
            }
        }
        #endregion
    }
}
