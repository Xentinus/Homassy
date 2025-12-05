using Asp.Versioning;
using Homassy.API.Context;
using Homassy.API.Entities.Product;
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
    }
}
