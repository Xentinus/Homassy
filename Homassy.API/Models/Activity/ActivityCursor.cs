using System.Text;

namespace Homassy.API.Models.Activity
{
    /// <summary>
    /// Opaque pagination cursor for the activity timeline. Encodes the <c>(Timestamp, PublicId)</c>
    /// of the last activity row a page actually consumed, so the next page can ask the database for
    /// strictly-older rows instead of relying on a numeric page offset that a newly inserted activity
    /// would shift out from under the client.
    /// </summary>
    /// <remarks>
    /// The cursor carries the row's <see cref="Guid"/> <c>PublicId</c> alongside the timestamp, not
    /// the timestamp alone. Two activities can be recorded in the very same <see cref="DateTime.Ticks"/>
    /// tick (bulk imports commonly do), and SQL makes no ordering promise among rows an
    /// <c>ORDER BY</c> treats as equal. Without a tiebreaker, the page boundary would fall inside
    /// that tied group non-deterministically - the next page's "give me everything older than the
    /// cursor" could re-include a row already shown (a duplicate) or step past one that was never
    /// shown (a skip), depending on which of the tied rows the database happened to return first.
    /// Pairing the timestamp with the (unique) <c>PublicId</c> and comparing them as a compound key
    /// makes the ordering - and therefore the page boundary - a strict total order.
    /// </remarks>
    public static class ActivityCursor
    {
        private const char Separator = ':';

        /// <summary>
        /// Encodes <paramref name="timestamp"/> and <paramref name="publicId"/> as a URL-safe
        /// (base64url, RFC 4648 &#167;5) opaque string. URL-safe because the cursor rides in a
        /// query string, where '+', '/' and '=' would all need percent-encoding.
        /// </summary>
        public static string Encode(DateTime timestamp, Guid publicId)
        {
            var raw = $"{timestamp.Ticks}{Separator}{publicId}";
            var bytes = Encoding.UTF8.GetBytes(raw);

            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        /// <summary>
        /// Attempts to decode a cursor produced by <see cref="Encode"/>. Returns <c>false</c> - never
        /// throws - for a null/empty string, invalid base64url, or a payload that does not parse as
        /// <c>"{ticks}:{guid}"</c>, so a caller can turn an undecodable cursor into a client error
        /// instead of a crash.
        /// </summary>
        public static bool TryDecode(string? cursor, out DateTime timestamp, out Guid publicId)
        {
            timestamp = default;
            publicId = default;

            if (string.IsNullOrEmpty(cursor))
                return false;

            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 0:
                    break;
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
                default:
                    // Length % 4 == 1 is not a valid base64 payload under any padding.
                    return false;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return false;
            }

            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return false;
            }

            var parts = decoded.Split(Separator, 2);
            if (parts.Length != 2)
                return false;

            if (!long.TryParse(parts[0], out var ticks))
                return false;

            if (!Guid.TryParse(parts[1], out var guid))
                return false;

            try
            {
                timestamp = new DateTime(ticks, DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            publicId = guid;
            return true;
        }
    }
}
