using Homassy.API.Entities.Product;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Location
{
    public class StorageLocation  : LocationBase
    {
        public bool IsFreezer { get; set; } = false;

        // Navigation properties
        public ICollection<ProductInventoryItem>? InventoryItems { get; set; }
    }
}
