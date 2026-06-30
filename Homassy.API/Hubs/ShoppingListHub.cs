using Homassy.API.Context;
using Homassy.API.Functions;
using Homassy.API.Middleware;
using Homassy.API.Models.Kratos;
using Homassy.API.Models.ShoppingList;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Realtime channel for shopping lists. Each shopping list is a SignalR group
    /// (<see cref="ShoppingListRealtime.GroupName"/>); a client joins the group of the list it is
    /// viewing and receives live <c>ItemUpserted</c> / <c>ItemDeleted</c> / <c>ListUpdated</c> /
    /// <c>ListDeleted</c> events for it.
    ///
    /// Authentication reuses the existing pipeline: the Kratos session cookie rides the WebSocket
    /// handshake, <see cref="KratosSessionMiddleware"/> validates it and sets the principal, so
    /// <see cref="AuthorizeAttribute"/> works exactly as on the controllers. The validated
    /// <see cref="KratosSession"/> is captured at connect and replayed into <see cref="SessionInfo"/>
    /// around each invocation so the existing <see cref="ShoppingListFunctions"/> (which read
    /// <see cref="SessionInfo"/>) can be reused unchanged for access checks.
    /// </summary>
    [Authorize]
    public class ShoppingListHub : Hub
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
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Joins the caller to a shopping list's channel and returns its current items.
        /// The snapshot is produced by the same access-checked path used by the REST endpoint,
        /// so unauthorized lists are rejected and no separate fetch is needed after joining.
        /// </summary>
        public async Task<DetailedShoppingListInfo> JoinList(Guid publicId, bool showPurchased = false)
        {
            var session = GetSession();

            DetailedShoppingListInfo? snapshot;
            try
            {
                SessionInfo.SetFromKratosSession(session);
                snapshot = new ShoppingListFunctions().GetDetailedShoppingList(publicId, showPurchased);
            }
            catch (Exceptions.ShoppingListAccessDeniedException)
            {
                throw new HubException("Access to this shopping list was denied.");
            }
            finally
            {
                SessionInfo.Clear();
            }

            if (snapshot == null)
            {
                throw new HubException("Shopping list not found.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, ShoppingListRealtime.GroupName(publicId));
            Log.Debug("Connection {ConnectionId} joined shopping list {PublicId}", Context.ConnectionId, publicId);

            return snapshot;
        }

        /// <summary>Removes the caller from a shopping list's channel (e.g. when switching lists).</summary>
        public async Task LeaveList(Guid publicId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ShoppingListRealtime.GroupName(publicId));
            Log.Debug("Connection {ConnectionId} left shopping list {PublicId}", Context.ConnectionId, publicId);
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
