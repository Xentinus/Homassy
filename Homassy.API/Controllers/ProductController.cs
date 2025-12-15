using Asp.Versioning;
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
            var products = new ProductFunctions().GetAllProducts();
            return Ok(ApiResponse<List<ProductInfo>>.SuccessResponse(products));
        }

        [HttpPost]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var productInfo = await new ProductFunctions().CreateProductAsync(request);
            return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo, "Product created successfully"));
        }

        [HttpPut("{productPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateProduct(Guid productPublicId, [FromBody] UpdateProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var productInfo = await new ProductFunctions().UpdateProductAsync(productPublicId, request);
            return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo, "Product updated successfully"));
        }

        [HttpDelete("{productPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteProduct(Guid productPublicId)
        {
            await new ProductFunctions().DeleteProductAsync(productPublicId);

            Log.Information($"Product {productPublicId} deleted successfully");
            return Ok(ApiResponse.SuccessResponse("Product deleted successfully"));
        }

        [HttpPost("{productPublicId}/favorite")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> ToggleFavorite(Guid productPublicId)
        {
            var productInfo = await new ProductFunctions().ToggleFavoriteAsync(productPublicId);
            return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo, "Favorite status updated successfully"));
        }
        #endregion

        #region User Products
        [HttpGet("{productPublicId}/detailed")]
        [MapToApiVersion(1.0)]
        public IActionResult GetDetailedProduct(Guid productPublicId)
        {
            var detailedProduct = new ProductFunctions().GetDetailedProductInfo(productPublicId);

            if (detailedProduct == null)
            {
                return NotFound(ApiResponse.ErrorResponse("Product not found"));
            }

            return Ok(ApiResponse<DetailedProductInfo>.SuccessResponse(detailedProduct));
        }

        [HttpGet("detailed")]
        [MapToApiVersion(1.0)]
        public IActionResult GetAllDetailedProducts()
        {
            var detailedProducts = new ProductFunctions().GetAllDetailedProductsForUser();
            return Ok(ApiResponse<List<DetailedProductInfo>>.SuccessResponse(detailedProducts));
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

            var inventoryItemInfo = await new ProductFunctions().CreateInventoryItemAsync(request);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Inventory item created successfully"));
        }

        [HttpPost("inventory/quick")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> QuickAddInventoryItem([FromBody] QuickAddInventoryItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var inventoryItemInfo = await new ProductFunctions().QuickAddInventoryItemAsync(request);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Inventory item added successfully"));
        }

        [HttpPut("inventory/{inventoryItemPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateInventoryItem(Guid inventoryItemPublicId, [FromBody] UpdateInventoryItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var inventoryItemInfo = await new ProductFunctions().UpdateInventoryItemAsync(inventoryItemPublicId, request);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Inventory item updated successfully"));
        }

        [HttpDelete("inventory/{inventoryItemPublicId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteInventoryItem(Guid inventoryItemPublicId)
        {
            await new ProductFunctions().DeleteInventoryItemAsync(inventoryItemPublicId);

            Log.Information($"Inventory item {inventoryItemPublicId} deleted successfully");
            return Ok(ApiResponse.SuccessResponse("Inventory item deleted successfully"));
        }

        [HttpPost("inventory/{inventoryItemPublicId}/consume")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> ConsumeInventoryItem(Guid inventoryItemPublicId, [FromBody] ConsumeInventoryItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var inventoryItemInfo = await new ProductFunctions().ConsumeInventoryItemAsync(inventoryItemPublicId, request);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo, "Consumption recorded successfully"));
        }

        [HttpPost("inventory/quick/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> QuickAddMultipleInventoryItems([FromBody] QuickAddMultipleInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var inventoryItems = await new ProductFunctions().QuickAddMultipleInventoryItemsAsync(request);
            return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(inventoryItems, "Inventory items added successfully"));
        }

        [HttpPost("inventory/move")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> MoveInventoryItems([FromBody] MoveInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var inventoryItems = await new ProductFunctions().MoveInventoryItemsAsync(request);
            return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(inventoryItems, "Inventory items moved successfully"));
        }

        [HttpDelete("inventory/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteMultipleInventoryItems([FromBody] DeleteMultipleInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            await new ProductFunctions().DeleteMultipleInventoryItemsAsync(request);
            return Ok(ApiResponse.SuccessResponse($"{request.ItemPublicIds.Count} inventory items deleted successfully"));
        }

        [HttpPost("inventory/consume/multiple")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> ConsumeMultipleInventoryItems([FromBody] ConsumeMultipleInventoryItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var items = await new ProductFunctions().ConsumeMultipleInventoryItemsAsync(request);
            return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(items, $"{items.Count} inventory items consumed successfully"));
        }

        [HttpPost("multiple")]
        [MapToApiVersion(1.0)]
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
