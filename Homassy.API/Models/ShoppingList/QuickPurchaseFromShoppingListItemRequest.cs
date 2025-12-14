using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ShoppingList
{
    public class QuickPurchaseFromShoppingListItemRequest
    {
        [Required]
        public required Guid ShoppingListItemPublicId { get; set; }

        [Required]
        public required DateTime PurchasedAt { get; set; }

        [Required]
        [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public required decimal Quantity { get; set; }

        public int? Price { get; set; }

        [EnumDataType(typeof(Currency))]
        public Currency? Currency { get; set; }

        public Guid? StorageLocationPublicId { get; set; }

        public DateTime? ExpirationAt { get; set; }

        public bool IsSharedWithFamily { get; set; } = false;
    }
}
