using Homassy.API.Attributes.Validation;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    public class CreateShoppingListItemRequest
    {
        [Required]
        public required Guid ShoppingListPublicId { get; set; }

        public Guid? ProductPublicId { get; set; }

        public Guid? ShoppingLocationPublicId { get; set; }

        [StringLength(255, MinimumLength = 2)]
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

        public DateTime? DeadlineAt { get; set; }

        public DateTime? DueAt { get; set; }
    }
}
