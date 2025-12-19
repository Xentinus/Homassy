using Homassy.API.Attributes.Validation;
using Homassy.API.Enums;
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

        [Required]
        [EnumDataType(typeof(Unit))]
        public required Unit Unit { get; set; } = Unit.Piece;

        [StringLength(255)]
        [SanitizedString]
        public string? Note { get; set; }

        public DateTime? DeadlineAt { get; set; }

        public DateTime? DueAt { get; set; }
    }
}
