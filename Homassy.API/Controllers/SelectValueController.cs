using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Select value endpoints for dropdown options.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class SelectValueController : ControllerBase
    {
        /// <summary>
        /// Gets select values for a specific type (units, currencies, time zones, etc.).
        /// </summary>
        [HttpGet("{type}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<SelectValue>>), StatusCodes.Status200OK)]
        public IActionResult GetSelectValues(SelectValueType type)
        {
            var selectValues = new SelectValueFunctions().GetSelectValues(type);
            return Ok(ApiResponse<List<SelectValue>>.SuccessResponse(selectValues));
        }
    }
}
