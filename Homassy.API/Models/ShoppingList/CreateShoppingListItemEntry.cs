using Homassy.API.Attributes.Validation;
using Homassy.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    public class CreateShoppingListItemEntry
    {
        public Guid? ProductPublicId { get; set; }

        public Guid? ShoppingLocationPublicId { get; set; }

        [StringLength(255, MinimumLength = 4)]
        [SanitizedString]
        public string? CustomName { get; set; }

        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public required decimal Quantity { get; set; } = 1.0m;

        // Only used for standalone/custom items (no ProductPublicId). For product-linked
        // items the unit is inherited from the product and this value is ignored.
        [EnumDataType(typeof(Unit))]
        public Unit? Unit { get; set; }

        [StringLength(255)]
        [SanitizedString]
        public string? Note { get; set; }

        [Url]
        [StringLength(1024)]
        public string? Url { get; set; }

        // Set together with EstimatedPriceCurrency or not at all — enforced in
        // ShoppingListFunctions, because DataAnnotations cannot express a two-property rule.
        [Range(0, 99999999)]
        public decimal? EstimatedUnitPrice { get; set; }

        [EnumDataType(typeof(Currency))]
        public Currency? EstimatedPriceCurrency { get; set; }

        public DateTime? DeadlineAt { get; set; }

        public DateTime? DueAt { get; set; }
    }
}
