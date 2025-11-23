
using Homassy.API.Enums;

namespace Homassy.API.Entities
{
    public class ShoppingListItem : BaseEntity
    {
        public int? FamilyId { get; set; }
        public int? UserId { get; set; }
        public int? ProductId { get; set; }
        public int? ShoppingLocationId { get; set; }
        public string? CustomName { get; set; }
        public required decimal Quantity { get; set; } = 1.0m;
        public required Unit Unit { get; set; } = Unit.Gram;
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? PurchasedAt { get; set; }
        public DateTime? DeadlineAt { get; set; }
        public DateTime? DueAt { get; set; }
    }
}
