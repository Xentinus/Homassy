using Asp.Versioning;
using Homassy.API.Constants;
using Homassy.API.Functions;
using Homassy.API.Models.CalendarNote;
using Homassy.API.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Family-shared day notes on the calendar.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class CalendarNoteController : ControllerBase
    {
        private const int MaxDateRangeDays = CalendarConstants.MaxDateRangeDays;

        /// <summary>
        /// Gets the family's notes for a date range. A user with no family gets an empty list, not an error.
        /// </summary>
        /// <remarks>
        /// A GET with query params rather than <c>CalendarController</c>'s POST-with-body: this controller needs
        /// <c>POST /</c> for creating a note, and a hot range read gets compression and caching for free over GET.
        /// </remarks>
        [HttpGet]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<CalendarNoteResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCalendarNotes(
            [FromQuery] GetCalendarNotesRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.ErrorResponse(Enums.ErrorCodes.ValidationInvalidRequest));

            if (request.StartDate is not { } startDate || request.EndDate is not { } endDate)
                return BadRequest(ApiResponse.ErrorResponse(Enums.ErrorCodes.ValidationInvalidRequest));

            if (endDate < startDate || (endDate.DayNumber - startDate.DayNumber) > MaxDateRangeDays)
                return BadRequest(ApiResponse.ErrorResponse(Enums.ErrorCodes.ValidationInvalidRequest));

            var notes = await new CalendarNoteFunctions().GetCalendarNotesAsync(startDate, endDate, cancellationToken);
            return Ok(ApiResponse<List<CalendarNoteResponse>>.SuccessResponse(notes));
        }

        [HttpPost]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<CalendarNoteResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCalendarNote(
            [FromBody] CreateCalendarNoteRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.ErrorResponse(Enums.ErrorCodes.ValidationInvalidRequest));

            var note = await new CalendarNoteFunctions().CreateCalendarNoteAsync(request, cancellationToken);
            return Ok(ApiResponse<CalendarNoteResponse>.SuccessResponse(note));
        }

        [HttpPut("{publicId:guid}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<CalendarNoteResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateCalendarNote(
            Guid publicId,
            [FromBody] UpdateCalendarNoteRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.ErrorResponse(Enums.ErrorCodes.ValidationInvalidRequest));

            var note = await new CalendarNoteFunctions().UpdateCalendarNoteAsync(publicId, request, cancellationToken);
            return Ok(ApiResponse<CalendarNoteResponse>.SuccessResponse(note));
        }

        [HttpDelete("{publicId:guid}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteCalendarNote(Guid publicId, CancellationToken cancellationToken)
        {
            await new CalendarNoteFunctions().DeleteCalendarNoteAsync(publicId, cancellationToken);
            return Ok(ApiResponse.SuccessResponse("Calendar note deleted"));
        }
    }
}
