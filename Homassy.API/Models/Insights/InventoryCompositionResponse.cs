using Homassy.API.Enums;

namespace Homassy.API.Models.Insights
{
    /// <summary>
    /// One category's share of the family's current inventory, as returned by
    /// <see cref="InventoryCompositionResponse"/>. Never carries <see cref="Category"/> equal to
    /// <see cref="ProductCategory.Other"/> - see <see cref="InventoryCompositionResponse.OtherCount"/>.
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

        /// <summary>
        /// Everything not individually listed in <see cref="Slices"/> - the single bucket for
        /// every notion of "other" this endpoint could otherwise produce, and the reason
        /// <see cref="Slices"/> never contains an entry for <see cref="ProductCategory.Other"/>.
        /// </summary>
        /// <remarks>
        /// Without this rule there would be two different things meaning "other" in the same
        /// payload: the explicit <see cref="ProductCategory.Other"/> category (value 0, which
        /// also absorbs any item whose product has a null <c>Category</c> - folded in the
        /// <c>GroupBy</c> key itself, see <c>InsightFunctions.ComputeInventoryCompositionAsync</c>)
        /// and, separately, every category ranked past the top-N cutoff. If
        /// <see cref="ProductCategory.Other"/> happened to rank inside the top N by count, a naive
        /// implementation would emit it as its own <see cref="CompositionSlice"/> <em>and</em>
        /// keep a smaller, separately-computed <see cref="OtherCount"/> for the overflow - two
        /// "other"s in one response (three, counting the donut chart's own under-2% merge
        /// downstream). So <see cref="ProductCategory.Other"/> is excluded from the ranking
        /// before the top-N cut is taken and is always folded in here instead, regardless of how
        /// large its count is. <see cref="Slices"/>' summed <see cref="CompositionSlice.Count"/>
        /// plus this field always equals <see cref="TotalCount"/>.
        /// <para>
        /// Every insight endpoint that follows this same top-N-plus-other shape (Tasks 8, 9, 19,
        /// 20) should apply the identical fold, for the identical reason.
        /// </para>
        /// </remarks>
        public int OtherCount { get; init; }

        public int TotalCount { get; init; }
    }
}
