using Asp.Versioning;
using Homassy.API.Models.Common;
using Homassy.API.Models.Statistics;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers;

/// <summary>
/// Public statistics endpoint returning nightly-cached global platform counts.
/// </summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[AllowAnonymous]
public class StatisticsController : ControllerBase
{
    private readonly StatisticsService _statisticsService;

    public StatisticsController(StatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    /// <summary>
    /// Returns the nightly-cached global statistics for the platform.
    /// The cache is refreshed once per day by the background worker.
    /// </summary>
    [HttpGet]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<GlobalStatisticsResponse>), StatusCodes.Status200OK)]
    public IActionResult GetStatistics()
    {
        var statistics = _statisticsService.GetStatistics();
        return Ok(ApiResponse<GlobalStatisticsResponse>.SuccessResponse(statistics));
    }
}
