using Homassy.API.Context;
using Homassy.API.Middleware;
using Homassy.API.Models.Kratos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Realtime channel for the "Törzsadatok" (master-data) management screens — products (catalog),
    /// storage locations, shopping locations, item-automation rules and family external calendars.
    /// Like <see cref="InventoryHub"/>, these lists show a whole family/personal scope at once (no single
    /// parent the user "opens"), so groups are derived from identity: each connection joins its own user
    /// group (<see cref="MasterDataRealtime.UserGroup"/>) and, if the user belongs to a family, that
    /// family's group (<see cref="MasterDataRealtime.FamilyGroup"/>).
    ///
    /// Unlike the inventory hub there is no snapshot method: every master-data screen already loads its
    /// list over REST and patches in place from the broadcast events, so the hub is a pure event channel —
    /// the client only needs to connect (which joins the groups here) and subscribe to events.
    ///
    /// Authentication reuses the existing pipeline exactly as <see cref="InventoryHub"/>: the Kratos
    /// session cookie rides the WebSocket handshake, <see cref="KratosSessionMiddleware"/> validates it,
    /// and the captured <see cref="KratosSession"/> is replayed into <see cref="SessionInfo"/>.
    /// </summary>
    [Authorize]
    public class MasterDataHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // KratosSessionMiddleware ran during the handshake and stashed the session on the
            // HttpContext; use it to derive the identity groups this connection belongs to.
            var session = Context.GetHttpContext()?.GetKratosSession();
            if (session != null)
            {
                // OnConnectedAsync also fires after an automatic reconnect (a new connection), so group
                // membership is re-established transparently.
                try
                {
                    SessionInfo.SetFromKratosSession(session);
                    var userId = SessionInfo.GetUserId();
                    var familyId = SessionInfo.GetFamilyId();

                    if (userId.HasValue)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, MasterDataRealtime.UserGroup(userId.Value));
                    }

                    if (familyId.HasValue)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, MasterDataRealtime.FamilyGroup(familyId.Value));
                    }

                    Log.Debug("Connection {ConnectionId} joined master-data groups (user {UserId}, family {FamilyId})",
                        Context.ConnectionId, userId, familyId);
                }
                finally
                {
                    SessionInfo.Clear();
                }
            }

            await base.OnConnectedAsync();
        }
    }
}
