namespace Homassy.API.Models.Product
{
    /// <summary>
    /// Lightweight product projection for the Készletek grid: only the fields a card renders,
    /// plus its in-scope inventory items. Used for the realtime snapshot (<see cref="Hubs.InventoryHub.JoinInventory"/>)
    /// and, with an empty item list, as the product carrier on an <c>InventoryUpserted</c> broadcast.
    ///
    /// Note: <see cref="IsFavorite"/> is per-user (backed by <c>ProductCustomization</c>). It is
    /// correct in the snapshot, but not meaningful in a group broadcast — recipients default it to
    /// <c>false</c> on insert and rely on the per-user favorite event / next load for the real value.
    /// </summary>
    public class InventoryGridProductInfo
    {
        public Guid PublicId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public bool IsEatable { get; set; }
        public bool IsFavorite { get; set; }
        public List<InventoryGridItemInfo> InventoryItems { get; set; } = new();
    }
}
