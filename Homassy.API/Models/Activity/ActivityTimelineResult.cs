namespace Homassy.API.Models.Activity
{
    /// <summary>One page of the activity timeline, newest first.</summary>
    public record ActivityTimelineResult
    {
        public IReadOnlyList<ActivityTimelineEntry> Entries { get; init; } = [];

        /// <summary>Opaque cursor for the next page (see <see cref="ActivityCursor"/>), or null when this page reached the end.</summary>
        public string? NextCursor { get; init; }
    }
}
