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
    private readonly PriceInsightFunctions _priceInsightFunctions;

    public InsightsController(InsightFunctions insightFunctions, PriceInsightFunctions priceInsightFunctions)
    {
        _insightFunctions = insightFunctions;
        _priceInsightFunctions = priceInsightFunctions;
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

    /// <summary>
    /// The allowed values for <c>days</c> on <see cref="GetSpendByLocation"/> - the same closed
    /// set <see cref="AllowedConsumptionWindowDays"/> uses on <see cref="GetConsumptionSeries"/>,
    /// and for the identical reason: an unbounded (or merely large) window is an unbounded query.
    /// </summary>
    private static readonly int[] AllowedSpendByLocationWindowDays = [30, 90];

    /// <summary>
    /// The caller's purchases within the requested window, broken down by shopping location and,
    /// per location, by currency - see <see cref="InsightFunctions.GetSpendByLocationAsync"/> for
    /// the scope rule and the three data rules (no currency conversion, the unknown-location
    /// fold, the null-price fold) this endpoint exists to get right.
    /// </summary>
    /// <remarks>
    /// A missing user id is <b>401</b>, for exactly the reason documented on
    /// <see cref="GetInventoryComposition"/> above - an authenticated request whose session does
    /// not resolve to a local user is an authentication problem, not an empty-but-valid dataset.
    /// <paramref name="days"/> is validated against <see cref="AllowedSpendByLocationWindowDays"/>
    /// before it ever reaches a query, matching <see cref="GetConsumptionSeries"/>'s own
    /// validation of its own window parameter.
    /// </remarks>
    [HttpGet("spend-by-location")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<SpendByLocationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSpendByLocation([FromQuery] int days, CancellationToken cancellationToken)
    {
        var userId = SessionInfo.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthUnauthorized));
        }

        if (!AllowedSpendByLocationWindowDays.Contains(days))
        {
            return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
        }

        var familyId = SessionInfo.GetFamilyId();
        var spend = await _insightFunctions.GetSpendByLocationAsync(userId.Value, familyId, days, cancellationToken);
        return Ok(ApiResponse<SpendByLocationResponse>.SuccessResponse(spend));
    }

    /// <summary>
    /// The allowed values for <c>days</c> on <see cref="GetScoreboard"/> - a closed set, for the
    /// same reason the other windows here are. 7 is offered on top of the usual 30/90 because a
    /// leaderboard over a week is a genuinely different (and more motivating) question than one
    /// over a quarter, which is not true of the inventory charts.
    /// </summary>
    private static readonly int[] AllowedScoreboardWindowDays = [7, 30, 90];

    /// <summary>
    /// The family's scoreboard: per-member counters over the window with a comparison against the
    /// previous equally-long one, plus the household's no-expiry and list-cleared streaks. See
    /// <see cref="InsightFunctions.GetFamilyScoreboardAsync"/> for how each number is computed.
    /// </summary>
    /// <remarks>
    /// A missing user id is <b>401</b>, for the reason documented on
    /// <see cref="GetInventoryComposition"/>. A caller with no <em>family</em> is different and
    /// legitimate: they get <b>200</b> with an empty scoreboard, since there is no household to
    /// rank and that is an answer rather than an error.
    /// </remarks>
    [HttpGet("scoreboard")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<FamilyScoreboardResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetScoreboard([FromQuery] int days, CancellationToken cancellationToken)
    {
        var userId = SessionInfo.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthUnauthorized));
        }

        if (!AllowedScoreboardWindowDays.Contains(days))
        {
            return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
        }

        var familyId = SessionInfo.GetFamilyId();
        var scoreboard = await _insightFunctions.GetFamilyScoreboardAsync(userId.Value, familyId, days, cancellationToken);
        return Ok(ApiResponse<FamilyScoreboardResponse>.SuccessResponse(scoreboard));
    }

    /// <summary>
    /// Every badge in the catalog as the caller currently stands with it - locked ones with their
    /// truthful progress, earned ones with the date they were earned. See
    /// <see cref="InsightFunctions.GetBadgeStateAsync"/> for the evaluation and for why exactly one
    /// response per badge reports <see cref="BadgeState.JustUnlocked"/>.
    /// </summary>
    /// <remarks>
    /// A <b>GET that writes</b>, which is worth being explicit about: crossing a threshold has to
    /// be recorded the moment it is first observed, or the unlock celebration cannot fire exactly
    /// once. The write is idempotent in effect (a unique index makes a second insert impossible)
    /// and only ever adds - nothing here revokes a badge - so a retried request cannot double-grant
    /// and cannot lose anything. It is deliberately not cached, for the same reason.
    /// <para>
    /// No <c>days</c> parameter: badges are lifetime achievements, not a window.
    /// </para>
    /// </remarks>
    [HttpGet("badges")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<BadgeStateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBadges(CancellationToken cancellationToken)
    {
        var userId = SessionInfo.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthUnauthorized));
        }

        var familyId = SessionInfo.GetFamilyId();
        var badges = await _insightFunctions.GetBadgeStateAsync(userId.Value, familyId, cancellationToken);
        return Ok(ApiResponse<BadgeStateResponse>.SuccessResponse(badges));
    }

    /// <summary>
    /// The cheapest price the caller's household has paid for each of the requested products -
    /// one call for a whole shopping list, never one per row. Products with no usable purchase
    /// history are <b>omitted</b> from the map rather than returned with a null value; see
    /// <see cref="PriceInsightFunctions.GetBestPricesAsync"/>.
    /// </summary>
    /// <remarks>
    /// A <b>POST</b> for a read, because the request's payload is a list that would not fit a
    /// query string for a long shopping list - see <see cref="BestPricesRequest"/>.
    /// <para>
    /// Three rejections happen before anything reaches a query: a session with no resolvable user
    /// id is <b>401</b> (the reason is documented on <see cref="GetInventoryComposition"/>), a
    /// malformed body is <b>400</b>, and more than
    /// <see cref="PriceInsightFunctions.MaxBestPriceProductIds"/> ids is <b>400</b> - an unbounded
    /// id list is an unbounded query, and truncating it silently would answer a different question
    /// than the one asked. An <em>empty</em> list is not an error: it answers an empty map, and
    /// does so without querying at all.
    /// </para>
    /// </remarks>
    [HttpPost("best-prices")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyDictionary<Guid, BestKnownPrice>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBestPrices([FromBody] BestPricesRequest request, CancellationToken cancellationToken)
    {
        var userId = SessionInfo.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthUnauthorized));
        }

        if (!ModelState.IsValid || request.ProductPublicIds.Count > PriceInsightFunctions.MaxBestPriceProductIds)
        {
            return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
        }

        var familyId = SessionInfo.GetFamilyId();
        var bestPrices = await _priceInsightFunctions.GetBestPricesAsync(userId.Value, familyId, request.ProductPublicIds, cancellationToken);
        return Ok(ApiResponse<IReadOnlyDictionary<Guid, BestKnownPrice>>.SuccessResponse(bestPrices));
    }
}
