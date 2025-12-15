using Asp.Versioning;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
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
            var shoppingLists = new ShoppingListFunctions().GetAllShoppingLists();
            return Ok(ApiResponse<List<ShoppingListInfo>>.SuccessResponse(shoppingLists));
        }

        [HttpGet("{publicId}")]
        [MapToApiVersion(1.0)]
        public IActionResult GetShoppingList(Guid publicId, [FromQuery] bool showPurchased = false)
        {
            var shoppingList = new ShoppingListFunctions().GetDetailedShoppingList(publicId, showPurchased);

            if (shoppingList == null)
            {
                return NotFound(ApiResponse.ErrorResponse("Shopping list not found"));
            }

            return Ok(ApiResponse<DetailedShoppingListInfo>.SuccessResponse(shoppingList));
        }

        [HttpPost]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateShoppingList([FromBody] CreateShoppingListRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var shoppingListInfo = await new ShoppingListFunctions().CreateShoppingListAsync(request);
            return Ok(ApiResponse<ShoppingListInfo>.SuccessResponse(shoppingListInfo, "Shopping list created successfully"));
        }

        [HttpPut("{publicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateShoppingList(Guid publicId, [FromBody] UpdateShoppingListRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var shoppingListInfo = await new ShoppingListFunctions().UpdateShoppingListAsync(publicId, request);
            return Ok(ApiResponse<ShoppingListInfo>.SuccessResponse(shoppingListInfo, "Shopping list updated successfully"));
        }

        [HttpDelete("{publicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteShoppingList(Guid publicId)
        {
            await new ShoppingListFunctions().DeleteShoppingListAsync(publicId);

            Log.Information("Shopping list {PublicId} deleted successfully", publicId);
            return Ok(ApiResponse.SuccessResponse("Shopping list deleted successfully"));
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

            var shoppingListItemInfo = await new ShoppingListFunctions().CreateShoppingListItemAsync(request);
            return Ok(ApiResponse<ShoppingListItemInfo>.SuccessResponse(shoppingListItemInfo, "Shopping list item created successfully"));
        }

        [HttpPut("item/{publicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateShoppingListItem(Guid publicId, [FromBody] UpdateShoppingListItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var shoppingListItemInfo = await new ShoppingListFunctions().UpdateShoppingListItemAsync(publicId, request);
            return Ok(ApiResponse<ShoppingListItemInfo>.SuccessResponse(shoppingListItemInfo, "Shopping list item updated successfully"));
        }

        [HttpDelete("item/{publicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteShoppingListItem(Guid publicId)
        {
            await new ShoppingListFunctions().DeleteShoppingListItemAsync(publicId);

            Log.Information("Shopping list item {PublicId} deleted successfully", publicId);
            return Ok(ApiResponse.SuccessResponse("Shopping list item deleted successfully"));
        }

        [HttpPost("item/quick-purchase")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> QuickPurchaseFromShoppingListItem([FromBody] QuickPurchaseFromShoppingListItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var shoppingListItem = await new ShoppingListFunctions().QuickPurchaseFromShoppingListItemAsync(request);
            return Ok(ApiResponse<ShoppingListItemInfo>.SuccessResponse(shoppingListItem, "Shopping list item purchased and inventory item created successfully"));
        }

        [HttpPost("item/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateMultipleShoppingListItems([FromBody] CreateMultipleShoppingListItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var items = await new ShoppingListFunctions().CreateMultipleShoppingListItemsAsync(request);
            return Ok(ApiResponse<List<ShoppingListItemInfo>>.SuccessResponse(items, $"{items.Count} shopping list items created successfully"));
        }

        [HttpDelete("item/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteMultipleShoppingListItems([FromBody] DeleteMultipleShoppingListItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new ShoppingListFunctions().DeleteMultipleShoppingListItemsAsync(request);
            return Ok(ApiResponse.SuccessResponse($"{request.ItemPublicIds.Count} shopping list items deleted successfully"));
        }

        [HttpPost("item/quick-purchase/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> QuickPurchaseMultipleShoppingListItems([FromBody] QuickPurchaseMultipleShoppingListItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var items = await new ShoppingListFunctions().QuickPurchaseMultipleShoppingListItemsAsync(request);
            return Ok(ApiResponse<List<ShoppingListItemInfo>>.SuccessResponse(items, $"{items.Count} shopping list items purchased successfully"));
        }
        #endregion
    }
}
