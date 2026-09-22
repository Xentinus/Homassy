using Homassy.API.Context;
using Homassy.API.Functions;
using Homassy.API.Middleware;
using Homassy.API.Models.Family;
using Homassy.API.Models.User;
using Homassy.Data.Models.Kratos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Realtime channel for household presence — who has the app open right now, and who is in
    /// in-store shopping mode. Feeds the home screen's member strip.
    ///
    /// Groups are derived from identity and joined on connect, like <see cref="InventoryHub"/>
    /// rather than <see cref="ShoppingListHub"/>: presence is a property of the household, not of
    /// something the user opens, and a client must start receiving it the moment it connects. A
    /// user with no household gets a group of their own (see <see cref="ScopeKeyFor"/>) so the
    /// connect path has no special case — that group simply never has anyone else in it.
    ///
    /// Authentication reuses the existing pipeline exactly as the other hubs: the Kratos session
    /// cookie rides the WebSocket handshake, <see cref="KratosSessionMiddleware"/> validates it,
    /// and the captured <see cref="KratosSession"/> is replayed into <see cref="SessionInfo"/>
    /// around each invocation so the existing Functions can be reused for access checks.
    ///
    /// Presence state lives in <see cref="FamilyPresence"/> (see that class's header for why it is
    /// process-local) and is broadcast through <see cref="PresenceRealtime"/>. A presence failure
    /// never breaks the connection it happened on: the connect, the shopping-mode report and the
    /// disconnect all still complete for the caller, matching the log-and-swallow stance the other
    /// realtime helpers already take on broadcast failures.
    /// </summary>
    [Authorize]
    public class PresenceHub : Hub
    {
        private const string SessionItemKey = "KratosSession";
        private const string ScopeItemKey = "PresenceScope";

        private readonly UserFunctions _userFunctions;
        private readonly ShoppingListFunctions _shoppingListFunctions;
        private readonly FamilyPresence _presence;
        private readonly PresenceRealtime _realtime;

        public PresenceHub(
            UserFunctions userFunctions,
            ShoppingListFunctions shoppingListFunctions,
            FamilyPresence presence,
            PresenceRealtime realtime)
        {
            _userFunctions = userFunctions;
            _shoppingListFunctions = shoppingListFunctions;
            _presence = presence;
            _realtime = realtime;
        }

        /// <summary>
        /// The presence group this identity belongs to: the household when there is one, and the
        /// user themselves when there is not. Namespaced so the two id spaces (both plain ints)
        /// can never collide.
        /// </summary>
        public static string ScopeKeyFor(int? familyId, int userId)
            => familyId.HasValue ? $"family:{familyId.Value}" : $"user:{userId}";

        public override async Task OnConnectedAsync()
        {
            // KratosSessionMiddleware ran during the handshake and stashed the session on the
            // HttpContext; capture it for the lifetime of this connection.
            var session = Context.GetHttpContext()?.GetKratosSession();
            if (session != null)
            {
                Context.Items[SessionItemKey] = session;

                string? scopeKey = null;
                UserInfo? user = null;

                try
                {
                    SessionInfo.SetFromKratosSession(session, _userFunctions);
                    var userId = SessionInfo.GetUserId();

                    if (userId.HasValue)
                    {
                        scopeKey = ScopeKeyFor(SessionInfo.GetFamilyId(), userId.Value);
                        user = GetCurrentUserInfo();
                    }
                }
                finally
                {
                    SessionInfo.Clear();
                }

                if (scopeKey != null && user != null)
                {
                    Context.Items[ScopeItemKey] = scopeKey;

                    // OnConnectedAsync also fires after an automatic reconnect (a new connection),
                    // so both the group and the presence entry are re-established transparently.
                    await Groups.AddToGroupAsync(Context.ConnectionId, PresenceRealtime.GroupName(scopeKey));
                    await RegisterPresenceAsync(scopeKey, user);
                }
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// The caller's current presence snapshot. The connection is already registered and
        /// subscribed by <see cref="OnConnectedAsync"/>, so a client calls this once after
        /// connecting (and again after a reconnect) instead of waiting for someone else to come or
        /// go before it has a roster to draw. Includes the caller — the home screen's strip shows
        /// the whole household, the viewer among them.
        /// </summary>
        public IReadOnlyList<FamilyPresenceMemberInfo> GetPresence()
        {
            var scopeKey = GetScopeKey();
            return scopeKey == null
                ? []
                : _presence.Snapshot(scopeKey);
        }

        /// <summary>
        /// Reports that this connection entered (a list's public id) or left (null) in-store
        /// shopping mode, and tells the household.
        /// </summary>
        /// <remarks>
        /// The list's <em>name</em> is resolved here, through the same access-checked path the
        /// REST endpoint uses, rather than taken from the client: what this method publishes is
        /// shown to every other member, and a client that could name its own shopping context
        /// could broadcast arbitrary text to the household. A list the caller cannot see is
        /// treated as no context at all — they still show as shopping, without a name.
        /// </remarks>
        public async Task SetShopping(Guid? shoppingListPublicId)
        {
            var context = shoppingListPublicId.HasValue
                ? ResolveShoppingContext(shoppingListPublicId.Value)
                : null;

            try
            {
                var changed = _presence.SetShopping(Context.ConnectionId, context);
                if (changed.HasValue)
                {
                    await _realtime.PresenceChangedAsync(changed.Value.ScopeKey, changed.Value.Members);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update shopping presence for connection {ConnectionId}", Context.ConnectionId);
            }
        }

        /// <summary>
        /// Drops the connection from its household's presence and broadcasts the new snapshot.
        /// Always calls the base implementation, whatever happens above it — SignalR's own
        /// connection cleanup must never be skipped because presence bookkeeping failed.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var changed = _presence.Disconnect(Context.ConnectionId);
                if (changed.HasValue)
                {
                    await _realtime.PresenceChangedAsync(changed.Value.ScopeKey, changed.Value.Members);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to process presence disconnect for connection {ConnectionId}", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task RegisterPresenceAsync(string scopeKey, UserInfo user)
        {
            try
            {
                var member = new FamilyPresenceMemberInfo
                {
                    PublicId = user.PublicId,
                    DisplayName = user.DisplayName,
                    ProfilePictureUrl = user.ProfilePictureUrl,
                    IdentityColor = user.IdentityColor,
                    DeviceCount = 1
                };

                var members = _presence.Join(scopeKey, Context.ConnectionId, member);
                if (members != null)
                {
                    await _realtime.PresenceChangedAsync(scopeKey, members);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register presence for connection {ConnectionId} in scope {ScopeKey}", Context.ConnectionId, scopeKey);
            }
        }

        /// <summary>The shopped list's name, or null when it cannot be resolved for this caller.</summary>
        private string? ResolveShoppingContext(Guid shoppingListPublicId)
        {
            var session = GetSession();

            try
            {
                SessionInfo.SetFromKratosSession(session, _userFunctions);
                return _shoppingListFunctions.GetDetailedShoppingList(shoppingListPublicId)?.Name;
            }
            catch (Homassy.Data.Exceptions.ShoppingListAccessDeniedException)
            {
                // Shopping without a name rather than a hub error: the member is genuinely in a
                // shop, and a list they may not read is not a reason to hide that from the family.
                return null;
            }
            finally
            {
                SessionInfo.Clear();
            }
        }

        /// <summary>The connecting user's info, for the presence roster. Must be called while <see cref="SessionInfo"/> is set.</summary>
        private UserInfo? GetCurrentUserInfo()
        {
            var publicId = SessionInfo.GetPublicId();
            if (!publicId.HasValue)
            {
                return null;
            }

            return _userFunctions.GetUsersByPublicIds([publicId.Value]).FirstOrDefault();
        }

        private string? GetScopeKey()
            => Context.Items.TryGetValue(ScopeItemKey, out var stored) && stored is string scopeKey
                ? scopeKey
                : null;

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
