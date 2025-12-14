using Asp.Versioning;
using Homassy.API.Models.Common;
using Homassy.API.Models.OpenFoodFacts;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers;

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

    [HttpGet("{barcode}")]
    [MapToApiVersion(1.0)]
    public async Task<IActionResult> GetProductByBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return BadRequest(ApiResponse.ErrorResponse("Barcode is required"));
        }

        try
        {
            var result = await _openFoodFactsService.GetProductByBarcodeAsync(barcode);

            if (result == null || result.Status != 1 || result.Product == null)
            {
                return NotFound(ApiResponse.ErrorResponse("Product not found in Open Food Facts database"));
            }

            return Ok(ApiResponse<OpenFoodFactsProduct>.SuccessResponse(result.Product));
        }
        catch (Exception ex)
        {
            Log.Error($"Error retrieving product from Open Food Facts for barcode {barcode}: {ex.Message}");
            return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving product from Open Food Facts"));
        }
    }
}
