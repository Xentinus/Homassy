using Homassy.API.Enums;

namespace Homassy.API.Constants
{
    /// <summary>
    /// Builds the paths the image endpoints are served on, so every DTO that carries a picture
    /// carries the same shape.
    /// </summary>
    /// <remarks>
    /// These are <b>paths</b>, not absolute URLs: the API is reached on a different origin in
    /// development and through a same-origin reverse proxy in production, and only the client
    /// knows which. The frontend prefixes its configured API base.
    /// <para>
    /// The <c>v</c> parameter is the stored image's content hash. It is what makes the answer
    /// safe to cache indefinitely — a new picture is a new URL, so nothing has to be invalidated.
    /// A null version means "no picture", and the builders return null rather than a URL that
    /// would 404.
    /// </para>
    /// </remarks>
    public static class MediaUrls
    {
        /// <summary>How long a client may keep an image URL's answer. A year, in seconds.</summary>
        public const int ImageCacheSeconds = 31_536_000;

        public static string? ProfilePicture(Guid publicId, string? version, ImageVariant variant = ImageVariant.Thumb)
        {
            return version == null
                ? null
                : $"/api/v1.0/User/{publicId}/profile-picture?size={Name(variant)}&v={version}";
        }

        public static string? ProductImage(Guid publicId, string? version, ImageVariant variant = ImageVariant.Thumb)
        {
            return version == null
                ? null
                : $"/api/v1.0/Product/{publicId}/image?size={Name(variant)}&v={version}";
        }

        /// <summary>
        /// The caller's family picture, addressed by <b>no</b> id at all.
        /// </summary>
        /// <remarks>
        /// A user has one family, and whether you may see its picture is exactly the question of
        /// whether you are in it - so the endpoint reads the family from the session and there is
        /// nothing for an id in the path to add. The <c>v</c> hash still makes the URL
        /// content-addressed, so a changed picture is a changed URL and leaving one family for
        /// another cannot show the old one out of a cache.
        /// </remarks>
        public static string? FamilyPicture(string? version, ImageVariant variant = ImageVariant.Thumb)
        {
            return version == null
                ? null
                : $"/api/v1.0/Family/picture?size={Name(variant)}&v={version}";
        }

        /// <summary>
        /// The picture behind a chat image message (#147), addressed by the <b>message's</b> public
        /// id rather than the image row's.
        /// </summary>
        /// <remarks>
        /// The message id is what a client already has (it is in the payload it just rendered), and
        /// it is what the access check needs anyway: whether you may see this picture is entirely a
        /// question of whether you may see the message it belongs to.
        /// </remarks>
        public static string? FamilyChatImage(Guid messagePublicId, string? version, ImageVariant variant = ImageVariant.Thumb)
        {
            return version == null
                ? null
                : $"/api/v1.0/FamilyChat/messages/{messagePublicId}/image?size={Name(variant)}&v={version}";
        }

        private static string Name(ImageVariant variant) => variant == ImageVariant.Thumb ? "thumb" : "full";
    }
}
