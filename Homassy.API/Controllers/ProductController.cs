using Asp.Versioning;
using Homassy.API.Context;
using Homassy.API.Entities.Product;
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
        #region Product Management
        [HttpGet]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> GetProducts()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            try
            {
                var products = new ProductFunctions().GetProductsByUserIdAndFamilyId(userId.Value, user.FamilyId);
                Log.Information($"User {userId.Value} retrieved {products.Count} visible products");
                return Ok(ApiResponse<List<Product>>.SuccessResponse(products));
            }
            catch (Exception ex)
            {
                Log.Error($"Error retrieving products for user {userId.Value}: {ex.Message}");
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

            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            try
            {
                var product = await new ProductFunctions().CreateProductAsync(user, request);
                Log.Information($"User {userId.Value} created product {product.Id}");
                return Ok(ApiResponse<int>.SuccessResponse(product.Id, "Product created successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error creating product for user {userId.Value}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while creating the product"));
            }
        }

        [HttpPut("{productId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateProduct(int productId, [FromBody] UpdateProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            var product = new ProductFunctions().GetProductById(productId);
            if (product == null || product.IsDeleted)
            {
                return NotFound(ApiResponse.ErrorResponse("Product not found"));
            }

            // Check ownership
            if (product.UserId != userId.Value && (product.FamilyId == null || product.FamilyId != user.FamilyId))
            {
                return Forbid();
            }

            try
            {
                await new ProductFunctions().UpdateProductAsync(product, request, userId.Value);
                Log.Information($"User {userId.Value} updated product {productId}");
                return Ok(ApiResponse.SuccessResponse("Product updated successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error updating product {productId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while updating the product"));
            }
        }

        [HttpPost("{productId}/picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UploadProductPicture(int productId, [FromBody] UploadProductPictureRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
            }

            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            var product = new ProductFunctions().GetProductById(productId);
            if (product == null || product.IsDeleted)
            {
                return NotFound(ApiResponse.ErrorResponse("Product not found"));
            }

            // Check ownership
            if (product.UserId != userId.Value && (product.FamilyId == null || product.FamilyId != user.FamilyId))
            {
                return Forbid();
            }

            try
            {
                await new ProductFunctions().UploadProductPictureAsync(product, request.ProductPictureBase64, userId.Value);
                Log.Information($"User {userId.Value} uploaded picture for product {productId}");
                return Ok(ApiResponse.SuccessResponse("Product picture uploaded successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error uploading picture for product {productId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while uploading the product picture"));
            }
        }

        [HttpDelete("{productId}/picture")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteProductPicture(int productId)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            var product = new ProductFunctions().GetProductById(productId);
            if (product == null || product.IsDeleted)
            {
                return NotFound(ApiResponse.ErrorResponse("Product not found"));
            }

            // Check ownership
            if (product.UserId != userId.Value && (product.FamilyId == null || product.FamilyId != user.FamilyId))
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(product.ProductPictureBase64))
            {
                return BadRequest(ApiResponse.ErrorResponse("No product picture to delete"));
            }

            try
            {
                await new ProductFunctions().DeleteProductPictureAsync(product, userId.Value);
                Log.Information($"User {userId.Value} deleted picture for product {productId}");
                return Ok(ApiResponse.SuccessResponse("Product picture deleted successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error deleting picture for product {productId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the product picture"));
            }
        }

        [HttpPost("{productId}/make-personal")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> MakeProductPersonal(int productId)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            var product = new ProductFunctions().GetProductById(productId);
            if (product == null || product.IsDeleted)
            {
                return NotFound(ApiResponse.ErrorResponse("Product not found"));
            }

            // Must be a family product
            if (product.FamilyId == null)
            {
                return BadRequest(ApiResponse.ErrorResponse("Product is already personal"));
            }

            // Check ownership
            if (product.FamilyId != user.FamilyId)
            {
                return Forbid();
            }

            try
            {
                await new ProductFunctions().MakeProductPersonalAsync(product, userId.Value);
                Log.Information($"User {userId.Value} made product {productId} personal");
                return Ok(ApiResponse.SuccessResponse("Product is now personal"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error making product {productId} personal: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while making the product personal"));
            }
        }

        [HttpPost("{productId}/add-to-family")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> AddProductToFamily(int productId)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            if (!user.FamilyId.HasValue)
            {
                return BadRequest(ApiResponse.ErrorResponse("User does not belong to a family"));
            }

            var product = new ProductFunctions().GetProductById(productId);
            if (product == null || product.IsDeleted)
            {
                return NotFound(ApiResponse.ErrorResponse("Product not found"));
            }

            // Must be owned by user
            if (product.UserId != userId.Value)
            {
                return Forbid();
            }

            // Must be personal (no FamilyId)
            if (product.FamilyId.HasValue)
            {
                return BadRequest(ApiResponse.ErrorResponse("Product is already shared with a family"));
            }

            try
            {
                await new ProductFunctions().AddProductToFamilyAsync(product, user.FamilyId.Value, userId.Value);
                Log.Information($"User {userId.Value} added product {productId} to family {user.FamilyId.Value}");
                return Ok(ApiResponse.SuccessResponse("Product added to family successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error adding product {productId} to family: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while adding the product to family"));
            }
        }

        [HttpDelete("{productId}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteProduct(int productId)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid authentication"));
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                return NotFound(ApiResponse.ErrorResponse("User not found"));
            }

            var product = new ProductFunctions().GetProductById(productId);
            if (product == null || product.IsDeleted)
            {
                return NotFound(ApiResponse.ErrorResponse("Product not found"));
            }

            // Check ownership
            if (product.UserId != userId.Value && (product.FamilyId == null || product.FamilyId != user.FamilyId))
            {
                return Forbid();
            }

            try
            {
                await new ProductFunctions().DeleteProductAsync(product, userId.Value);
                Log.Information($"User {userId.Value} deleted product {productId}");
                return Ok(ApiResponse.SuccessResponse("Product deleted successfully"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error deleting product {productId}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while deleting the product"));
            }
        }
        #endregion
    }
}
