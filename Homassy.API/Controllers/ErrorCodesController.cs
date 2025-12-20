using Asp.Versioning;
using Homassy.API.Constants;
using Homassy.API.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers;

/// <summary>
/// Error code reference endpoints for API documentation.
/// </summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[AllowAnonymous]
public class ErrorCodesController : ControllerBase
{
    /// <summary>
    /// Gets all available error codes with their descriptions.
    /// Error codes are grouped by category (e.g., AUTH, USER, PRODUCT).
    /// </summary>
    [HttpGet]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ErrorCodeInfo>>), StatusCodes.Status200OK)]
    public IActionResult GetAllErrorCodes()
    {
        var errorCodes = ErrorCodeDescriptions.GetAllErrorCodes();
        return Ok(ApiResponse<IReadOnlyList<ErrorCodeInfo>>.SuccessResponse(errorCodes));
    }

    /// <summary>
    /// Gets error codes for a specific group.
    /// </summary>
    /// <param name="group">The error code group prefix (e.g., "AUTH", "USER", "PRODUCT", "VALIDATION").</param>
    [HttpGet("{group}")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ErrorCodeInfo>>), StatusCodes.Status200OK)]
    public IActionResult GetErrorCodesByGroup(string group)
    {
        var errorCodes = ErrorCodeDescriptions.GetErrorCodesByGroup(group);
        return Ok(ApiResponse<IReadOnlyList<ErrorCodeInfo>>.SuccessResponse(errorCodes));
    }
}
