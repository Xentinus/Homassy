using Homassy.API.Attributes.Validation;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    public class UpdateShoppingListItemRequest
    {
        public Guid? ProductPublicId { get; set; }

        public Guid? ShoppingLocationPublicId { get; set; }

        [StringLength(255, MinimumLength = 4)]
        [SanitizedString]
        public string? CustomName { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal? Quantity { get; set; }

        [EnumDataType(typeof(Unit))]
        public Unit? Unit { get; set; }

        [StringLength(255)]
        [SanitizedString]
        public string? Note { get; set; }

        public DateTime? PurchasedAt { get; set; }

        public DateTime? DeadlineAt { get; set; }

        public DateTime? DueAt { get; set; }
    }
}
