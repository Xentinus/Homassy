using Homassy.API.Attributes.Validation;
using Homassy.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    public class UpdateShoppingListItemRequest
    {
        public Guid? ProductPublicId { get; set; }

        public Guid? ShoppingLocationPublicId { get; set; }

        // When true, removes the item's shopping location (set to "no location").
        // Needed because a null ShoppingLocationPublicId means "no change", not "clear".
        public bool? ClearShoppingLocation { get; set; }

        [StringLength(255, MinimumLength = 2)]
        [SanitizedString]
        public string? CustomName { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal? Quantity { get; set; }

        [EnumDataType(typeof(Unit))]
        public Unit? Unit { get; set; }

        [StringLength(255)]
        [SanitizedString]
        public string? Note { get; set; }

        [Url]
        [StringLength(1024)]
        public string? Url { get; set; }

        // A null Url means "no change", so removing one needs its own flag — same reason
        // ClearShoppingLocation exists above.
        public bool? ClearUrl { get; set; }

        [Range(0, 99999999)]
        public decimal? EstimatedUnitPrice { get; set; }

        [EnumDataType(typeof(Currency))]
        public Currency? EstimatedPriceCurrency { get; set; }

        // Clears the price and the currency together: they are one value.
        public bool? ClearEstimatedPrice { get; set; }

        public DateTime? PurchasedAt { get; set; }

        public DateTime? DeadlineAt { get; set; }

        public DateTime? DueAt { get; set; }
    }
}
