using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public class QuickAddInventoryItemRequest
    {
        [Required]
        public required Guid ProductPublicId { get; set; }

        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public required decimal Quantity { get; set; }

        [EnumDataType(typeof(Unit))]
        public Unit Unit { get; set; } = Unit.Piece;

        public bool IsSharedWithFamily { get; set; } = false;
    }
}
