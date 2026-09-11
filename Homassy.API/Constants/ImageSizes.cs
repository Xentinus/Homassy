namespace Homassy.API.Constants
{
    /// <summary>
    /// The pixel sizes the stored image renditions are generated at.
    /// </summary>
    /// <remarks>
    /// Each is roughly twice the largest box the frontend renders that rendition in, so a 2x
    /// display gets real pixels and nothing else is paid for. Changing a value here does not
    /// re-generate what is already stored — existing thumbnails keep their size until their
    /// picture is uploaded again.
    /// </remarks>
    public static class ImageSizes
    {
        /// <summary>
        /// Avatar thumbnails, square-cropped. Largest avatar on screen is the 64px profile
        /// identity card.
        /// </summary>
        public const int AvatarThumbnail = 128;

        /// <summary>
        /// Product card thumbnails, bounded rather than cropped. Largest card image is ~128px.
        /// </summary>
        public const int ProductThumbnail = 256;

        /// <summary>
        /// Chat picture thumbnails (#147), bounded rather than cropped - a photo of a shelf is
        /// wide and a receipt is tall, and either cropped square is useless.
        /// </summary>
        /// <remarks>
        /// Larger than the product thumbnail because a chat bubble renders it much bigger: the
        /// image fills most of a ~280px bubble, so 512 is the same "roughly twice the largest box"
        /// rule the other two follow. The full rendition is what the tap-to-open viewer loads.
        /// </remarks>
        public const int ChatThumbnail = 512;
    }
}
