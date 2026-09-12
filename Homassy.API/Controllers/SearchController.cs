using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Global search across every entity type the caller can see — the command palette's
    /// single endpoint.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly SearchFunctions _searchFunctions;

        public SearchController(SearchFunctions searchFunctions)
        {
            _searchFunctions = searchFunctions;
        }

        /// <summary>
        /// Searches products, inventory items, shopping lists, shopping locations, storage
        /// locations and automations at once, returning a small ranked set per type.
        /// </summary>
        /// <param name="q">The search term. Shorter than two characters returns no groups.</param>
        /// <param name="limit">Hits per type, 1–20.</param>
        /// <param name="cancellationToken">Cancels the automation lookup when the caller gives up.</param>
        [HttpGet]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<GlobalSearchResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] int limit = SearchFunctions.DefaultLimit,
            CancellationToken cancellationToken = default)
        {
            if (q is { Length: > 128 })
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));

            var results = await _searchFunctions.SearchAsync(q ?? string.Empty, limit, cancellationToken);
            return Ok(ApiResponse<GlobalSearchResponse>.SuccessResponse(results));
        }
    }
}
