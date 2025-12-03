using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.Product
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
