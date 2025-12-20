using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.ShoppingList;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Shopping list management endpoints.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class ShoppingListController : ControllerBase
    {
        #region ShoppingList
        /// <summary>
        /// Gets all shopping lists for the current user's family.
        /// </summary>
        [HttpGet]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<ShoppingListInfo>>), StatusCodes.Status200OK)]
        public IActionResult GetShoppingLists()
        {
            var shoppingLists = new ShoppingListFunctions().GetAllShoppingLists();
            return Ok(ApiResponse<List<ShoppingListInfo>>.SuccessResponse(shoppingLists));
        }

        /// <summary>
        /// Gets detailed information about a specific shopping list including its items.
        /// </summary>
        [HttpGet("{publicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<DetailedShoppingListInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public IActionResult GetShoppingList(Guid publicId, [FromQuery] bool showPurchased = false)
        {
            var shoppingList = new ShoppingListFunctions().GetDetailedShoppingList(publicId, showPurchased);

            if (shoppingList == null)
            {
                return NotFound(ApiResponse.ErrorResponse(ErrorCodes.ShoppingListNotFound));
            }

            return Ok(ApiResponse<DetailedShoppingListInfo>.SuccessResponse(shoppingList));
        }

        /// <summary>
        /// Creates a new shopping list.
        /// </summary>
        [HttpPost]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ShoppingListInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateShoppingList([FromBody] CreateShoppingListRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var shoppingListInfo = await new ShoppingListFunctions().CreateShoppingListAsync(request, cancellationToken);
            return Ok(ApiResponse<ShoppingListInfo>.SuccessResponse(shoppingListInfo));
        }

        /// <summary>
        /// Updates an existing shopping list.
        /// </summary>
        [HttpPut("{publicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ShoppingListInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateShoppingList(Guid publicId, [FromBody] UpdateShoppingListRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var shoppingListInfo = await new ShoppingListFunctions().UpdateShoppingListAsync(publicId, request, cancellationToken);
            return Ok(ApiResponse<ShoppingListInfo>.SuccessResponse(shoppingListInfo));
        }

        /// <summary>
        /// Deletes a shopping list and all its items.
        /// </summary>
        [HttpDelete("{publicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteShoppingList(Guid publicId, CancellationToken cancellationToken)
        {
            await new ShoppingListFunctions().DeleteShoppingListAsync(publicId, cancellationToken);

            Log.Information($"Shopping list {publicId} deleted successfully");
            return Ok(ApiResponse.SuccessResponse());
        }
        #endregion

        #region ShoppingListItem
        /// <summary>
        /// Creates a new item in a shopping list.
        /// </summary>
        [HttpPost("item")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ShoppingListItemInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateShoppingListItem([FromBody] CreateShoppingListItemRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var shoppingListItemInfo = await new ShoppingListFunctions().CreateShoppingListItemAsync(request, cancellationToken);
            return Ok(ApiResponse<ShoppingListItemInfo>.SuccessResponse(shoppingListItemInfo));
        }

        /// <summary>
        /// Updates an existing shopping list item.
        /// </summary>
        [HttpPut("item/{publicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ShoppingListItemInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateShoppingListItem(Guid publicId, [FromBody] UpdateShoppingListItemRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var shoppingListItemInfo = await new ShoppingListFunctions().UpdateShoppingListItemAsync(publicId, request, cancellationToken);
            return Ok(ApiResponse<ShoppingListItemInfo>.SuccessResponse(shoppingListItemInfo));
        }

        /// <summary>
        /// Deletes a shopping list item.
        /// </summary>
        [HttpDelete("item/{publicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteShoppingListItem(Guid publicId, CancellationToken cancellationToken)
        {
            await new ShoppingListFunctions().DeleteShoppingListItemAsync(publicId, cancellationToken);

            Log.Information($"Shopping list item {publicId} deleted successfully");
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Marks a shopping list item as purchased and creates a corresponding inventory item.
        /// </summary>
        [HttpPost("item/quick-purchase")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ShoppingListItemInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> QuickPurchaseFromShoppingListItem([FromBody] QuickPurchaseFromShoppingListItemRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var shoppingListItem = await new ShoppingListFunctions().QuickPurchaseFromShoppingListItemAsync(request, cancellationToken);
            return Ok(ApiResponse<ShoppingListItemInfo>.SuccessResponse(shoppingListItem));
        }

        /// <summary>
        /// Creates multiple shopping list items in a single request.
        /// </summary>
        [HttpPost("item/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<ShoppingListItemInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateMultipleShoppingListItems([FromBody] CreateMultipleShoppingListItemsRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var items = await new ShoppingListFunctions().CreateMultipleShoppingListItemsAsync(request, cancellationToken);
            return Ok(ApiResponse<List<ShoppingListItemInfo>>.SuccessResponse(items));
        }

        /// <summary>
        /// Deletes multiple shopping list items in a single request.
        /// </summary>
        [HttpDelete("item/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteMultipleShoppingListItems([FromBody] DeleteMultipleShoppingListItemsRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            await new ShoppingListFunctions().DeleteMultipleShoppingListItemsAsync(request, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Marks multiple shopping list items as purchased and creates corresponding inventory items.
        /// </summary>
        [HttpPost("item/quick-purchase/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<ShoppingListItemInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> QuickPurchaseMultipleShoppingListItems([FromBody] QuickPurchaseMultipleShoppingListItemsRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var items = await new ShoppingListFunctions().QuickPurchaseMultipleShoppingListItemsAsync(request, cancellationToken);
            return Ok(ApiResponse<List<ShoppingListItemInfo>>.SuccessResponse(items));
        }
        #endregion
    }
}
