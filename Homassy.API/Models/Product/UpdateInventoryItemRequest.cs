using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Product
{
    public class UpdateInventoryItemRequest
    {
        public Guid? StorageLocationPublicId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Quantity cannot be negative")]
        public decimal? Quantity { get; set; }

        [EnumDataType(typeof(Unit))]
        public Unit? Unit { get; set; }

        public DateTime? ExpirationAt { get; set; }

        public bool? IsSharedWithFamily { get; set; }

        public int? Price { get; set; }

        [EnumDataType(typeof(Currency))]
        public Currency? Currency { get; set; }

        public Guid? ShoppingLocationPublicId { get; set; }

        [StringLength(64)]
        public string? ReceiptNumber { get; set; }
    }
}
