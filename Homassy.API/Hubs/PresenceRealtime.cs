using Homassy.API.Models.Family;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace Homassy.API.Hubs
{
    /// <summary>
    /// Broadcast helper for household presence: pushes "who has the app open, and who is in a
    /// shop right now" to a household's group. Registered as a singleton over
    /// <see cref="IHubContext{THub}"/>, mirroring <see cref="ShoppingListRealtime"/>.
    /// </summary>
    public sealed class PresenceRealtime
    {
        public const string PresenceChangedEvent = "PresenceChanged";

        /// <summary>
        /// SignalR group name for one presence scope. The scope key is built by
        /// <see cref="PresenceHub"/> from identity (a household, or a lone user who has none), so
        /// it is already namespaced — see <see cref="PresenceHub.ScopeKeyFor"/>.
        /// </summary>
        public static string GroupName(string scopeKey) => $"presence:{scopeKey}";

        private readonly IHubContext<PresenceHub>? _hubContext;

        /// <remarks>
        /// The hub context is optional so a host that maps no hubs still resolves the helper and
        /// simply skips the broadcast, exactly as the other realtime helpers do.
        /// </remarks>
        public PresenceRealtime(IHubContext<PresenceHub>? hubContext = null)
        {
            _hubContext = hubContext;
        }

        /// <summary>Pushes a scope's current presence snapshot to everyone in it.</summary>
        public async Task PresenceChangedAsync(string scopeKey, IReadOnlyList<FamilyPresenceMemberInfo> members, CancellationToken cancellationToken = default)
        {
            var hub = _hubContext;
            if (hub == null)
            {
                Log.Warning("PresenceHub context unavailable; skipping presence broadcast for scope {ScopeKey}", scopeKey);
                return;
            }

            try
            {
                await hub.Clients.Group(GroupName(scopeKey)).SendAsync(PresenceChangedEvent, members, cancellationToken);
            }
            catch (Exception ex)
            {
                // A broadcast failure must never break the connect/disconnect that triggered it.
                Log.Error(ex, "Failed to broadcast presence for scope {ScopeKey}", scopeKey);
            }
        }
    }
}
