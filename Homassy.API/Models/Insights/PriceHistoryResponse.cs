using Homassy.API.Controllers;
using Homassy.API.Enums;
using Homassy.API.Functions;
using System.Text.Json.Serialization;

namespace Homassy.API.Models.Insights
{
    /// <summary>
    /// One purchase of the product, re-expressed as a price per canonical unit - see
    /// <see cref="PriceBasisGroup"/> for which unit that is and whether the value is comparable
    /// beyond this group at all.
    /// </summary>
    public record PricePoint
    {
        /// <summary>When the purchase was made (UTC, as stored).</summary>
        public DateTime PurchasedAt { get; init; }

        /// <summary>
        /// The purchase's price divided by its quantity expressed in the group's
        /// <see cref="PriceBasisGroup.CanonicalUnit"/> - so a 2 l bottle at 300 and a 1 l bottle
        /// at 200 come out as 150 and 200, making the bigger bottle correctly the cheaper one.
        /// This is the whole reason the endpoint exists; see
        /// <see cref="UnitNormalization"/> for the normalization itself.
        /// </summary>
        public decimal UnitPrice { get; init; }

        /// <summary>
        /// The quantity <b>as it was purchased</b> - 500 for a 500 g jar, not the 0.5 kg it
        /// normalizes to. Deliberately the original rather than the normalized value, paired with
        /// <see cref="Unit"/>: this is what the family actually recorded and what a tooltip should
        /// show them ("500 g for 250 Ft"), while the normalized basis they are being compared
        /// under is already stated once per group on <see cref="PriceBasisGroup.CanonicalUnit"/>
        /// rather than repeated on every point.
        /// </summary>
        public decimal Quantity { get; init; }

        /// <summary>
        /// The unit <see cref="Quantity"/> is expressed in - the inventory item's own unit, since
        /// <c>Entities.Product.ProductPurchaseInfo</c> carries no unit of its own and its
        /// <c>OriginalQuantity</c> is recorded in the unit of the item it purchased.
        /// </summary>
        public Unit Unit { get; init; }
    }

    /// <summary>
    /// Every purchase of the product made at one shopping location, within one currency and one
    /// comparison basis, plus the five statistics the chart's summary line needs. All five are
    /// computed over <see cref="Points"/> only - never across locations, never across currencies
    /// and never across bases.
    /// </summary>
    public record ShopPriceSeries
    {
        /// <summary>
        /// The location's public id, or <see langword="null"/> for the single "unknown location"
        /// bucket every purchase with no shopping location tag folds into - never dropped, and
        /// never split across more than one such bucket. Matches
        /// <see cref="LocationSpend.ShoppingLocationPublicId"/> exactly, including being
        /// deliberately nullable against this codebase's usual convention, because that bucket
        /// has no location row to carry a public id.
        /// </summary>
        public Guid? ShoppingLocationPublicId { get; init; }

        /// <summary>
        /// The location's display name, or a fixed "unknown location" label when
        /// <see cref="ShoppingLocationPublicId"/> is <see langword="null"/> (or, defensively, when
        /// a non-null id can no longer be resolved to a location row).
        /// </summary>
        public string LocationName { get; init; } = "";

        /// <summary>Every qualifying purchase at this location, oldest first.</summary>
        public IReadOnlyList<PricePoint> Points { get; init; } = [];

        /// <summary>The lowest <see cref="PricePoint.UnitPrice"/> in <see cref="Points"/>.</summary>
        public decimal Min { get; init; }

        /// <summary>The highest <see cref="PricePoint.UnitPrice"/> in <see cref="Points"/>.</summary>
        public decimal Max { get; init; }

        /// <summary>
        /// The <see cref="PricePoint.UnitPrice"/> of the most recent purchase in
        /// <see cref="Points"/> - the last one by <see cref="PricePoint.PurchasedAt"/>, not the
        /// last one the database happened to return.
        /// </summary>
        public decimal Latest { get; init; }

        /// <summary>
        /// The mean <see cref="PricePoint.UnitPrice"/> over <see cref="Points"/>, unweighted: each
        /// purchase counts once regardless of how much of it was bought. That is the average this
        /// chart is asking about - "what do I usually pay per kilo here" - not a
        /// quantity-weighted cost basis.
        /// </summary>
        public decimal Average { get; init; }

        /// <summary>How many purchases <see cref="Points"/> holds.</summary>
        public int Count { get; init; }
    }

    /// <summary>
    /// Every purchase of the product that is comparable with every other purchase in the same
    /// group: one currency (from the enclosing <see cref="PriceHistoryResponse.ByCurrency"/> entry)
    /// and one comparison basis. A product bought both by weight and by the pack produces two of
    /// these - never one blended series, which would put a price per kilogram and a price per pack
    /// on the same axis.
    /// </summary>
    public record PriceBasisGroup
    {
        /// <summary>
        /// <see cref="UnitNormalization.SeriesKeyFor"/>'s key for this group - <c>"per-kg"</c>,
        /// <c>"per-l"</c>, <c>"per-pack"</c>, and so on. Stable and machine-readable: the client
        /// keys its series colours and its i18n label off this, never off a display string.
        /// </summary>
        public string SeriesKey { get; init; } = "";

