using Asp.Versioning;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Product;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Product and inventory management endpoints.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductController : ControllerBase
    {
        #region Product
        /// <summary>
        /// Gets all products for the current user's family.
        /// </summary>
        [HttpGet]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<ProductInfo>>), StatusCodes.Status200OK)]
        public IActionResult GetProducts()
        {
            var products = new ProductFunctions().GetAllProducts();
            return Ok(ApiResponse<List<ProductInfo>>.SuccessResponse(products));
        }

        /// <summary>
        /// Creates a new product.
        /// </summary>
        [HttpPost]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ProductInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var productInfo = await new ProductFunctions().CreateProductAsync(request);
            return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo, "Product created successfully"));
        }

        /// <summary>
        /// Updates an existing product.
        /// </summary>
        [HttpPut("{productPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ProductInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProduct(Guid productPublicId, [FromBody] UpdateProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var productInfo = await new ProductFunctions().UpdateProductAsync(productPublicId, request);
            return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo, "Product updated successfully"));
        }

        /// <summary>
        /// Deletes a product and all its inventory items.
        /// </summary>
        [HttpDelete("{productPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteProduct(Guid productPublicId)
        {
            await new ProductFunctions().DeleteProductAsync(productPublicId);

            Log.Information($"Product {productPublicId} deleted successfully");
            return Ok(ApiResponse.SuccessResponse("Product deleted successfully"));
        }

        /// <summary>
        /// Toggles the favorite status of a product.
        /// </summary>
        [HttpPost("{productPublicId}/favorite")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ProductInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ToggleFavorite(Guid productPublicId)
        {
            var productInfo = await new ProductFunctions().ToggleFavoriteAsync(productPublicId);
            return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo, "Favorite status updated successfully"));
        }
        #endregion

        #region User Products
        /// <summary>
        /// Gets detailed information about a specific product including inventory items.
        /// </summary>
        [HttpGet("{productPublicId}/detailed")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<DetailedProductInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public IActionResult GetDetailedProduct(Guid productPublicId)
        {
            var detailedProduct = new ProductFunctions().GetDetailedProductInfo(productPublicId);

            if (detailedProduct == null)
            {
                return NotFound(ApiResponse.ErrorResponse("Product not found"));
            }

            return Ok(ApiResponse<DetailedProductInfo>.SuccessResponse(detailedProduct));
        }

        /// <summary>
        /// Gets detailed information about all products including inventory items.
        /// </summary>
        [HttpGet("detailed")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<DetailedProductInfo>>), StatusCodes.Status200OK)]
        public IActionResult GetAllDetailedProducts()
        {
            var detailedProducts = new ProductFunctions().GetAllDetailedProductsForUser();
            return Ok(ApiResponse<List<DetailedProductInfo>>.SuccessResponse(detailedProducts));
        }
        #endregion

        #region InventoryItem
        /// <summary>
        /// Creates a new inventory item for a product.
        /// </summary>
        [HttpPost("inventory")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<InventoryItemInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateInventoryItem([FromBody] CreateInventoryItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var inventoryItemInfo = await new ProductFunctions().CreateInventoryItemAsync(request);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Inventory item created successfully"));
        }

        /// <summary>
        /// Quickly adds an inventory item with minimal required information.
        /// </summary>
        [HttpPost("inventory/quick")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<InventoryItemInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> QuickAddInventoryItem([FromBody] QuickAddInventoryItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var inventoryItemInfo = await new ProductFunctions().QuickAddInventoryItemAsync(request);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Inventory item added successfully"));
        }

        /// <summary>
        /// Updates an existing inventory item.
        /// </summary>
        [HttpPut("inventory/{inventoryItemPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<InventoryItemInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateInventoryItem(Guid inventoryItemPublicId, [FromBody] UpdateInventoryItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var inventoryItemInfo = await new ProductFunctions().UpdateInventoryItemAsync(inventoryItemPublicId, request);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Inventory item updated successfully"));
        }

        /// <summary>
        /// Deletes an inventory item.
        /// </summary>
        [HttpDelete("inventory/{inventoryItemPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteInventoryItem(Guid inventoryItemPublicId)
        {
            await new ProductFunctions().DeleteInventoryItemAsync(inventoryItemPublicId);

            Log.Information($"Inventory item {inventoryItemPublicId} deleted successfully");
            return Ok(ApiResponse.SuccessResponse("Inventory item deleted successfully"));
        }

        /// <summary>
        /// Records consumption of an inventory item.
        /// </summary>
        [HttpPost("inventory/{inventoryItemPublicId}/consume")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<InventoryItemInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ConsumeInventoryItem(Guid inventoryItemPublicId, [FromBody] ConsumeInventoryItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var inventoryItemInfo = await new ProductFunctions().ConsumeInventoryItemAsync(inventoryItemPublicId, request);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Consumption recorded successfully"));
        }

        /// <summary>
        /// Quickly adds multiple inventory items in a single request.
        /// </summary>
        [HttpPost("inventory/quick/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<InventoryItemInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> QuickAddMultipleInventoryItems([FromBody] QuickAddMultipleInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var inventoryItems = await new ProductFunctions().QuickAddMultipleInventoryItemsAsync(request);
            return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(inventoryItems, "Inventory items added successfully"));
        }

        /// <summary>
        /// Moves inventory items to a different storage location.
        /// </summary>
        [HttpPost("inventory/move")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<InventoryItemInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MoveInventoryItems([FromBody] MoveInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var inventoryItems = await new ProductFunctions().MoveInventoryItemsAsync(request);
            return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(inventoryItems, "Inventory items moved successfully"));
        }

        /// <summary>
        /// Deletes multiple inventory items in a single request.
        /// </summary>
        [HttpDelete("inventory/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteMultipleInventoryItems([FromBody] DeleteMultipleInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new ProductFunctions().DeleteMultipleInventoryItemsAsync(request);
            return Ok(ApiResponse.SuccessResponse($"{request.ItemPublicIds.Count} inventory items deleted successfully"));
        }

        /// <summary>
        /// Records consumption of multiple inventory items in a single request.
        /// </summary>
        [HttpPost("inventory/consume/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<InventoryItemInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConsumeMultipleInventoryItems([FromBody] ConsumeMultipleInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var items = await new ProductFunctions().ConsumeMultipleInventoryItemsAsync(request);
            return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(items, $"{items.Count} inventory items consumed successfully"));
        }

        /// <summary>
        /// Creates multiple products in a single request.
        /// </summary>
        [HttpPost("multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<ProductInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateMultipleProducts([FromBody] CreateMultipleProductsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var products = await new ProductFunctions().CreateMultipleProductsAsync(request);
            return Ok(ApiResponse<List<ProductInfo>>.SuccessResponse(products, $"{products.Count} products created successfully"));
        }
        #endregion
    }
}
