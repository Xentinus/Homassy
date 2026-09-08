using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.Product
{
    /// <summary>
    /// A product's picture bytes. One row per product at most; see
    /// <see cref="StoredImageEntity"/> for why the bytes do not live on <see cref="Product"/>.
    /// </summary>
    /// <remarks>
    /// The case for the split is strongest here: product images are the largest ones the app
    /// stores and a list page shows many of them, so every product payload used to carry every
    /// image inline — again after each realtime refresh — and the process-wide product cache held
    /// them all in memory.
    /// </remarks>
    public class ProductImage : StoredImageEntity
    {
        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;
    }
}
