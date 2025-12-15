using System.Reflection;
using Homassy.API.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers;

/// <summary>
/// Version information endpoint.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[AllowAnonymous]
public class VersionController : ControllerBase
{
    /// <summary>
    /// Gets the current API version information including build details.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<VersionInfo>), StatusCodes.Status200OK)]
    public IActionResult GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        var parts = informationalVersion.Split('-');
        var versionPart = parts[0];
        var buildType = parts.Length > 1 ? parts[1] : "unknown";

        var versionSegments = versionPart.Split('.');
        var shortVersion = versionSegments.Length >= 2 
            ? $"{versionSegments[0]}.{versionSegments[1]}" 
            : versionPart;

        string buildDate = "unknown";

        if (versionSegments.Length >= 3)
        {
            var year = "20" + versionSegments[0];
            var monthDay = versionSegments[1].PadLeft(4, '0');
            var time = versionSegments[2].PadLeft(4, '0');

            var month = monthDay[..2];
            var day = monthDay[2..];
            var hour = time[..2];
            var minute = time[2..];

            buildDate = $"{year}-{month}-{day}T{hour}:{minute}:00";
        }

        var versionInfo = new VersionInfo
        {
            Version = informationalVersion,
            ShortVersion = shortVersion,
            BuildType = buildType,
            BuildDate = buildDate
        };

        return Ok(ApiResponse<VersionInfo>.SuccessResponse(versionInfo));
    }
}
