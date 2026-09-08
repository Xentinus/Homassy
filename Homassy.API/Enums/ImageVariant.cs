namespace Homassy.API.Enums
{
    /// <summary>
    /// Which stored rendition of an image an <c>?size=</c> query is asking for.
    /// </summary>
    public enum ImageVariant
    {
        /// <summary>The image as it was stored on upload (capped by the upload's processing options).</summary>
        Full = 0,

        /// <summary>A small square crop, generated on upload. What list-level UI asks for.</summary>
        Thumb = 1
    }
}
