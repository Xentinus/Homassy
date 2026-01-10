using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models;
using Homassy.API.Models.Common;
using Homassy.API.Models.Product;
using Homassy.API.Models.ImageUpload;
using Homassy.API.Services;
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
        private readonly IImageProcessingService _imageProcessingService;
        private readonly IProgressTrackerService _progressTrackerService;

        public ProductController(IImageProcessingService imageProcessingService, IProgressTrackerService progressTrackerService)
        {
            _imageProcessingService = imageProcessingService;
            _progressTrackerService = progressTrackerService;
        }

        #region Product
        /// <summary>
        /// Gets all products for the current user's family with pagination support.
        /// </summary>
        [HttpGet]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductInfo>>), StatusCodes.Status200OK)]
        public IActionResult GetProducts([FromQuery] PaginationRequest pagination)
        {
            var products = new ProductFunctions().GetAllProducts(pagination);
            return Ok(ApiResponse<PagedResult<ProductInfo>>.SuccessResponse(products));
        }

        /// <summary>
        /// Creates a new product.
        /// </summary>
        [HttpPost]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ProductInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var productInfo = await new ProductFunctions().CreateProductAsync(request, cancellationToken);
            return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo));
        }

        /// <summary>
        /// Updates an existing product.
        /// </summary>
        [HttpPut("{productPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ProductInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProduct(Guid productPublicId, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var productInfo = await new ProductFunctions().UpdateProductAsync(productPublicId, request, cancellationToken);
            return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo));
        }

        /// <summary>
        /// Deletes a product and all its inventory items.
        /// </summary>
        [HttpDelete("{productPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteProduct(Guid productPublicId, CancellationToken cancellationToken)
        {
            await new ProductFunctions().DeleteProductAsync(productPublicId, cancellationToken);

            Log.Information($"Product {productPublicId} deleted successfully");
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Toggles the favorite status of a product.
        /// </summary>
        [HttpPost("{productPublicId}/favorite")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ProductInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ToggleFavorite(Guid productPublicId, CancellationToken cancellationToken)
        {
            var productInfo = await new ProductFunctions().ToggleFavoriteAsync(productPublicId, cancellationToken);
            return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo));
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
                return NotFound(ApiResponse.ErrorResponse(ErrorCodes.ProductNotFound));
            }

            return Ok(ApiResponse<DetailedProductInfo>.SuccessResponse(detailedProduct));
        }

        /// <summary>
        /// Gets detailed information about all products including inventory items with pagination support.
        /// </summary>
        [HttpGet("detailed")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<DetailedProductInfo>>), StatusCodes.Status200OK)]
        public IActionResult GetAllDetailedProducts([FromQuery] PaginationRequest pagination)
        {
            var detailedProducts = new ProductFunctions().GetAllDetailedProductsForUser(pagination);
            return Ok(ApiResponse<PagedResult<DetailedProductInfo>>.SuccessResponse(detailedProducts));
        }

        /// <summary>
        /// Gets the count of expiring and expired inventory items for the current user.
        /// </summary>
        [HttpGet("inventory/expiration-count")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ExpirationCountResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetExpirationCount(CancellationToken cancellationToken)
        {
            var count = await new ProductFunctions().GetExpiringAndExpiredInventoryCountAsync(cancellationToken);

            var response = new ExpirationCountResponse
            {
                TotalCount = count
            };

            return Ok(ApiResponse<ExpirationCountResponse>.SuccessResponse(response));
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
        public async Task<IActionResult> CreateInventoryItem([FromBody] CreateInventoryItemRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var inventoryItemInfo = await new ProductFunctions().CreateInventoryItemAsync(request, cancellationToken);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo));
        }

        /// <summary>
        /// Quickly adds an inventory item with minimal required information.
        /// </summary>
        [HttpPost("inventory/quick")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<InventoryItemInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> QuickAddInventoryItem([FromBody] QuickAddInventoryItemRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var inventoryItemInfo = await new ProductFunctions().QuickAddInventoryItemAsync(request, cancellationToken);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo));
        }

        /// <summary>
        /// Updates an existing inventory item.
        /// </summary>
        [HttpPut("inventory/{inventoryItemPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<InventoryItemInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateInventoryItem(Guid inventoryItemPublicId, [FromBody] UpdateInventoryItemRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var inventoryItemInfo = await new ProductFunctions().UpdateInventoryItemAsync(inventoryItemPublicId, request, cancellationToken);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo));
        }

        /// <summary>
        /// Deletes an inventory item.
        /// </summary>
        [HttpDelete("inventory/{inventoryItemPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteInventoryItem(Guid inventoryItemPublicId, CancellationToken cancellationToken)
        {
            await new ProductFunctions().DeleteInventoryItemAsync(inventoryItemPublicId, cancellationToken);

            Log.Information($"Inventory item {inventoryItemPublicId} deleted successfully");
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Records consumption of an inventory item.
        /// </summary>
        [HttpPost("inventory/{inventoryItemPublicId}/consume")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<InventoryItemInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ConsumeInventoryItem(Guid inventoryItemPublicId, [FromBody] ConsumeInventoryItemRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var inventoryItemInfo = await new ProductFunctions().ConsumeInventoryItemAsync(inventoryItemPublicId, request, cancellationToken);
            return Ok(ApiResponse<InventoryItemInfo>.SuccessResponse(inventoryItemInfo));
        }

        /// <summary>
        /// Splits an inventory item into two separate items.
        /// </summary>
        [HttpPost("inventory/{inventoryItemPublicId}/split")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<SplitInventoryItemResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SplitInventoryItem(Guid inventoryItemPublicId, [FromBody] SplitInventoryItemRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var response = await new ProductFunctions().SplitInventoryItemAsync(inventoryItemPublicId, request, cancellationToken);
            return Ok(ApiResponse<SplitInventoryItemResponse>.SuccessResponse(response));
        }

        /// <summary>
        /// Quickly adds multiple inventory items in a single request.
        /// </summary>
        [HttpPost("inventory/quick/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<InventoryItemInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> QuickAddMultipleInventoryItems([FromBody] QuickAddMultipleInventoryItemsRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var inventoryItems = await new ProductFunctions().QuickAddMultipleInventoryItemsAsync(request, cancellationToken);
            return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(inventoryItems));
        }

        /// <summary>
        /// Moves inventory items to a different storage location.
        /// </summary>
        [HttpPost("inventory/move")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<InventoryItemInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MoveInventoryItems([FromBody] MoveInventoryItemsRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var inventoryItems = await new ProductFunctions().MoveInventoryItemsAsync(request, cancellationToken);
            return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(inventoryItems));
        }

        /// <summary>
        /// Deletes multiple inventory items in a single request.
        /// </summary>
        [HttpDelete("inventory/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteMultipleInventoryItems([FromBody] DeleteMultipleInventoryItemsRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            await new ProductFunctions().DeleteMultipleInventoryItemsAsync(request, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Records consumption of multiple inventory items in a single request.
        /// </summary>
        [HttpPost("inventory/consume/multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<InventoryItemInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConsumeMultipleInventoryItems([FromBody] ConsumeMultipleInventoryItemsRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var items = await new ProductFunctions().ConsumeMultipleInventoryItemsAsync(request, cancellationToken);
            return Ok(ApiResponse<List<InventoryItemInfo>>.SuccessResponse(items));
        }

        /// <summary>
        /// Creates multiple products in a single request.
        /// </summary>
        [HttpPost("multiple")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<ProductInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateMultipleProducts([FromBody] CreateMultipleProductsRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var products = await new ProductFunctions().CreateMultipleProductsAsync(request, cancellationToken);
            return Ok(ApiResponse<List<ProductInfo>>.SuccessResponse(products));
        }
        #endregion

        #region Product Image
        /// <summary>
        /// Uploads and processes an image for a product (synchronous - legacy).
        /// </summary>
        [HttpPost("{productPublicId}/image")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ProductImageInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UploadProductImage(Guid productPublicId, [FromBody] UploadProductImageRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var uploadRequest = new UploadProductImageRequest
            {
                ProductPublicId = productPublicId,
                ImageBase64 = request.ImageBase64
            };

            var imageInfo = await new ImageFunctions(_imageProcessingService).UploadProductImageAsync(uploadRequest, null, cancellationToken);
            return Ok(ApiResponse<ProductImageInfo>.SuccessResponse(imageInfo));
        }

        /// <summary>
        /// Uploads and processes an image for a product asynchronously with progress tracking.
        /// </summary>
        [HttpPost("{productPublicId}/image/upload-async")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<UploadJobResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public IActionResult UploadProductImageAsync(Guid productPublicId, [FromBody] UploadProductImageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var jobId = _progressTrackerService.CreateJob();

            // Start background task
            _ = Task.Run(async () =>
            {
                try
                {
                    var cancellationToken = _progressTrackerService.GetCancellationToken(jobId);
                    
                    var uploadRequest = new UploadProductImageRequest
                    {
                        ProductPublicId = productPublicId,
                        ImageBase64 = request.ImageBase64
                    };

                    var progress = new Progress<ProgressInfo>(info =>
                    {
                        _progressTrackerService.UpdateProgress(jobId, info.Percentage, info.Stage, info.Status);
                    });

                    await new ImageFunctions(_imageProcessingService).UploadProductImageAsync(uploadRequest, progress, cancellationToken);
                    
                    _progressTrackerService.CompleteJob(jobId);
                }
                catch (OperationCanceledException)
                {
                    _progressTrackerService.CancelJob(jobId);
                    Log.Information($"Product image upload cancelled for job {jobId}");
                }
                catch (Exception ex)
                {
                    _progressTrackerService.FailJob(jobId, ex.Message);
                    Log.Error(ex, $"Failed to upload product image for job {jobId}");
                }
            });

            return Ok(ApiResponse<UploadJobResponse>.SuccessResponse(new UploadJobResponse { JobId = jobId }));
        }

        /// <summary>
        /// Deletes the image of a product.
        /// </summary>
        [HttpDelete("{productPublicId}/image")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteProductImage(Guid productPublicId, CancellationToken cancellationToken)
        {
            await new ImageFunctions(_imageProcessingService).DeleteProductImageAsync(productPublicId, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }
        #endregion
    }
}
