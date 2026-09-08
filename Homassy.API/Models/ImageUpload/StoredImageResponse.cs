namespace Homassy.API.Models.ImageUpload
{
    /// <summary>
    /// One rendition of a stored image, ready to be written to the response body.
    /// </summary>
    /// <remarks>
    /// Not an <c>ApiResponse&lt;T&gt;</c>: these endpoints answer with the bytes themselves, which
    /// is the whole point of moving images out of JSON. The envelope is still used for their
    /// error paths.
    /// </remarks>
    public record StoredImageResponse
    {
        public required byte[] Data { get; init; }

        public required string ContentType { get; init; }

        /// <summary>
        /// Strong validator for the exact bytes being returned — the stored image's content hash
        /// plus which rendition this is, so a thumbnail and a full-size image never share one.
        /// </summary>
        public required string ETag { get; init; }
    }
}