        /// <summary>
        /// The unit every <see cref="PricePoint.UnitPrice"/> in this group is a price per -
        /// <see cref="Unit.Kilogram"/>, <see cref="Unit.Liter"/>, or the purchased unit itself for
        /// a countable one.
        /// </summary>
        public Unit CanonicalUnit { get; init; }

        /// <summary>
        /// <see langword="true"/> only when this group's prices are genuinely per weight or per
        /// volume, so a client may present them as a like-for-like comparison. <see langword="false"/>
        /// for a countable basis: a price per pack is still perfectly comparable <em>within</em>
        /// this group (every point in it is a price per the same <see cref="CanonicalUnit"/>), but
        /// the client must label it "per pack" rather than implying a weight comparison it cannot
        /// make - see <see cref="UnitNormalization"/> for why that distinction is the point of the
        /// whole flag.
        /// </summary>
        public bool Normalized { get; init; }

        /// <summary>
        /// One entry per shopping location that sold the product on this basis, busiest first.
        /// </summary>
        public IReadOnlyList<ShopPriceSeries> Shops { get; init; } = [];
    }

    /// <summary>
    /// The cheapest unit price the caller's household has ever actually paid for the product in
    /// the window, within one currency and one basis - see
    /// <see cref="PriceInsightFunctions.GetPriceHistoryAsync"/> for how that single currency and
    /// basis are picked when the data spans several.
    /// </summary>
    public record BestKnownPrice
    {
        /// <summary>The winning <see cref="PricePoint.UnitPrice"/>.</summary>
        public decimal UnitPrice { get; init; }

        /// <summary>
        /// The currency it was paid in - never converted, never blended.
        /// </summary>
        /// <remarks>
        /// Serialized as the enum's <b>name</b> (<c>"Huf"</c>, <c>"Eur"</c>) rather than the API's
        /// default numeric form, for two reasons. It matches
        /// <see cref="PriceHistoryResponse.ByCurrency"/>, whose dictionary keys are already names
        /// (a <c>Dictionary&lt;Currency, …&gt;</c> always serializes its key that way), so one
        /// response cannot express the same currency two different ways. And the number is not
        /// safely interpretable on the client: <c>Homassy.Web</c>'s own <c>Currency</c> enum is a
        /// hand-maintained three-member subset whose values have drifted out of step with this
        /// one, so a numeric currency would be silently mis-read there. A name needs no such
        /// table - and, being a well-formed three-letter code for every fiat currency, it goes
        /// straight into <c>Intl.NumberFormat</c>, which is exactly what the client does with
        /// <see cref="PriceHistoryResponse.ByCurrency"/>'s keys already.
        /// </remarks>
        [JsonConverter(typeof(JsonStringEnumConverter<Currency>))]
        public Currency Currency { get; init; }

        /// <summary>
        /// Where it was paid, or <see langword="null"/> when the winning purchase carries no
        /// resolvable shopping location.
        /// </summary>
        public Guid? ShoppingLocationPublicId { get; init; }

        /// <summary>The location's display name, or the "unknown location" label.</summary>
        public string LocationName { get; init; } = "";

        /// <summary>When it was paid, so the client can say how long ago that was.</summary>
        public DateTime At { get; init; }

        /// <summary>
        /// The basis it was paid on - the client must show this next to the amount, since a best
        /// known price of 150 means nothing without knowing it is 150 per litre.
        /// </summary>
        public string SeriesKey { get; init; } = "";
    }

    /// <summary>
    /// One product's purchase price history for the caller's household, as returned by
    /// <see cref="ProductController.GetPriceHistory"/>. See
    /// <see cref="PriceInsightFunctions.GetPriceHistoryAsync"/> for the scope rule (the same
    /// personal-plus-family union the inventory list itself uses) and for every rule about what is
    /// dropped, grouped or picked.
    /// </summary>
    public record PriceHistoryResponse
    {
        /// <summary>
        /// The history, grouped by the currency it was paid in and then by comparison basis.
        /// Currencies are kept apart and never converted into one another: this milestone has no
        /// exchange rate to convert with, so a household that shops in two currencies gets two
        /// entries here rather than one misleading blended series.
        /// </summary>
        public IReadOnlyDictionary<Currency, IReadOnlyList<PriceBasisGroup>> ByCurrency { get; init; }
            = new Dictionary<Currency, IReadOnlyList<PriceBasisGroup>>();

        /// <summary>
        /// The cheapest price actually paid, or <see langword="null"/> when there is no qualifying
        /// purchase at all.
        /// </summary>
        public BestKnownPrice? BestKnown { get; init; }

        /// <summary>
        /// The most recent purchase's unit price divided by the average unit price of the group it
        /// belongs to, so a value above 1 means "you paid more than you usually do". Deliberately
        /// a ratio rather than a boolean: the client decides what margin is worth mentioning (the
        /// price UI uses 1.1), and a server-side threshold would hard-code that product decision
        /// into the wire format. <see langword="null"/> when the latest purchase's group holds only
        /// that one purchase - a single data point is not an average to be above.
        /// </summary>
        public decimal? LatestAboveAverageRatio { get; init; }
    }
}
