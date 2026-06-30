using Asp.Versioning;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.ExternalCalendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers
{
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class ExternalCalendarController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ExternalCalendarController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> GetExternalCalendars(CancellationToken cancellationToken)
        {
            var calendars = await new ExternalCalendarFunctions().GetExternalCalendarsAsync(cancellationToken);
            return Ok(ApiResponse<List<ExternalCalendarResponse>>.SuccessResponse(calendars));
        }

        [HttpPost]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> CreateExternalCalendar(
            [FromBody] CreateExternalCalendarRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.ErrorResponse(Enums.ErrorCodes.ValidationInvalidRequest));

            var httpClient = _httpClientFactory.CreateClient("ExternalCalendarSync");
            var calendar = await new ExternalCalendarFunctions()
                .CreateExternalCalendarAsync(request, httpClient, cancellationToken);

            return Ok(ApiResponse<ExternalCalendarResponse>.SuccessResponse(calendar));
        }

        [HttpPut("{publicId:guid}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> UpdateExternalCalendar(
            Guid publicId,
            [FromBody] UpdateExternalCalendarRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.ErrorResponse(Enums.ErrorCodes.ValidationInvalidRequest));

            var httpClient = _httpClientFactory.CreateClient("ExternalCalendarSync");
            var calendar = await new ExternalCalendarFunctions()
                .UpdateExternalCalendarAsync(publicId, request, httpClient, cancellationToken);

            return Ok(ApiResponse<ExternalCalendarResponse>.SuccessResponse(calendar));
        }

        [HttpDelete("{publicId:guid}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteExternalCalendar(Guid publicId, CancellationToken cancellationToken)
        {
            await new ExternalCalendarFunctions().DeleteExternalCalendarAsync(publicId, cancellationToken);
            return Ok(ApiResponse.SuccessResponse("External calendar deleted"));
        }

        [HttpPost("{publicId:guid}/sync")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> SyncExternalCalendar(Guid publicId, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient("ExternalCalendarSync");
            var calendar = await new ExternalCalendarFunctions()
                .TriggerSyncAsync(publicId, httpClient, cancellationToken);

            return Ok(ApiResponse<ExternalCalendarResponse>.SuccessResponse(calendar));
        }
    }
}
