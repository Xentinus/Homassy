using Homassy.API.Context;
using Homassy.API.Functions;
using Homassy.API.Middleware;
using Homassy.API.Models.FamilyChat;
using Homassy.API.Models.Kratos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Realtime channel for the family chat (#144). One SignalR group per family
    /// (<see cref="FamilyChatRealtime.GroupName"/>); a connection joins its own family's group and
    /// receives live <c>MessageCreated</c> / <c>MessageDeleted</c> events for it.
    ///
    /// Authentication is the pipeline the rest of the app already uses: the Kratos session cookie
    /// rides the WebSocket handshake, <see cref="KratosSessionMiddleware"/> validates it and sets
    /// the principal, so <see cref="AuthorizeAttribute"/> works exactly as it does on the
    /// controllers. The validated <see cref="KratosSession"/> is captured at connect and replayed
    /// into <see cref="SessionInfo"/> around each invocation, so
    /// <see cref="FamilyChatFunctions"/> - which reads <see cref="SessionInfo"/> for its access
    /// check - is reused here unchanged. Copied from <see cref="ShoppingListHub"/> deliberately:
    /// two hubs that authenticate differently is one hub authenticating wrongly.
    ///
    /// Unlike the shopping-list hub there is no resource id to join by. The only conversation a
    /// connection may join is its own family's, so <see cref="JoinChat"/> takes no argument and
    /// resolves the group from the session - a client cannot ask for somebody else's group.
    /// </summary>
    [Authorize]
    public class FamilyChatHub : Hub
    {
        private const string SessionItemKey = "KratosSession";

        private readonly UserFunctions _userFunctions;
        private readonly FamilyChatFunctions _chatFunctions;
        private readonly FamilyChatConnectionState _connectionState;
        private readonly FamilyChatRealtime _realtime;

        public FamilyChatHub(
            UserFunctions userFunctions,
            FamilyChatFunctions chatFunctions,
            FamilyChatConnectionState connectionState,
            FamilyChatRealtime realtime)
        {
            _userFunctions = userFunctions;
            _chatFunctions = chatFunctions;
            _connectionState = connectionState;
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
        /// Joins the caller to their family's conversation and returns the newest page of it.
        /// </summary>
        /// <remarks>
        /// The snapshot comes back with the join rather than from a second REST call, the same way
        /// <c>JoinList</c> answers with the list it just joined: opening the panel is one round
        /// trip, and the page cannot be a moment older than the group membership that will keep it
        /// current.
        /// </remarks>
        public async Task<FamilyChatPage> JoinChat()
        {
            var session = GetSession();

            FamilyChatPage page;
            Guid familyPublicId;
            int userId;
            Guid userPublicId;
            string displayName;
            try
            {
                SessionInfo.SetFromKratosSession(session, _userFunctions);
                (_, familyPublicId) = _chatFunctions.RequireFamily();
                page = await _chatFunctions.GetMessagesAsync(null, 0, Context.ConnectionAborted);

                userId = SessionInfo.GetUserId() ?? throw new HubException("Unauthorized.");
                userPublicId = SessionInfo.GetPublicId() ?? throw new HubException("Unauthorized.");
                displayName = ResolveDisplayName(userPublicId);
            }
            catch (Exceptions.FamilyChatAccessDeniedException)
            {
                throw new HubException("Access to this family chat was denied.");
            }
            finally
            {
                SessionInfo.Clear();
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, FamilyChatRealtime.GroupName(familyPublicId));

            // Registered only after the group join actually succeeded: a connection that was
            // refused the conversation must never appear in anyone's typing list.
            _connectionState.Register(Context.ConnectionId, familyPublicId, userPublicId, userId, displayName);

            Log.Debug("Connection {ConnectionId} joined family chat {FamilyPublicId}", Context.ConnectionId, familyPublicId);

            return page;
        }

        /// <summary>
        /// Reports whether this connection's composer currently has somebody typing in it (#148).
        /// </summary>
        /// <remarks>
        /// The flag carries its own expiry (see <see cref="FamilyChatConnectionState.TypingTtl"/>),
        /// so a client that never sends the "stopped" call - a closed tab, a dropped connection -
        /// stops being a typist by itself rather than forever. The client refreshes it while there
        /// is something in the input.
        /// </remarks>
        public async Task SetTyping(bool isTyping)
        {
            var familyPublicId = _connectionState.SetTyping(Context.ConnectionId, isTyping, DateTime.UtcNow);
            if (familyPublicId == null) return;

            await BroadcastTypingAsync(familyPublicId.Value);
        }

        /// <summary>Pushes the family's current typing set, never back to the connection that changed it.</summary>
        private Task BroadcastTypingAsync(Guid familyPublicId)
        {
            var typing = _connectionState.TypingIn(familyPublicId, DateTime.UtcNow);
            return _realtime.TypingChangedAsync(familyPublicId, typing, Context.ConnectionId);
        }

        /// <summary>The caller's display name, for the typing list. Must be called with <see cref="SessionInfo"/> set.</summary>
        private string ResolveDisplayName(Guid userPublicId)
        {
            var user = _userFunctions.GetUsersByPublicIds([userPublicId]).FirstOrDefault();
            return user?.DisplayName ?? user?.Name ?? string.Empty;
        }

        /// <summary>Removes the caller from their family's conversation (the panel was closed).</summary>
        /// <remarks>
        /// Best-effort and never throws for a connection that never joined: the group is dropped
        /// on disconnect anyway, so a failed leave costs nothing but a few extra events.
        /// </remarks>
        public async Task LeaveChat()
        {
            // The registry knows which family this connection is in without a session lookup, and
            // it is also what has to be cleaned up - so it answers first.
            var familyPublicId = _connectionState.Remove(Context.ConnectionId) ?? TryResolveFamilyPublicId();
            if (familyPublicId == null)
            {
                return;
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, FamilyChatRealtime.GroupName(familyPublicId.Value));

            // Leaving clears this connection's typing (and #149's active) flag with it, so the
            // family sees the indicator go rather than waiting for the TTL.
            await BroadcastTypingAsync(familyPublicId.Value);

            Log.Debug("Connection {ConnectionId} left family chat {FamilyPublicId}", Context.ConnectionId, familyPublicId);
        }

        /// <summary>
        /// Drops the connection's state and tells the family, whatever happens above it.
        /// </summary>
        /// <remarks>
        /// A dropped socket is the common case, not the exception - a phone locking its screen is
        /// one - so this path, not <see cref="LeaveChat"/>, is what most typing flags are actually
        /// cleared by. The base implementation always runs: SignalR's own cleanup must not be
        /// skipped because a broadcast failed.
        /// </remarks>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var familyPublicId = _connectionState.Remove(Context.ConnectionId);
                if (familyPublicId != null)
                {
                    await BroadcastTypingAsync(familyPublicId.Value);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clean up chat state for connection {ConnectionId}", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// The caller's family public id, or null when there is no session or no family.
        /// </summary>
        /// <remarks>
        /// Used by the paths that only want to clean up after a connection. They must not fail the
        /// invocation because the caller turned out to have no family - there is simply nothing to
        /// clean up in that case.
        /// </remarks>
        private Guid? TryResolveFamilyPublicId()
        {
            if (!Context.Items.TryGetValue(SessionItemKey, out var stored) || stored is not KratosSession session)
            {
                return null;
            }

            try
            {
                SessionInfo.SetFromKratosSession(session, _userFunctions);
                return _chatFunctions.RequireFamily().FamilyPublicId;
            }
            catch (Exceptions.FamilyChatAccessDeniedException)
            {
                return null;
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
