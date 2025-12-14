using Asp.Versioning;
using Homassy.API.Exceptions;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Product;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductController : ControllerBase
    {
        #region Product
        [HttpGet]
        [MapToApiVersion(1.0)]
        public IActionResult GetProducts()
        {
            try
            {
                var products = new ProductFunctions().GetAllProducts();
                return Ok(ApiResponse<List<ProductInfo>>.SuccessResponse(products));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error retrieving products: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving products"));
            }
        }

        [HttpPost]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var productInfo = await new ProductFunctions().CreateProductAsync(request);
                return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo, "Product created successfully"));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error creating product: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while creating the product"));
            }
        }

        [HttpPut("{productPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateProduct(Guid productPublicId, [FromBody] UpdateProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var productInfo = await new ProductFunctions().UpdateProductAsync(productPublicId, request);
                return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo, "Product updated successfully"));
            }
            catch (ProductNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error updating product {productPublicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while updating the product"));
            }
        }

        [HttpDelete("{productPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteProduct(Guid productPublicId)
        {
            try
            {
                await new ProductFunctions().DeleteProductAsync(productPublicId);

                Log.Information($"Product {productPublicId} deleted successfully");
                return Ok(ApiResponse.SuccessResponse("Product deleted successfully"));
            }
            catch (ProductNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error deleting product {productPublicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the product"));
            }
        }

        [HttpPost("{productPublicId}/favorite")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> ToggleFavorite(Guid productPublicId)
        {
            try
            {
                var productInfo = await new ProductFunctions().ToggleFavoriteAsync(productPublicId);
                return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo, "Favorite status updated successfully"));
            }
            catch (ProductNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error toggling favorite for product {productPublicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while updating favorite status"));
            }
        }
        #endregion

        #region User Products
        [HttpGet("{productPublicId}/detailed")]
        [MapToApiVersion(1.0)]
        public IActionResult GetDetailedProduct(Guid productPublicId)
        {
        try
        {
            var detailedProduct = new ProductFunctions().GetDetailedProductInfo(productPublicId);

            if (detailedProduct == null)
            {
                return NotFound(ApiResponse.ErrorResponse("Product not found"));
            }

            return Ok(ApiResponse<DetailedProductInfo>.SuccessResponse(detailedProduct));
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(ApiResponse.ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            Log.Error($"Error retrieving detailed product {productPublicId}: {ex.Message}");
            return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving product details"));
        }
        }

        [HttpGet("detailed")]
        [MapToApiVersion(1.0)]
        public IActionResult GetAllDetailedProducts()
        {
            try
            {
                var detailedProducts = new ProductFunctions().GetAllDetailedProductsForUser();
                return Ok(ApiResponse<List<DetailedProductInfo>>.SuccessResponse(detailedProducts));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error retrieving detailed products: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving detailed products"));
            }
        }
        #endregion

        #region InventoryItem
        [HttpPost("inventory")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateInventoryItem([FromBody] CreateInventoryItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var inventoryItemInfo = await new ProductFunctions().CreateInventoryItemAsync(request);
                return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Inventory item created successfully"));
            }
            catch (ProductNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (StorageLocationNotFoundException ex)
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
                Log.Error($"Error creating inventory item: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while creating the inventory item"));
            }
        }

        [HttpPost("inventory/quick")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> QuickAddInventoryItem([FromBody] QuickAddInventoryItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var inventoryItemInfo = await new ProductFunctions().QuickAddInventoryItemAsync(request);
                return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Inventory item added successfully"));
            }
            catch (ProductNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Error quick-adding inventory item: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while adding the inventory item"));
            }
        }

        [HttpPut("inventory/{inventoryItemPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateInventoryItem(Guid inventoryItemPublicId, [FromBody] UpdateInventoryItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var inventoryItemInfo = await new ProductFunctions().UpdateInventoryItemAsync(inventoryItemPublicId, request);
                return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Inventory item updated successfully"));
            }
            catch (ProductInventoryItemNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (StorageLocationNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
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
                Log.Error($"Error updating inventory item {inventoryItemPublicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while updating the inventory item"));
            }
        }

        [HttpDelete("inventory/{inventoryItemPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteInventoryItem(Guid inventoryItemPublicId)
        {
            try
            {
                await new ProductFunctions().DeleteInventoryItemAsync(inventoryItemPublicId);

                Log.Information($"Inventory item {inventoryItemPublicId} deleted successfully");
                return Ok(ApiResponse.SuccessResponse("Inventory item deleted successfully"));
            }
            catch (ProductInventoryItemNotFoundException ex)
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
                Log.Error($"Error deleting inventory item {inventoryItemPublicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the inventory item"));
            }
        }

        [HttpPost("inventory/{inventoryItemPublicId}/consume")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> ConsumeInventoryItem(Guid inventoryItemPublicId, [FromBody] ConsumeInventoryItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var inventoryItemInfo = await new ProductFunctions().ConsumeInventoryItemAsync(inventoryItemPublicId, request);
                return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Consumption recorded successfully"));
            }
            catch (ProductInventoryItemNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
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
                Log.Error($"Error consuming inventory item {inventoryItemPublicId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while recording consumption"));
            }
        }

        [HttpPost("inventory/quick/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> QuickAddMultipleInventoryItems([FromBody] QuickAddMultipleInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var inventoryItems = await new ProductFunctions().QuickAddMultipleInventoryItemsAsync(request);
                return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(inventoryItems, "Inventory items added successfully"));
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
                Log.Error($"Error quick-adding multiple inventory items: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while adding the inventory items"));
            }
        }

        [HttpPost("inventory/move")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> MoveInventoryItems([FromBody] MoveInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var inventoryItems = await new ProductFunctions().MoveInventoryItemsAsync(request);
                return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(inventoryItems, "Inventory items moved successfully"));
            }
            catch (ProductInventoryItemNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
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
                Log.Error($"Error moving inventory items: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while moving the inventory items"));
            }
        }

        [HttpDelete("inventory/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteMultipleInventoryItems([FromBody] DeleteMultipleInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                await new ProductFunctions().DeleteMultipleInventoryItemsAsync(request);
                return Ok(ApiResponse.SuccessResponse($"{request.ItemPublicIds.Count} inventory items deleted successfully"));
            }
            catch (ProductInventoryItemNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
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
                Log.Error($"Error deleting multiple inventory items: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the inventory items"));
            }
        }

        [HttpPost("inventory/consume/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> ConsumeMultipleInventoryItems([FromBody] ConsumeMultipleInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var items = await new ProductFunctions().ConsumeMultipleInventoryItemsAsync(request);
                return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(items, $"{items.Count} inventory items consumed successfully"));
            }
            catch (ProductInventoryItemNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
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
                Log.Error($"Error consuming multiple inventory items: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while consuming the inventory items"));
            }
        }

        [HttpPost("multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateMultipleProducts([FromBody] CreateMultipleProductsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            try
            {
                var products = await new ProductFunctions().CreateMultipleProductsAsync(request);
                return Ok(ApiResponse<List<ProductInfo>>.SuccessResponse(products, $"{products.Count} products created successfully"));
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
                Log.Error($"Error creating multiple products: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while creating the products"));
            }
        }
        #endregion
    }
}
