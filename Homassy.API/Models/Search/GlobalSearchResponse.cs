namespace Homassy.API.Models.Search
{
    /// <summary>
    /// One keystroke's worth of answers: every searchable type, each capped, in one round trip.
    /// </summary>
    public class GlobalSearchResponse
    {
        /// <summary>The query the groups below answer, echoed so a late response can be discarded.</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>Only the types that matched something — an empty group is not sent.</summary>
        public List<SearchResultGroup> Groups { get; set; } = [];

        /// <summary>Total hits across every group, so the client can render one empty state.</summary>
        public int TotalCount => Groups.Sum(g => g.Items.Count);
    }
}
