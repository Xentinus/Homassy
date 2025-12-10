using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public class CreateInventoryItemRequest
    {
        [Required]
        public required Guid ProductPublicId { get; set; }

        public bool IsSharedWithFamily { get; set; } = false;

        public Guid? StorageLocationPublicId { get; set; }

        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public required decimal Quantity { get; set; }

        [EnumDataType(typeof(Unit))]
        public Unit Unit { get; set; } = Unit.Piece;

        public DateTime? ExpirationAt { get; set; }

        public int? Price { get; set; }

        [EnumDataType(typeof(Currency))]
        public Currency? Currency { get; set; }

        public Guid? ShoppingLocationPublicId { get; set; }

        [StringLength(64)]
        public string? ReceiptNumber { get; set; }
    }
}
