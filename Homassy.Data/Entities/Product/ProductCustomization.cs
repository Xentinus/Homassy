using Homassy.Data.Entities.Common;
using Homassy.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.Data.Entities.Product
{
    public class ProductCustomization : RecordChangeEntity
    {
        [ForeignKey(nameof(Product))]
        public required int ProductId { get; set; }

        public int? UserId { get; set; }

        [StringLength(128)]
        public string? Notes { get; set; }
        public bool IsFavorite { get; set; } = false;

        // Navigation
        public Product Product { get; set; } = null!;
    }
}
