using System.Collections.Concurrent;

namespace Homassy.API.Services
{
    /// <summary>
    /// Remembers, in memory, when each user was last seen - so the away-delta feature (#127) can
    /// answer "what changed since your last visit" without writing to the database on every
    /// request.
    /// </summary>
    /// <remarks>
    /// <b>Why in memory at all.</b> A last-seen stamp is written on literally every authenticated
    /// request and read approximately never (once per app launch, and only after a long gap). A
    /// row update per request would add a write to the hottest path in the application to keep a
    /// value whose precision nobody can perceive - one flushed every few minutes is exactly as
    /// useful. <see cref="Background.LastSeenFlushService"/> is what makes it durable enough for
    /// the one case that needs it: a user opening the app on a new device, where the client has no
    /// local last-seen of its own.
    /// <para>
    /// Registered as a singleton (see <c>Program.cs</c>), so every method here runs against
    /// arbitrary concurrent requests and must be safe for that.
    /// </para>
    /// </remarks>
    public sealed class LastSeenTracker
    {
        /// <summary>
        /// The pending stamps. Swapped out wholesale by <see cref="DrainPending"/> rather than
        /// iterated-and-removed - see that method for why that difference matters.
        /// </summary>
        private ConcurrentDictionary<int, DateTime> _pending = new();

        /// <summary>
        /// Records that <paramref name="userId"/> was seen at <paramref name="utcNow"/>.
        ///
        /// <para>
        /// Keeps the <b>later</b> of the two timestamps when a user already has a pending stamp.
        /// Requests do not arrive in timestamp order (two in the same millisecond, a slow one
        /// stamping after a fast one that came later), and last-seen means the most recent moment,
        /// so an out-of-order write must not be able to move it backwards.
        /// </para>
        /// </summary>
        public void Stamp(int userId, DateTime utcNow)
        {
            var stamp = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            _pending.AddOrUpdate(userId, stamp, (_, existing) => existing > stamp ? existing : stamp);
        }

        /// <summary>
        /// Returns the pending stamps and clears them, so a flush never writes the same value twice.
        /// </summary>
        /// <remarks>
        /// Swaps in a fresh dictionary rather than iterating the existing one and removing what it
        /// read. That is what makes a <see cref="Stamp"/> arriving <em>during</em> a drain safe: it
        /// lands in either the old dictionary (already returned, so it is written this time) or the
        /// new one (returned by the next drain), and in both cases it is written exactly once and
        /// never lost. An iterate-and-remove drain has a window where a stamp can be overwritten by
        /// the removal of the value it replaced - the classic way this kind of tracker silently
        /// drops the most recent visit.
        /// </remarks>
        public IReadOnlyDictionary<int, DateTime> DrainPending() =>
            Interlocked.Exchange(ref _pending, new ConcurrentDictionary<int, DateTime>());
    }
}
