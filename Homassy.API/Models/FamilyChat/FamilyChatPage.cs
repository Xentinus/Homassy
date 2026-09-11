namespace Homassy.API.Models.FamilyChat
{
    /// <summary>One page of a family's chat history, newest first (#144).</summary>
    /// <remarks>
    /// Cursor-paged, never page-numbered, and for a sharper version of the reason the timeline in
    /// #108 is: messages arrive at the top of the window *while the reader is scrolling back
    /// through it*, so an offset-based page 2 would re-show rows page 1 already had, or step past
    /// rows nobody ever saw. The cursor is the <c>(SentAt, PublicId)</c> of the oldest row this
    /// page actually carried, so "older than this" is a strict total order whatever arrives next.
    /// </remarks>
    public record FamilyChatPage
    {
        /// <summary>Newest first. The client reverses for display; the API's own order is the query's.</summary>
        public IReadOnlyList<FamilyChatMessageInfo> Items { get; init; } = [];

        /// <summary>Opaque cursor for the next (older) page, or null when this page reached the beginning.</summary>
        public string? NextCursor { get; init; }
    }
}
