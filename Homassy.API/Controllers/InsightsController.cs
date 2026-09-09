using Asp.Versioning;
using Homassy.API.Context;
using Homassy.API.Enums;
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
    /// A caller with no user id at all gets <b>401</b>, not 200 with an empty payload. This
    /// endpoint is <c>[Authorize]</c>, so the request did authenticate - a missing user id means
    /// the session could not be resolved to a local user row (see
    /// <see cref="SessionInfo.SetFromKratosSession"/>'s "doesn't exist locally yet" branch), which
    /// is an authentication problem, not a legitimately empty dataset. Answering 200 with zeroes
    /// would tell the client its household is empty when the server in fact does not know who is
    /// asking. Mirrors the in-repo precedent for exactly this condition:
    /// <see cref="UserController.SendTestPushNotification"/> / <see cref="UserController.SendTestEmail"/>.
    /// That check happens here, before <see cref="InsightFunctions.GetInventoryCompositionAsync"/>
    /// is ever called, so a missing user id can never reach a query.
    /// <para>
    /// A caller with no <em>family</em> is a different, legitimate case: they still have their own
    /// personal items to show, so only the missing-user-id case short-circuits to an error - a
    /// missing family id is passed straight through and simply drops the family half of the union,
    /// still answering 200 (empty if they also have no items, populated if they do).
    /// </para>
    /// </remarks>
    [HttpGet("inventory-composition")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<InventoryCompositionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoryComposition(CancellationToken cancellationToken)
    {
        var userId = SessionInfo.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthUnauthorized));
        }

        var familyId = SessionInfo.GetFamilyId();
        var composition = await _insightFunctions.GetInventoryCompositionAsync(userId.Value, familyId, cancellationToken);
        return Ok(ApiResponse<InventoryCompositionResponse>.SuccessResponse(composition));
    }

    /// <summary>
    /// The allowed values for <c>days</c> on <see cref="GetConsumptionSeries"/>. A closed set,
    /// not an arbitrary upper bound: an unbounded (or merely large) window is an unbounded query
    /// over <c>Activities</c>, and every value here has to match a real preset on the chart the
    /// frontend renders from this - there is no in-between value a client could usefully ask for.
    /// </summary>
    private static readonly int[] AllowedConsumptionWindowDays = [30, 90];

    /// <summary>
    /// A dense consumption time series for the caller - see
    /// <see cref="InsightFunctions.GetConsumptionSeriesAsync"/> for the scope rule (whole family
    /// vs. just this caller) and the timezone bucketing this endpoint depends on getting right.
    /// </summary>
    /// <remarks>
    /// A missing user id is <b>401</b>, for exactly the reason documented on
    /// <see cref="GetInventoryComposition"/> above - an authenticated request whose session does
    /// not resolve to a local user is an authentication problem, not an empty-but-valid dataset.
    /// <paramref name="days"/> and <paramref name="bucket"/> are validated here, before either
    /// ever reaches a query: <see cref="AllowedConsumptionWindowDays"/> is a closed set (never an
    /// arbitrary caller-supplied window), and <paramref name="bucket"/> must name one of
    /// <see cref="SeriesBucket"/>'s two values. Either failing is <b>400</b>, matching the
    /// codebase-wide convention of a typed <see cref="ErrorCodes"/> in an <see cref="ApiResponse"/>
    /// rather than the framework's own default model-binding error shape.
    /// </remarks>
    [HttpGet("consumption")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<ConsumptionSeriesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetConsumptionSeries([FromQuery] int days, [FromQuery] string? bucket, CancellationToken cancellationToken)
    {
        var userId = SessionInfo.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthUnauthorized));
        }

        if (!AllowedConsumptionWindowDays.Contains(days))
        {
            return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
        }

        SeriesBucket seriesBucket;
        if (string.Equals(bucket, "day", StringComparison.OrdinalIgnoreCase))
        {
            seriesBucket = SeriesBucket.Day;
        }
        else if (string.Equals(bucket, "week", StringComparison.OrdinalIgnoreCase))
        {
            seriesBucket = SeriesBucket.Week;
        }
        else
        {
            return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
        }

        var familyId = SessionInfo.GetFamilyId();
        var series = await _insightFunctions.GetConsumptionSeriesAsync(userId.Value, familyId, days, seriesBucket, cancellationToken);
        return Ok(ApiResponse<ConsumptionSeriesResponse>.SuccessResponse(series));
    }
}
