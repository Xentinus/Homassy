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
    }
}
