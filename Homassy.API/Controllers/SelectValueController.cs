using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class SelectValueController : ControllerBase
    {
        /// <summary>
        /// Retrieves select values for the specified entity type.
        /// Returns items accessible by the current user (by userId or familyId).
        /// </summary>
        /// <param name="type">The type of entity to retrieve select values for.</param>
        /// <returns>A list of SelectValue containing PublicId and display text.</returns>
        [HttpGet("{type}")]
        [MapToApiVersion(1.0)]
        public IActionResult GetSelectValues(SelectValueType type)
        {
            try
            {
                var selectValues = new SelectValueFunctions().GetSelectValues(type);
                return Ok(ApiResponse<List<SelectValue>>.SuccessResponse(selectValues));
            }
            catch (UserNotFoundException ex)
            {
                return NotFound(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error retrieving select values for type {type}: {ex.Message}");
                return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving select values"));
            }
        }
    }
}
