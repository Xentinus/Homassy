using Homassy.API.Enums;

namespace Homassy.API.Models.Search
{
    /// <summary>
    /// The hits of one entity type, already ranked and capped.
    /// </summary>
    public class SearchResultGroup
    {
        public SearchResultKind Kind { get; set; }

        /// <summary>The top hits, at most the request's per-type limit.</summary>
        public List<SearchResultItem> Items { get; set; } = [];

        /// <summary>
        /// How many hits this type has in total, so the client can offer "show all N in …".
        /// Counted up to a scan cap — see <see cref="HasMore"/>.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// True when the type had more matches than the scan cap, so <see cref="TotalCount"/> is
        /// a floor rather than an exact figure and the client should say "show all" without one.
        /// </summary>
        public bool HasMore { get; set; }
    }
}
