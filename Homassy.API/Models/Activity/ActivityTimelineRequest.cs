using Homassy.API.Enums;

namespace Homassy.API.Models.Activity
{
    /// <summary>
    /// Request for one page of the cursor-paged, server-aggregated activity timeline. Unlike
    /// <see cref="GetActivitiesRequest"/> this is never page-numbered: <see cref="Cursor"/> names
    /// the last row the previous page consumed (see <see cref="ActivityCursor"/>), or is null for
    /// the first page.
    /// </summary>
    public class ActivityTimelineRequest
    {
        public string? Cursor { get; init; }
        public int PageSize { get; init; } = 30;
        public ActivityType? ActivityType { get; init; }
        public Guid? UserPublicId { get; init; }

        /// <summary>
        /// Optional window start (exclusive), for showing the timeline over one specific period -
        /// #127's "what changed while you were away" card links here with the exact window its
        /// summary was computed over.
        /// </summary>
        /// <remarks>
        /// A filter on the same query rather than a second endpoint: the timeline's visibility
        /// rules, aggregation and cursor paging are exactly the same over a window as without one,
        /// and a parallel endpoint would be two implementations of the rule about who may see whose
        /// activity - which is the one rule here whose drift would be a data leak.
        /// <para>
        /// It composes with the cursor rather than replacing it, so paging inside a window works
        /// the same way as paging without one.
        /// </para>
        /// </remarks>
        public DateTime? Since { get; init; }

        /// <summary>Optional window end (inclusive). See <see cref="Since"/>.</summary>
        public DateTime? Until { get; init; }
    }
}
