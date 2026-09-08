using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Homassy.Tests.Infrastructure;

/// <summary>
/// Image payloads for tests that upload one.
/// </summary>
/// <remarks>
/// Generated rather than pasted in as a base64 literal, and generated as a real image rather than
/// a hand-assembled byte array. The tests used to share a 50x50 PNG literal whose IDAT chunk had
/// a bad CRC and an unusable zlib stream: it satisfied <c>ImageProcessingService.ValidateImage</c>,
/// which reads dimensions out of the header without decoding, so nothing noticed — until the
/// upload path started generating thumbnails, which needs the bytes to actually decode.
/// </remarks>
public static class TestImages
{
    /// <summary>
    /// A decodable PNG of the given size, base64-encoded the way an upload request carries it.
    /// </summary>
    /// <remarks>
    /// A gradient, not a flat fill, so the encoded bytes are not degenerate and a re-encode at a
    /// different size is genuinely different data.
    /// </remarks>
    public static string PngBase64(int width = 50, int height = 50)
    {
        using var image = new Image<Rgb24>(width, height);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgb24(
                        (byte)(255 - x * 255 / width),
                        (byte)(y * 255 / height),
                        (byte)(x * 255 / width));
                }
            }
        });

        using var buffer = new MemoryStream();
        image.SaveAsPng(buffer);
        return Convert.ToBase64String(buffer.ToArray());
    }
}
