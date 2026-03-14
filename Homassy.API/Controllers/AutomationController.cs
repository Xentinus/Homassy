using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Automation;
using Homassy.API.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Automation rule management endpoints.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class AutomationController : ControllerBase
    {
        /// <summary>
        /// Gets all automation rules for the current user and family.
        /// </summary>
        [HttpGet]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<AutomationResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAutomations(CancellationToken cancellationToken)
        {
            var automations = await new AutomationFunctions().GetAutomationsAsync(cancellationToken);
            return Ok(ApiResponse<List<AutomationResponse>>.SuccessResponse(automations));
        }

        /// <summary>
        /// Gets a single automation rule by PublicId.
        /// </summary>
        [HttpGet("{publicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<AutomationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAutomation(Guid publicId, CancellationToken cancellationToken)
        {
            var automation = await new AutomationFunctions().GetAutomationAsync(publicId, cancellationToken);
            return Ok(ApiResponse<AutomationResponse>.SuccessResponse(automation));
        }

        /// <summary>
        /// Creates a new automation rule.
        /// </summary>
        [HttpPost]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<AutomationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateAutomation([FromBody] CreateAutomationRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var automation = await new AutomationFunctions().CreateAutomationAsync(request, cancellationToken);
            return Ok(ApiResponse<AutomationResponse>.SuccessResponse(automation));
        }

        /// <summary>
        /// Updates an existing automation rule.
        /// </summary>
        [HttpPut("{publicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<AutomationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateAutomation(Guid publicId, [FromBody] UpdateAutomationRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var automation = await new AutomationFunctions().UpdateAutomationAsync(publicId, request, cancellationToken);
            return Ok(ApiResponse<AutomationResponse>.SuccessResponse(automation));
        }

        /// <summary>
        /// Deletes an automation rule (soft delete).
        /// </summary>
        [HttpDelete("{publicId}")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAutomation(Guid publicId, CancellationToken cancellationToken)
        {
            await new AutomationFunctions().DeleteAutomationAsync(publicId, cancellationToken);

            Log.Information($"Automation {publicId} deleted successfully");
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Manually executes an automation rule (triggers consumption or confirms notification).
        /// </summary>
        [HttpPost("{publicId}/execute")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<AutomationExecutionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ExecuteAutomation(Guid publicId, [FromBody] ExecuteAutomationRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var execution = await new AutomationFunctions().ExecuteAutomationAsync(publicId, request, cancellationToken);
            return Ok(ApiResponse<AutomationExecutionResponse>.SuccessResponse(execution));
        }

        /// <summary>
        /// Gets execution history for an automation rule.
        /// </summary>
        [HttpGet("{publicId}/history")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<AutomationExecutionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetExecutionHistory(Guid publicId, CancellationToken cancellationToken)
        {
            var history = await new AutomationFunctions().GetExecutionHistoryAsync(publicId, cancellationToken);
            return Ok(ApiResponse<List<AutomationExecutionResponse>>.SuccessResponse(history));
        }
    }
}
