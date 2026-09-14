using Asp.Versioning;
using Homassy.Data.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Calendar;
using Homassy.Data.Models.Common;
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
        private readonly CalendarFunctions _calendarFunctions;
        private readonly CalendarNoteFunctions _calendarNoteFunctions;

        public CalendarController(CalendarFunctions calendarFunctions, CalendarNoteFunctions calendarNoteFunctions)
        {
            _calendarFunctions = calendarFunctions;
            _calendarNoteFunctions = calendarNoteFunctions;
        }

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

            var events = await _calendarFunctions.GetCalendarEventsAsync(
                DateTime.SpecifyKind(request.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                DateTime.SpecifyKind(request.EndDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc),
                cancellationToken);
            return Ok(ApiResponse<List<CalendarEventInfo>>.SuccessResponse(events));
        }

        /// <summary>
        /// Gets the family's day notes in a date range. Notes also arrive inside
        /// <see cref="GetCalendarEvents"/>; this endpoint is what the note panel reads when it needs
        /// the full note (its text, its reminder, who wrote it) rather than the calendar projection.
        /// </summary>
        [HttpPost("notes/range")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<CalendarNoteInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCalendarNotes(
            [FromBody] GetCalendarNotesRequest request,
            CancellationToken cancellationToken)
        {
            if (request.EndDate < request.StartDate ||
                (request.EndDate.DayNumber - request.StartDate.DayNumber) > MaxDateRangeDays)
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));

            var notes = await _calendarNoteFunctions.GetNotesAsync(request.StartDate, request.EndDate, cancellationToken);
            return Ok(ApiResponse<List<CalendarNoteInfo>>.SuccessResponse(notes));
        }

        /// <summary>
        /// Creates a day note for the caller's family.
        /// </summary>
        [HttpPost("notes")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<CalendarNoteInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCalendarNote(
            [FromBody] CreateCalendarNoteRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var note = await _calendarNoteFunctions.CreateNoteAsync(request, cancellationToken);
            return Ok(ApiResponse<CalendarNoteInfo>.SuccessResponse(note));
        }

        /// <summary>
        /// Updates a day note. Any family member may edit any of the family's notes.
        /// </summary>
        [HttpPut("notes/{publicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<CalendarNoteInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCalendarNote(
            Guid publicId,
            [FromBody] UpdateCalendarNoteRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var note = await _calendarNoteFunctions.UpdateNoteAsync(publicId, request, cancellationToken);
            return Ok(ApiResponse<CalendarNoteInfo>.SuccessResponse(note));
        }

        /// <summary>
        /// Deletes a day note.
        /// </summary>
        [HttpDelete("notes/{publicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteCalendarNote(Guid publicId, CancellationToken cancellationToken)
        {
            await _calendarNoteFunctions.DeleteNoteAsync(publicId, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }
    }
}
