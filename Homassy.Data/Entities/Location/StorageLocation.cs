using Homassy.Data.Entities.Product;
using System.ComponentModel.DataAnnotations;

namespace Homassy.Data.Entities.Location
{
    public class StorageLocation  : LocationBase
    {
        public bool IsFreezer { get; set; } = false;

        // Navigation properties
        public ICollection<ProductInventoryItem>? InventoryItems { get; set; }
    }
}
