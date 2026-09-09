using Asp.Versioning;
using Homassy.API.Context;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers;

/// <summary>
/// Family-scoped inventory insight endpoints (charts and other aggregates) for the R5 "Insight"
/// milestone. Every endpoint here returns data for the caller's own family only.
/// </summary>
/// <remarks>
/// Deliberately a separate controller from <see cref="StatisticsController"/>, not an addition to
/// it: <see cref="StatisticsController"/> is class-level <c>[AllowAnonymous]</c> and serves public,
/// global platform counts. Hanging private family-scoped data off that controller would mean one
/// forgotten per-endpoint attribute leaks another family's inventory - so this controller is
/// <c>[Authorize]</c> at class level instead, the opposite default.
/// </remarks>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public class InsightsController : ControllerBase
{
    private readonly InsightFunctions _insightFunctions;

    public InsightsController(InsightFunctions insightFunctions)
    {
        _insightFunctions = insightFunctions;
    }

    /// <summary>
    /// The caller's family's current inventory (non-deleted, non-fully-consumed items), broken
    /// down by product category.
    /// </summary>
    /// <remarks>
    /// A caller with no family gets an empty <see cref="InventoryCompositionResponse"/> - zero
    /// slices, zero counts - rather than an error or, worse, an unscoped query that would answer
    /// with every family's combined data. That check happens here, before
    /// <see cref="InsightFunctions.GetInventoryCompositionAsync"/> - which takes a plain
    /// <see langword="int"/> - is ever called, so a missing family id can never reach a query.
    /// </remarks>
    [HttpGet("inventory-composition")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<InventoryCompositionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoryComposition(CancellationToken cancellationToken)
    {
        var familyId = SessionInfo.GetFamilyId();
        if (!familyId.HasValue)
        {
            return Ok(ApiResponse<InventoryCompositionResponse>.SuccessResponse(new InventoryCompositionResponse()));
        }

        var composition = await _insightFunctions.GetInventoryCompositionAsync(familyId.Value, cancellationToken);
        return Ok(ApiResponse<InventoryCompositionResponse>.SuccessResponse(composition));
    }
}
