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
        private readonly ExternalCalendarFunctions _externalCalendarFunctions;

        public ExternalCalendarController(
            IHttpClientFactory httpClientFactory,
            ExternalCalendarFunctions externalCalendarFunctions)
        {
            _httpClientFactory = httpClientFactory;
            _externalCalendarFunctions = externalCalendarFunctions;
        }

        [HttpGet]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> GetExternalCalendars(CancellationToken cancellationToken)
        {
            var calendars = await _externalCalendarFunctions.GetExternalCalendarsAsync(cancellationToken);
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
            var calendar = await _externalCalendarFunctions
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
            var calendar = await _externalCalendarFunctions
                .UpdateExternalCalendarAsync(publicId, request, httpClient, cancellationToken);

            return Ok(ApiResponse<ExternalCalendarResponse>.SuccessResponse(calendar));
        }

        [HttpDelete("{publicId:guid}")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> DeleteExternalCalendar(Guid publicId, CancellationToken cancellationToken)
        {
            await _externalCalendarFunctions.DeleteExternalCalendarAsync(publicId, cancellationToken);
            return Ok(ApiResponse.SuccessResponse("External calendar deleted"));
        }

        [HttpPost("{publicId:guid}/sync")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> SyncExternalCalendar(Guid publicId, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient("ExternalCalendarSync");
            var calendar = await _externalCalendarFunctions
                .TriggerSyncAsync(publicId, httpClient, cancellationToken);

            return Ok(ApiResponse<ExternalCalendarResponse>.SuccessResponse(calendar));
        }
    }
}
