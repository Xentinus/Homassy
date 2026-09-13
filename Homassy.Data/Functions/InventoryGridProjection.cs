using Homassy.Data.Entities.Product;
using Homassy.Data.Models.Inventory;

namespace Homassy.Data.Functions
{
    /// <summary>
    /// The lightweight inventory projections the Készletek grid renders and the realtime
    /// broadcasts carry.
    /// </summary>
    /// <remarks>
    /// These live here, in the shared library, because both sides of the boundary build them: the
    /// API when an endpoint writes, and <c>Homassy.Notifications</c> when the automation worker
    /// creates an item on a schedule. A receiving client cannot tell which one sent it, so the two
    /// must project identically — which is only guaranteed if it is the same code (#91).
    /// <para>
    /// Genuinely static and cache-free, unlike most of the API's Functions layer. That is the
    /// property that makes them safe to call from a worker process, where the entity caches are
    /// never initialised and a cache-backed read would silently return nothing.
    /// </para>
    /// </remarks>
    public static class InventoryGridProjection
    {
        /// <summary>Builds the grid item projection used by the snapshot and realtime broadcasts.</summary>
        /// <param name="item">The inventory item to project.</param>
        /// <param name="productPublicId">Its product, which the projection does not navigate to.</param>
        /// <param name="originalQuantity">
        /// The purchased amount, for the card's stock ring. Callers with a context of their own pass
        /// it; the fallback reads the navigation, which is loaded on a tracked item and null on one
        /// that came out of a cache.
        /// </param>
        public static InventoryGridItemInfo BuildItem(
            ProductInventoryItem item,
            Guid productPublicId,
            decimal? originalQuantity = null) => new()
        {
            PublicId = item.PublicId,
            ProductPublicId = productPublicId,
            CurrentQuantity = item.CurrentQuantity,
            OriginalQuantity = originalQuantity ?? item.PurchaseInfo?.OriginalQuantity,
            Unit = item.Unit,
            ExpirationAt = item.ExpirationAt,
            IsSharedWithFamily = item.FamilyId.HasValue
        };

        /// <summary>
        /// Builds a grid product carrier (no items) for an <c>InventoryUpserted</c> broadcast, so a
        /// receiver can insert a new card for a product it hasn't seen yet.
        /// <see cref="InventoryGridProductInfo.IsFavorite"/> is left <c>false</c> - favorite is
        /// per-user and not meaningful in a group broadcast.
        /// </summary>
        public static InventoryGridProductInfo BuildProduct(Product product) => new()
        {
            PublicId = product.PublicId,
            Name = product.Name,
            Brand = product.Brand,
            Category = product.Category,
            Barcode = product.Barcode,
            IsEatable = product.IsEatable,
            IsFavorite = false,
            InventoryItems = new()
        };
    }
}
