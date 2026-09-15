using Homassy.Data.Enums;
using Homassy.API.Models.Location;
using Homassy.API.Models.Product;
using System.Text.Json.Serialization;

namespace Homassy.API.Models.ShoppingList
{
    public class ShoppingListItemInfo
    {
        public Guid PublicId { get; set; }
        public Guid ShoppingListPublicId { get; set; }
        public Guid? ProductPublicId { get; set; }
        public Guid? ShoppingLocationPublicId { get; set; }
        public ProductInfo? Product { get; set; }
        public ShoppingLocationInfo? ShoppingLocation { get; set; }
        public string? CustomName { get; set; }
        public decimal Quantity { get; set; }
        public Unit Unit { get; set; }
        public string? Note { get; set; }
        public string? Url { get; set; }
        public decimal? EstimatedUnitPrice { get; set; }

        /// <summary>
        /// Serialized as the enum's NAME (e.g. "Huf"), like
        /// <c>PriceHistoryResponse.BestKnownPrice.Currency</c>. The web estimate keys its
        /// per-currency totals by this string and passes it to <c>Intl.NumberFormat</c>, which
        /// accepts "Huf" and rejects the "135" a plain enum would serialize to.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter<Currency>))]
        public Currency? EstimatedPriceCurrency { get; set; }

        /// <summary>Manual (aisle) position — see <c>ShoppingListItem.SortOrder</c>. Sparse, not an index.</summary>
        public int SortOrder { get; set; }
        public DateTime? PurchasedAt { get; set; }
        public DateTime? DeadlineAt { get; set; }
        public DateTime? DueAt { get; set; }
    }
}
