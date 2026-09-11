using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using Homassy.API.Hubs;
using Homassy.API.Models.Common;
using Homassy.API.Models.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// Service-to-service endpoints called by trusted internal services only (never the frontend).
    /// Authenticated by a shared secret in the <c>X-Api-Key</c> header (validated against
    /// <c>InternalApi:ApiKey</c>), not by a Kratos session — so these actions are <see cref="AllowAnonymousAttribute"/>
    /// and gated by the key check instead.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class InternalController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly InventoryRealtime _inventoryRealtime;
        private readonly FamilyChatConnectionState _chatConnectionState;

        public InternalController(
            IConfiguration configuration,
            InventoryRealtime inventoryRealtime,
            FamilyChatConnectionState chatConnectionState)
        {
            _configuration = configuration;
            _inventoryRealtime = inventoryRealtime;
            _chatConnectionState = chatConnectionState;
        }

        /// <summary>
        /// Answers which of the given users is currently watching their family chat (#149).
        /// </summary>
        /// <remarks>
        /// The flags live in this process's memory, because this is where the hub is; the worker
        /// that decides whether to send a chat notification runs in <c>Homassy.Notifications</c>.
        /// Rather than move the state, the question crosses the boundary - the same shape the
        /// inventory broadcast relay already uses, in the other direction.
        /// <para>
        /// Being flagged active means the panel is open <em>and</em> the document is visible, as
        /// reported by the client and kept alive by a heartbeat. It is not "connected": the chat
        /// socket is an app-wide singleton and a backgrounded tab keeps a WebSocket alive.
        /// </para>
        /// <para>
        /// A missing answer fails safe. With more than one API instance this one only knows its own
        /// connections (the #68 backplane caveat), so an unseen flag means one extra notification -
        /// never a lost one.
        /// </para>
        /// </remarks>
        [HttpPost("family-chat/active-users")]
        [MapToApiVersion(1.0)]
        public IActionResult GetActiveChatUsers([FromBody] FamilyChatActiveUsersRequest request)
        {
            if (!IsAuthorized())
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid API key"));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request"));
            }

            var now = DateTime.UtcNow;
            var active = request.UserIds
                .Distinct()
                .Where(id => _chatConnectionState.IsUserActive(id, now))
                .ToList();

            return Ok(ApiResponse<FamilyChatActiveUsersResponse>.SuccessResponse(
                new FamilyChatActiveUsersResponse { ActiveUserIds = active }));
        }

        /// <summary>
        /// Relays an inventory change (originating in another process, e.g. an automation worker) to the
        /// SignalR inventory groups so connected grids update live.
        /// </summary>
        [HttpPost("inventory/broadcast")]
        [MapToApiVersion(1.0)]
        public async Task<IActionResult> BroadcastInventory([FromBody] InventoryBroadcastRequest request, CancellationToken cancellationToken)
        {
            if (!IsAuthorized())
            {
                return Unauthorized(ApiResponse.ErrorResponse("Invalid API key"));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request"));
            }

            switch (request.EventType?.ToLowerInvariant())
            {
                case "upserted":
                    if (request.Product == null || request.Item == null)
                    {
                        return BadRequest(ApiResponse.ErrorResponse("Product and Item are required for an upserted event"));
                    }
                    await _inventoryRealtime.InventoryUpsertedAsync(
                        request.UserId, request.FamilyId, request.Product, request.Item, cancellationToken);
                    break;

                case "deleted":
                    if (!request.ProductPublicId.HasValue || !request.ItemPublicId.HasValue)
                    {
                        return BadRequest(ApiResponse.ErrorResponse("ProductPublicId and ItemPublicId are required for a deleted event"));
                    }
                    await _inventoryRealtime.InventoryDeletedAsync(
                        request.UserId, request.FamilyId, request.SharedWithFamily,
                        request.ProductPublicId.Value, request.ItemPublicId.Value, cancellationToken);
                    break;

                default:
                    return BadRequest(ApiResponse.ErrorResponse($"Unknown event type '{request.EventType}'"));
            }

            return Ok(ApiResponse.SuccessResponse("Broadcast relayed"));
        }

        private bool IsAuthorized()
        {
            var configuredKey = _configuration["InternalApi:ApiKey"];
            if (string.IsNullOrEmpty(configuredKey))
            {
                Log.Error("InternalApi:ApiKey is not configured; rejecting internal broadcast request");
                return false;
            }

            if (!Request.Headers.TryGetValue("X-Api-Key", out var receivedKeyHeader))
            {
                return false;
            }

            var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
            var receivedBytes = Encoding.UTF8.GetBytes(receivedKeyHeader.ToString());

            // Pad to equal length so the comparison time doesn't leak the key length.
            var maxLen = Math.Max(configuredBytes.Length, receivedBytes.Length);
            var paddedConfigured = new byte[maxLen];
            var paddedReceived = new byte[maxLen];
            configuredBytes.CopyTo(paddedConfigured, 0);
            receivedBytes.CopyTo(paddedReceived, 0);

            return CryptographicOperations.FixedTimeEquals(paddedConfigured, paddedReceived);
        }
    }
}
