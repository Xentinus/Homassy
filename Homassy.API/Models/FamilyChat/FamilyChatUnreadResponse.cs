namespace Homassy.API.Models.FamilyChat
{
    /// <summary>How many chat messages the caller has not read yet (#149).</summary>
    /// <remarks>
    /// Shaped like <c>DeadlineCountResponse</c> and the expiration count the bottom nav already
    /// consumes - a single <c>TotalCount</c> - because it feeds the same kind of badge, and a badge
    /// endpoint that answers a different shape for every feature is a client with three parsers.
    /// </remarks>
    public class FamilyChatUnreadResponse
    {
        public int TotalCount { get; set; }
    }
}
