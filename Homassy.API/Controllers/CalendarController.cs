using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Calendar;
using Homassy.API.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Calendar event aggregation endpoints.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class CalendarController : ControllerBase
    {
        private const int MaxDateRangeDays = 93;

        /// <summary>
        /// Gets all calendar events (inventory expirations, automation executions, shopping list deadlines)
        /// within the specified date range.
        /// </summary>
        [HttpPost]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<CalendarEventInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCalendarEvents(
            [FromBody] GetCalendarEventsRequest request,
            CancellationToken cancellationToken)
        {
            if (request.EndDate < request.StartDate ||
                (request.EndDate.DayNumber - request.StartDate.DayNumber) > MaxDateRangeDays)
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));

            var events = await new CalendarFunctions().GetCalendarEventsAsync(
                DateTime.SpecifyKind(request.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                DateTime.SpecifyKind(request.EndDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc),
                cancellationToken);
            return Ok(ApiResponse<List<CalendarEventInfo>>.SuccessResponse(events));
        }
    }
}
