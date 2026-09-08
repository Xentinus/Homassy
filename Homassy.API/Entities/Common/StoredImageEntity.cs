using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Common
{
    /// <summary>
    /// One uploaded picture, kept in a table of its own rather than as a column on the row it
    /// belongs to.
    /// </summary>
    /// <remarks>
    /// The owning rows (<c>Products</c>, <c>UserProfiles</c>) are held in the Functions layer's
    /// process-wide caches in full, so a blob column on them is a blob column resident in memory
    /// for every product a family owns. It also rode along in every payload that mentioned the
    /// row. Both problems are the same problem: the bytes were part of the entity. Here they are
    /// not — the owning row keeps only <c>…PictureVersion</c>, which is all a URL needs, and
    /// these rows are read by the image endpoints and nothing else.
    /// <para>
    /// <see cref="Version"/> is what makes the URLs cacheable: it changes when the bytes change,
    /// so the URL changes with it and a client can be told to keep the answer indefinitely.
    /// </para>
    /// </remarks>
    public abstract class StoredImageEntity : BaseEntity
    {
        /// <summary>The image as stored on upload, already through <c>IImageProcessingService</c>.</summary>
        public required byte[] Data { get; set; }

        /// <summary>Encoding of <see cref="Data"/>, so serving it needs no magic-number sniffing.</summary>
        public ImageFormat Format { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// The small square crop list-level UI asks for. Generated on upload; null on rows that
        /// predate thumbnails, which the image endpoint backfills on the first request for one.
        /// </summary>
        public byte[]? ThumbnailData { get; set; }

        public ImageFormat ThumbnailFormat { get; set; }

        /// <summary>
        /// Short content hash of <see cref="Data"/>. Carried in the image URL as <c>?v=</c> and
        /// answered as the <c>ETag</c>, so a changed picture is a changed URL.
        /// </summary>
        [StringLength(32)]
        public required string Version { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
