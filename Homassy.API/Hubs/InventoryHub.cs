using Homassy.API.Context;
using Homassy.API.Functions;
using Homassy.API.Middleware;
using Homassy.API.Models.Kratos;
using Homassy.API.Models.Product;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Realtime channel for the Készletek (inventory) grid. Unlike shopping lists (one group per list),
    /// inventory has no single parent the user "opens" — the grid shows the whole family/personal
    /// inventory at once. So groups are derived from identity: each connection joins its own user group
    /// (<see cref="InventoryRealtime.UserGroup"/>) and, if the user belongs to a family, that family's
    /// group (<see cref="InventoryRealtime.FamilyGroup"/>). This matches the grid's visibility filter
    /// (<c>item.UserId == me || item.FamilyId == myFamily</c>) and means a family-less user still has a
    /// group, so automation-driven changes to a lone user's inventory can be pushed.
    ///
    /// Authentication reuses the existing pipeline exactly as <see cref="ShoppingListHub"/>: the Kratos
    /// session cookie rides the WebSocket handshake, <see cref="KratosSessionMiddleware"/> validates it,
    /// and the captured <see cref="KratosSession"/> is replayed into <see cref="SessionInfo"/> so the
    /// existing <see cref="ProductFunctions"/> can be reused.
    /// </summary>
    [Authorize]
    public class InventoryHub : Hub
    {
        private const string SessionItemKey = "KratosSession";

        public override async Task OnConnectedAsync()
        {
            // KratosSessionMiddleware ran during the handshake and stashed the session on the
            // HttpContext; capture it for the lifetime of this connection.
            var session = Context.GetHttpContext()?.GetKratosSession();
            if (session != null)
            {
                Context.Items[SessionItemKey] = session;

                // Join the identity-derived groups. OnConnectedAsync also fires after an automatic
                // reconnect (a new connection), so group membership is re-established transparently.
                try
                {
                    SessionInfo.SetFromKratosSession(session);
                    var userId = SessionInfo.GetUserId();
                    var familyId = SessionInfo.GetFamilyId();

                    if (userId.HasValue)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, InventoryRealtime.UserGroup(userId.Value));
                    }

                    if (familyId.HasValue)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, InventoryRealtime.FamilyGroup(familyId.Value));
                    }

                    Log.Debug("Connection {ConnectionId} joined inventory groups (user {UserId}, family {FamilyId})",
                        Context.ConnectionId, userId, familyId);
                }
                finally
                {
                    SessionInfo.Clear();
                }
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Returns the caller's current inventory grid snapshot (only the fields the cards need).
        /// The connection is already subscribed to its groups via <see cref="OnConnectedAsync"/>, so a
        /// client just calls this once after connecting (and again after a reconnect) — no separate REST
        /// fetch is needed.
        /// </summary>
        public List<InventoryGridProductInfo> JoinInventory()
        {
            var session = GetSession();

            try
            {
                SessionInfo.SetFromKratosSession(session);
                return new ProductFunctions().GetInventoryGridForUser();
            }
            finally
            {
                SessionInfo.Clear();
            }
        }

        private KratosSession GetSession()
        {
            if (Context.Items.TryGetValue(SessionItemKey, out var stored) && stored is KratosSession session)
            {
                return session;
            }

            throw new HubException("Unauthorized.");
        }
    }
}
