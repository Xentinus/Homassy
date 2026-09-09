using Homassy.API.Enums;

namespace Homassy.API.Models.Insights
{
    /// <summary>
    /// One category's share of the family's current inventory, as returned by
    /// <see cref="InventoryCompositionResponse"/>.
    /// </summary>
    public record CompositionSlice
    {
        public ProductCategory Category { get; init; }
        public int Count { get; init; }
        public decimal Share { get; init; }
    }

    /// <summary>
    /// The family's current inventory (non-deleted, non-fully-consumed items) broken down by
    /// product category: the largest categories as individual <see cref="Slices"/>, everything
    /// past that folded into <see cref="OtherCount"/> so the chart never has to render more slices
    /// than a legend can hold.
    /// </summary>
    public record InventoryCompositionResponse
    {
        public IReadOnlyList<CompositionSlice> Slices { get; init; } = [];
        public int OtherCount { get; init; }
        public int TotalCount { get; init; }
    }
}
