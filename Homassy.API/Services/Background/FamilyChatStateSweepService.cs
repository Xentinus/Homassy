using Homassy.API.Hubs;
using Serilog;

namespace Homassy.API.Services.Background
{
    /// <summary>
    /// Retires expired typing and watching flags, and tells the families they belonged to.
    /// </summary>
    /// <remarks>
    /// The self-healing half of both indicators. Everything else that clears one of these flags is
    /// an event - a "stopped" call, a message sent, a leave, a disconnect - and the whole reason
    /// they carry a TTL is that none of those is guaranteed to arrive. Without this loop an expiry
    /// would only be noticed the next time something else happened to broadcast, which for a quiet
    /// family is never: "Anna is typing…" would sit there until somebody spoke, and the bubble
    /// would keep counting a reader who closed their laptop.
    /// <para>
    /// One loop for both, because they are the same kind of fact expiring for the same reason -
    /// and because a second timer would be a second place to get the pairing with the TTLs wrong.
    /// The two sets expire on different clocks (seconds for typing, most of a minute for watching),
    /// which the sweep handles simply by asking each one separately.
    /// </para>
    /// <para>
    /// It ticks faster than the shorter TTL so the typing indicator disappears within about a
    /// second of expiring, and each tick is cheap: the sweep touches one small in-memory dictionary
    /// and broadcasts only to families whose set actually changed. A tick that finds nothing sends
    /// nothing.
    /// </para>
    /// <para>
    /// Note for #95, which is about the hardcoded intervals duplicated across the notification
    /// workers: this one is deliberately not configurable. It is not a policy knob - it is paired
    /// with <see cref="FamilyChatConnectionState.TypingTtl"/> and
    /// <see cref="FamilyChatConnectionState.ActiveTtl"/>, and a deployment that moved one without
    /// the others would only make the indicators wrong.
    /// </para>
    /// </remarks>
    public class FamilyChatStateSweepService : BackgroundService
    {
        private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(2);

        private readonly FamilyChatConnectionState _connectionState;
        private readonly FamilyChatRealtime _realtime;

        public FamilyChatStateSweepService(FamilyChatConnectionState connectionState, FamilyChatRealtime realtime)
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

                    foreach (var familyPublicId in _connectionState.SweepExpiredTyping(now))
                    {
                        // The whole set, not a diff: the event carries who is typing *now*, so a
                        // client that missed an earlier one still ends up correct.
                        var typing = _connectionState.TypingIn(familyPublicId, now);
                        await _realtime.TypingChangedAsync(familyPublicId, typing, cancellationToken: stoppingToken);
                    }

                    foreach (var familyPublicId in _connectionState.SweepExpiredActive(now))
                    {
                        // Same rule, and the same reason it is the whole set rather than a diff.
                        // An expired watcher is also one whose messages start notifying again, so
                        // this is the one place where the bubble's count and the notification
                        // decision would drift apart if it were skipped.
                        var active = _connectionState.ActiveIn(familyPublicId, now);
                        await _realtime.ActiveChangedAsync(familyPublicId, active, stoppingToken);
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
                    Log.Error(ex, "Family chat state sweep failed");
                }
            }
        }
    }
}
