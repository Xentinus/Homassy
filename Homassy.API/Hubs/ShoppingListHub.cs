using Homassy.API.Context;
using Homassy.API.Functions;
using Homassy.API.Middleware;
using Homassy.API.Models.Kratos;
using Homassy.API.Models.ShoppingList;
using Homassy.API.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Realtime channel for shopping lists. Each shopping list is a SignalR group
    /// (<see cref="ShoppingListRealtime.GroupName"/>); a client joins the group of the list it is
    /// viewing and receives live <c>ItemUpserted</c> / <c>ItemDeleted</c> / <c>ListUpdated</c> /
    /// <c>ListDeleted</c> / <c>PresenceChanged</c> events for it.
    ///
    /// Authentication reuses the existing pipeline: the Kratos session cookie rides the WebSocket
    /// handshake, <see cref="KratosSessionMiddleware"/> validates it and sets the principal, so
    /// <see cref="AuthorizeAttribute"/> works exactly as on the controllers. The validated
    /// <see cref="KratosSession"/> is captured at connect and replayed into <see cref="SessionInfo"/>
    /// around each invocation so the existing <see cref="ShoppingListFunctions"/> (which read
    /// <see cref="SessionInfo"/>) can be reused unchanged for access checks.
    ///
    /// Presence (who else has this list open) is tracked in <see cref="ShoppingListPresence"/> and
    /// broadcast through <see cref="ShoppingListRealtime.PresenceChangedAsync"/>; see that class's
    /// header for why it is process-local. A presence failure never breaks the hub method that
    /// triggered it — join/leave/disconnect all still complete for the caller even if updating or
    /// broadcasting presence throws, matching <see cref="ShoppingListRealtime.SendAsync"/>'s
    /// existing log-and-swallow stance on broadcast failures.
    /// </summary>
    [Authorize]
    public class ShoppingListHub : Hub
    {
        private const string SessionItemKey = "KratosSession";

        private readonly UserFunctions _userFunctions;
        private readonly ShoppingListFunctions _shoppingListFunctions;
        private readonly ShoppingListPresence _presence;
        private readonly ShoppingListRealtime _realtime;

        public ShoppingListHub(
            UserFunctions userFunctions,
            ShoppingListFunctions shoppingListFunctions,
            ShoppingListPresence presence,
            ShoppingListRealtime realtime)
        {
            _userFunctions = userFunctions;
            _shoppingListFunctions = shoppingListFunctions;
            _presence = presence;
            _realtime = realtime;
        }

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
            UserInfo? currentUser;
            try
            {
                SessionInfo.SetFromKratosSession(session, _userFunctions);
                snapshot = _shoppingListFunctions.GetDetailedShoppingList(publicId, showPurchased);
                currentUser = GetCurrentUserInfo();
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

            // Presence is only ever registered for a list the group-join above actually succeeded
            // for — a list the caller was denied must never appear in anyone's presence.
            await RegisterPresenceAsync(publicId, currentUser);

            return snapshot;
        }

        /// <summary>Removes the caller from a shopping list's channel (e.g. when switching lists).</summary>
        public async Task LeaveList(Guid publicId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ShoppingListRealtime.GroupName(publicId));
            Log.Debug("Connection {ConnectionId} left shopping list {PublicId}", Context.ConnectionId, publicId);

            try
            {
                var members = _presence.Leave(publicId, Context.ConnectionId);
                await _realtime.PresenceChangedAsync(publicId, members);
            }
            catch (Exception ex)
            {
                // Matches ShoppingListRealtime.SendAsync's stance: a presence failure must never
                // break the leave the caller asked for.
                Log.Error(ex, "Failed to update presence for connection {ConnectionId} leaving shopping list {PublicId}", Context.ConnectionId, publicId);
            }
        }

        /// <summary>
        /// Drops the connection from every list's presence it had joined and broadcasts the new
        /// snapshot for each. Always calls the base implementation, whatever happens above it —
        /// SignalR's own connection cleanup must never be skipped because presence bookkeeping
        /// failed.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var affectedLists = _presence.Disconnect(Context.ConnectionId);
                foreach (var (listPublicId, members) in affectedLists)
                {
                    await _realtime.PresenceChangedAsync(listPublicId, members);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to process presence disconnect for connection {ConnectionId}", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task RegisterPresenceAsync(Guid listPublicId, UserInfo? user)
        {
            if (user == null)
            {
                // No local user to attribute presence to (e.g. a Kratos identity with no Homassy
                // user yet); nothing to register.
                return;
            }

            try
            {
                var member = new PresenceMemberInfo
                {
                    PublicId = user.PublicId,
                    DisplayName = user.DisplayName,
                    ProfilePictureUrl = user.ProfilePictureUrl,
                    IdentityColor = user.IdentityColor,
                    DeviceCount = 1
                };

                var members = _presence.Join(listPublicId, Context.ConnectionId, member);
                await _realtime.PresenceChangedAsync(listPublicId, members);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register presence for connection {ConnectionId} on shopping list {PublicId}", Context.ConnectionId, listPublicId);
            }
        }

        /// <summary>The joining/leaving user's info, for the presence roster. Must be called while <see cref="SessionInfo"/> is set.</summary>
        private UserInfo? GetCurrentUserInfo()
        {
            var publicId = SessionInfo.GetPublicId();
            if (!publicId.HasValue)
            {
                return null;
            }

            return _userFunctions.GetUsersByPublicIds([publicId.Value]).FirstOrDefault();
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
