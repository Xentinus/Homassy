using Homassy.API.Hubs;
using Serilog;

namespace Homassy.API.Services.Background
{
    /// <summary>
    /// Retires expired typing flags and tells the families they belonged to (#148).
    /// </summary>
    /// <remarks>
    /// The self-healing half of the indicator. Everything else that clears a typing flag is an
    /// event - a "stopped" call, a message sent, a leave, a disconnect - and the whole reason the
    /// flag has a TTL is that none of those is guaranteed to arrive. Without this loop an expiry
    /// would only be noticed the next time something else happened to broadcast, which for a quiet
    /// family is never: "Anna is typing…" would sit there until somebody spoke.
    /// <para>
    /// It ticks faster than the TTL so the indicator disappears within about a second of expiring,
    /// and each tick is cheap: the sweep touches one small in-memory dictionary and broadcasts only
    /// to families whose set actually changed. A tick that finds nothing sends nothing.
    /// </para>
    /// <para>
    /// Note for #95, which is about the hardcoded intervals duplicated across the notification
    /// workers: this one is deliberately not configurable. It is not a policy knob - it is paired
    /// with <see cref="FamilyChatConnectionState.TypingTtl"/>, and a deployment that moved one
    /// without the other would only make the indicator wrong.
    /// </para>
    /// </remarks>
    public class FamilyChatTypingSweepService : BackgroundService
    {
        private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(2);

        private readonly FamilyChatConnectionState _connectionState;
        private readonly FamilyChatRealtime _realtime;

        public FamilyChatTypingSweepService(FamilyChatConnectionState connectionState, FamilyChatRealtime realtime)
        {
            _connectionState = connectionState;
            _realtime = realtime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(SweepInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var affected = _connectionState.SweepExpiredTyping(now);

                    foreach (var familyPublicId in affected)
                    {
                        // The whole set, not a diff: the event carries who is typing *now*, so a
                        // client that missed an earlier one still ends up correct.
                        var typing = _connectionState.TypingIn(familyPublicId, now);
                        await _realtime.TypingChangedAsync(familyPublicId, typing, cancellationToken: stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // A failed sweep must not end the loop: the next tick would have fixed
                    // whatever this one could not, and a dead sweeper means every stale indicator
                    // stays forever.
                    Log.Error(ex, "Family chat typing sweep failed");
                }
            }
        }
    }
}
