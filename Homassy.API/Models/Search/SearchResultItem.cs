using Homassy.API.Enums;

namespace Homassy.API.Models.Search
{
    /// <summary>
    /// One hit in the global search response.
    /// </summary>
    public class SearchResultItem
    {
        /// <summary>The matched entity's own public id.</summary>
        public Guid PublicId { get; set; }

        public SearchResultKind Kind { get; set; }

        /// <summary>What the row reads as — the product, list, location or automation name.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The second line: a brand, a storage location, the shopping-list item that matched.
        /// Null when the title says everything.
        /// </summary>
        public string? Subtitle { get; set; }

        /// <summary>
        /// Versioned path to the product thumbnail for hits that have one, so the palette shows
        /// the same picture the cards do. See <see cref="Constants.MediaUrls"/>.
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>The entity's own colour (lists and locations carry one), as <c>#rrggbb</c>.</summary>
        public string? Color { get; set; }

        /// <summary>
        /// The public id the client navigates to when the hit is not itself a destination: an
        /// inventory item opens its product, an automation opens its rule page by its own id.
        /// Null when <see cref="PublicId"/> is the destination.
        /// </summary>
        public Guid? ParentPublicId { get; set; }
    }
}
