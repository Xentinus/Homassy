using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Models;
using Homassy.API.Models.Common;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Progress tracking endpoints for long-running operations.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class ProgressController : ControllerBase
    {
        private readonly IProgressTrackerService _progressTrackerService;

        public ProgressController(IProgressTrackerService progressTrackerService)
        {
            _progressTrackerService = progressTrackerService;
        }

        /// <summary>
        /// Gets the progress status of a job.
        /// </summary>
        [HttpGet("{jobId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<ProgressResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public IActionResult GetProgress(Guid jobId)
        {
            var progress = _progressTrackerService.GetProgress(jobId);
            
            if (progress == null)
            {
                return NotFound(ApiResponse.ErrorResponse(ErrorCodes.ProgressJobNotFound));
            }

            var response = new ProgressResponse
            {
                JobId = progress.JobId,
                Percentage = progress.Percentage,
                Stage = progress.Stage.ToString().ToLowerInvariant(),
                Status = progress.Status.ToString().ToLowerInvariant(),
                ErrorMessage = progress.ErrorMessage
            };

            return Ok(ApiResponse<ProgressResponse>.SuccessResponse(response));
        }

        /// <summary>
        /// Cancels a running job.
        /// </summary>
        [HttpDelete("{jobId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public IActionResult CancelJob(Guid jobId)
        {
            var progress = _progressTrackerService.GetProgress(jobId);
            
            if (progress == null)
            {
                return NotFound(ApiResponse.ErrorResponse(ErrorCodes.ProgressJobNotFound));
            }

            _progressTrackerService.CancelJob(jobId);
            return Ok(ApiResponse.SuccessResponse());
        }
    }
}
