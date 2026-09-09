using Asp.Versioning;
using Homassy.API.Context;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers;

/// <summary>
/// Inventory insight endpoints (charts and other aggregates) for the R5 "Insight" milestone.
/// Every endpoint here returns data scoped to the caller only - their own personal items plus
/// their family's shared items - never another family's, and never a blend of two families'.
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
    /// The caller's current inventory (non-deleted, non-fully-consumed items) - their own
    /// personal items plus their family's shared items - broken down by product category.
    /// </summary>
    /// <remarks>
    /// A caller with no user id at all gets an empty <see cref="InventoryCompositionResponse"/> -
    /// zero slices, zero counts - rather than an error or, worse, an unscoped query that would
    /// answer with every family's combined data. That check happens here, before
    /// <see cref="InsightFunctions.GetInventoryCompositionAsync"/> is ever called, so a missing
    /// user id can never reach a query. A caller with no family is different: they still have
    /// their own personal items to show, so only the missing-user-id case short-circuits - a
    /// missing family id is passed straight through and simply drops the family half of the union.
    /// </remarks>
    [HttpGet("inventory-composition")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<InventoryCompositionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoryComposition(CancellationToken cancellationToken)
    {
        var userId = SessionInfo.GetUserId();
        if (!userId.HasValue)
        {
            return Ok(ApiResponse<InventoryCompositionResponse>.SuccessResponse(new InventoryCompositionResponse()));
        }

        var familyId = SessionInfo.GetFamilyId();
        var composition = await _insightFunctions.GetInventoryCompositionAsync(userId.Value, familyId, cancellationToken);
        return Ok(ApiResponse<InventoryCompositionResponse>.SuccessResponse(composition));
    }
}
