using Asp.Versioning;
using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.Models;
using Homassy.API.Models.Common;
using Homassy.API.Models.Insights;
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
        private readonly IProgressTrackerService _progressTrackerService;
        private readonly ProductFunctions _productFunctions;
        private readonly ImageFunctions _imageFunctions;
        private readonly PriceInsightFunctions _priceInsightFunctions;

        public ProductController(
            IProgressTrackerService progressTrackerService,
            ProductFunctions productFunctions,
            ImageFunctions imageFunctions,
            PriceInsightFunctions priceInsightFunctions)
        {
            _progressTrackerService = progressTrackerService;
            _productFunctions = productFunctions;
            _imageFunctions = imageFunctions;
            _priceInsightFunctions = priceInsightFunctions;
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
            var products = _productFunctions.GetAllProducts(pagination);
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

            var productInfo = await _productFunctions.CreateProductAsync(request, cancellationToken);
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

            var productInfo = await _productFunctions.UpdateProductAsync(productPublicId, request, cancellationToken);
            return Ok(ApiResponse<ProductInfo>.SuccessResponse(productInfo));
        }

        /// <summary>
        /// Always rejects: products live in a shared, global namespace, so deleting one would
        /// remove it (and the matching inventory items) for every family using it. Users remove
        /// their own inventory items or their product customization instead.
        /// </summary>
        [HttpDelete("{productPublicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
        public IActionResult DeleteProduct(Guid productPublicId)
        {
            Log.Information($"Rejected delete request for shared product {productPublicId}");
            return StatusCode(403, ApiResponse.ErrorResponse(ErrorCodes.ProductDeletionNotAllowed));
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
            var productInfo = await _productFunctions.ToggleFavoriteAsync(productPublicId, cancellationToken);
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
            var detailedProduct = _productFunctions.GetDetailedProductInfo(productPublicId);

            if (detailedProduct == null)
            {
                return NotFound(ApiResponse.ErrorResponse(ErrorCodes.ProductNotFound));
            }

            return Ok(ApiResponse<DetailedProductInfo>.SuccessResponse(detailedProduct));
        }

        /// <summary>
        /// Gets the full global stock history for a product (purchases, consumptions, and add/update/delete
        /// events) aggregated across all of its inventory items, newest first.
        /// </summary>
        [HttpGet("{productPublicId}/history")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<ProductHistoryEventInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductHistory(Guid productPublicId, CancellationToken cancellationToken)
        {
            var history = await _productFunctions.GetProductHistoryAsync(productPublicId, cancellationToken);

            if (history == null)
            {
                return NotFound(ApiResponse.ErrorResponse(ErrorCodes.ProductNotFound));
            }

            return Ok(ApiResponse<List<ProductHistoryEventInfo>>.SuccessResponse(history));
        }

        /// <summary>
        /// The allowed values for <c>days</c> on <see cref="GetPriceHistory"/>. A closed set, the
        /// same way <c>InsightsController</c>'s window parameters are: an unbounded (or merely
        /// large) window is an unbounded query, and every value here matches a real preset on the
        /// price chart, so there is no in-between value a client could usefully ask for. Longer
        /// than the insight charts' 30/90 on purpose - a price trend needs enough purchases of one
        /// product to be a trend at all, and a household buys any single product far less often
        /// than it consumes something.
        /// </summary>
        private static readonly int[] AllowedPriceHistoryWindowDays = [90, 180, 365];

        /// <summary>
        /// One product's purchase price history for the caller's household - per shopping location,
        /// normalized to a price per canonical unit so pack sizes are comparable, and grouped by
        /// currency. See <see cref="PriceInsightFunctions.GetPriceHistoryAsync"/> for the scope
        /// rule and every rule about what is dropped, grouped or picked.
        /// </summary>
        /// <remarks>
        /// Validation and the 401 follow <c>InsightsController</c>'s convention rather than the
        /// rest of this controller's, because this is an insight endpoint that happens to hang off
        /// the product route: <paramref name="days"/> is checked against
        /// <see cref="AllowedPriceHistoryWindowDays"/> before it can reach a query, and a session
        /// that does not resolve to a local user row is <b>401</b> rather than 200-with-nothing -
        /// an authenticated request whose user id is missing is an authentication problem, not an
        /// empty-but-valid dataset. A caller with no family is the legitimate case and passes
        /// straight through, since their own personal purchases are still theirs to see.
        /// <para>
        /// A product nobody in the household has bought - and an unknown or deleted product id -
        /// answers <b>200</b> with an empty history rather than 404. That is deliberate; see
        /// <see cref="PriceInsightFunctions.GetPriceHistoryAsync"/>.
        /// </para>
        /// </remarks>
        [HttpGet("{productPublicId:guid}/price-history")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<PriceHistoryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPriceHistory(Guid productPublicId, [FromQuery] int days, CancellationToken cancellationToken)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthUnauthorized));
            }

            if (!AllowedPriceHistoryWindowDays.Contains(days))
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var familyId = SessionInfo.GetFamilyId();
            var history = await _priceInsightFunctions.GetPriceHistoryAsync(userId.Value, familyId, productPublicId, days, cancellationToken);
            return Ok(ApiResponse<PriceHistoryResponse>.SuccessResponse(history));
        }

        /// <summary>
        /// Gets detailed information about all products including inventory items with pagination support.
        /// </summary>
        [HttpGet("detailed")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<DetailedProductInfo>>), StatusCodes.Status200OK)]
        public IActionResult GetAllDetailedProducts([FromQuery] PaginationRequest pagination)
        {
            var detailedProducts = _productFunctions.GetAllDetailedProductsForUser(pagination);
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
            var response = await _productFunctions.GetExpiringAndExpiredInventoryCountAsync(cancellationToken);
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

            var inventoryItemInfo = await _productFunctions.CreateInventoryItemAsync(request, cancellationToken);
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

            var inventoryItemInfo = await _productFunctions.QuickAddInventoryItemAsync(request, cancellationToken);
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

            var inventoryItemInfo = await _productFunctions.UpdateInventoryItemAsync(inventoryItemPublicId, request, cancellationToken);
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
            await _productFunctions.DeleteInventoryItemAsync(inventoryItemPublicId, cancellationToken);

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

            var inventoryItemInfo = await _productFunctions.ConsumeInventoryItemAsync(inventoryItemPublicId, request, cancellationToken);
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

            var response = await _productFunctions.SplitInventoryItemAsync(inventoryItemPublicId, request, cancellationToken);
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

            var inventoryItems = await _productFunctions.QuickAddMultipleInventoryItemsAsync(request, cancellationToken);
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

            var inventoryItems = await _productFunctions.MoveInventoryItemsAsync(request, cancellationToken);
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

            await _productFunctions.DeleteMultipleInventoryItemsAsync(request, cancellationToken);
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

            var items = await _productFunctions.ConsumeMultipleInventoryItemsAsync(request, cancellationToken);
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

            var products = await _productFunctions.CreateMultipleProductsAsync(request, cancellationToken);
            return Ok(ApiResponse<List<ProductInfo>>.SuccessResponse(products));
        }
        #endregion

        #region Product Image
        /// <summary>
        /// Serves a product's picture as image bytes.
        /// </summary>
        /// <remarks>
        /// The counterpart of the avatar endpoint on <c>UserController</c>, and the reason
        /// <c>ProductInfo</c> can carry a URL instead of the image: a product list used to
        /// download every picture inline, uncacheably, on every fetch and again after each
        /// realtime refresh.
        /// <para>
        /// The <c>v</c> query parameter is not read — it is the stored picture's content hash, so
        /// that a changed picture is a changed URL. That is what makes the long
        /// <c>Cache-Control</c> safe.
        /// </para>
        /// </remarks>
        /// <param name="publicId">The product whose picture to serve.</param>
        /// <param name="size">Which rendition: <c>thumb</c> (default, for cards) or <c>full</c>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{publicId:guid}/image")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductImage(
            Guid publicId,
            [FromQuery] ImageVariant size = ImageVariant.Thumb,
            CancellationToken cancellationToken = default)
        {
            var image = await _imageFunctions.GetProductImageAsync(publicId, size, this.AcceptsWebp(), cancellationToken);

            if (image == null)
            {
                return NotFound(ApiResponse.ErrorResponse(ErrorCodes.ProductNotFound));
            }

            return this.CacheableImage(image);
        }

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

            var imageInfo = await _imageFunctions.UploadProductImageAsync(uploadRequest, null, cancellationToken);
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

                    await _imageFunctions.UploadProductImageAsync(uploadRequest, progress, cancellationToken);
                    
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
            await _imageFunctions.DeleteProductImageAsync(productPublicId, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }
        #endregion
    }
}
