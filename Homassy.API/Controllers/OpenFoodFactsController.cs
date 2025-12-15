using Asp.Versioning;
using Homassy.API.Models.Common;
using Homassy.API.Models.OpenFoodFacts;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers;

/// <summary>
/// Open Food Facts integration endpoints.
/// </summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public class OpenFoodFactsController : ControllerBase
{
    private readonly OpenFoodFactsService _openFoodFactsService;

    public OpenFoodFactsController(OpenFoodFactsService openFoodFactsService)
    {
        _openFoodFactsService = openFoodFactsService;
    }

    /// <summary>
    /// Gets product information from Open Food Facts database by barcode.
    /// </summary>
    /// <param name="barcode">The barcode of the product.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>An IActionResult containing the product information or an error response.</returns>
    [HttpGet("{barcode}")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<OpenFoodFactsProduct>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductByBarcode(string barcode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return BadRequest(ApiResponse.ErrorResponse("Barcode is required"));
        }

        var result = await _openFoodFactsService.GetProductByBarcodeAsync(barcode, cancellationToken);

        if (result == null || result.Status != 1 || result.Product == null)
        {
            return NotFound(ApiResponse.ErrorResponse("Product not found in Open Food Facts database"));
        }

        return Ok(ApiResponse<OpenFoodFactsProduct>.SuccessResponse(result.Product));
    }
}
