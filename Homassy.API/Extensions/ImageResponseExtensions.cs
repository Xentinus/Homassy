using Homassy.API.Constants;
using Homassy.API.Models.ImageUpload;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Homassy.API.Extensions
{
    /// <summary>
    /// The HTTP half of serving a stored image: conditional requests, caching headers, and
    /// content negotiation. Shared so every image endpoint answers identically.
    /// </summary>
    public static class ImageResponseExtensions
    {
        /// <summary>
        /// Writes an image with a strong <c>ETag</c> and a year-long private cache lifetime, or a
        /// <c>304</c> when the client already has these exact bytes.
        /// </summary>
        /// <remarks>
        /// <c>private</c>, not <c>public</c>: the endpoints are behind <c>[Authorize]</c>, so a
        /// shared cache must not keep one family's picture and hand it to the next request.
        /// <c>immutable</c> is honest here because the URL carries the bytes' content hash — the
        /// answer for a given URL genuinely never changes.
        /// </remarks>
        public static IActionResult CacheableImage(this ControllerBase controller, StoredImageResponse image)
        {
            var response = controller.Response;
            response.Headers.CacheControl = $"private, max-age={MediaUrls.ImageCacheSeconds}, immutable";
            response.Headers.ETag = image.ETag;
            // The bytes vary with the request's Accept header (WebP thumbnail vs JPEG transcode),
            // so a cache keyed on the URL alone would be wrong. Appended, not assigned: the CORS
            // middleware has already put `Origin` here.
            response.Headers.Append(HeaderNames.Vary, HeaderNames.Accept);

            if (controller.Request.Headers.IfNoneMatch.Contains(image.ETag))
            {
                return controller.StatusCode(StatusCodes.Status304NotModified);
            }

            return controller.File(image.Data, image.ContentType);
        }

        /// <summary>
        /// Whether the request's <c>Accept</c> header allows <c>image/webp</c>.
        /// </summary>
        /// <remarks>
        /// Absent or wildcard <c>Accept</c> counts as accepting it: every browser that can run
        /// this app decodes WebP, and an <c>&lt;img&gt;</c> request that lists image types at all
        /// lists WebP. The check exists for the non-browser client that does not.
        /// </remarks>
        public static bool AcceptsWebp(this ControllerBase controller)
        {
            var accept = controller.Request.Headers.Accept;
            if (accept.Count == 0)
            {
                return true;
            }

            var joined = string.Join(',', accept.ToArray());
            if (joined.Length == 0)
            {
                return true;
            }

            return joined.Contains("image/webp", StringComparison.OrdinalIgnoreCase)
                || joined.Contains("image/*", StringComparison.OrdinalIgnoreCase)
                || joined.Contains("*/*", StringComparison.Ordinal);
        }
    }
}
