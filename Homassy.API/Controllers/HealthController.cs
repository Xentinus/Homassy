using Asp.Versioning;
using Homassy.API.Models.Common;
using Homassy.API.Models.HealthCheck;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

namespace Homassy.API.Controllers;

/// <summary>
/// Health check endpoints for monitoring application and dependency status.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Basic health check endpoint. Returns overall application status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthCheckResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HealthCheckResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealthAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);
        stopwatch.Stop();

        var response = CreateHealthCheckResponse(report, stopwatch.Elapsed);
        var statusCode = report.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return StatusCode(statusCode, ApiResponse<HealthCheckResponse>.SuccessResponse(response));
    }

    /// <summary>
    /// Readiness probe endpoint. Checks if application is ready to receive traffic (database).
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(typeof(ApiResponse<HealthCheckResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HealthCheckResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReadinessAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = await _healthCheckService.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"),
            cancellationToken);
        stopwatch.Stop();

        var response = CreateHealthCheckResponse(report, stopwatch.Elapsed);
        var statusCode = report.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return StatusCode(statusCode, ApiResponse<HealthCheckResponse>.SuccessResponse(response));
    }

    /// <summary>
    /// Liveness probe endpoint. Simple check to verify the application process is running.
    /// </summary>
    [HttpGet("live")]
    [ProducesResponseType(typeof(ApiResponse<HealthCheckResponse>), StatusCodes.Status200OK)]
    public IActionResult GetLiveness()
    {
        var response = new HealthCheckResponse
        {
            Status = "Healthy",
            Duration = "0ms",
            Dependencies = new Dictionary<string, DependencyHealth>()
        };

        return Ok(ApiResponse<HealthCheckResponse>.SuccessResponse(response));
    }

    private static HealthCheckResponse CreateHealthCheckResponse(HealthReport report, TimeSpan duration)
    {
        var dependencies = new Dictionary<string, DependencyHealth>();

        foreach (var entry in report.Entries)
        {
            dependencies[entry.Key] = new DependencyHealth
            {
                Status = entry.Value.Status.ToString(),
                Duration = $"{entry.Value.Duration.TotalMilliseconds:F0}ms",
                Description = entry.Value.Description,
                Data = entry.Value.Data.Count > 0
                    ? entry.Value.Data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    : null
            };
        }

        return new HealthCheckResponse
        {
            Status = report.Status.ToString(),
            Duration = $"{duration.TotalMilliseconds:F0}ms",
            Dependencies = dependencies
        };
    }
}
