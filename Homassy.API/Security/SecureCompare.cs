using System.Security.Cryptography;
using System.Text;

namespace Homassy.API.Security
{
    public static class SecureCompare
    {
        public static bool ConstantTimeEquals(string? a, string? b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);

            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }

        public static bool ConstantTimeEqualsIgnoreCase(string? a, string? b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            var aUpper = a.ToUpperInvariant();
            var bUpper = b.ToUpperInvariant();

            var aBytes = Encoding.UTF8.GetBytes(aUpper);
            var bBytes = Encoding.UTF8.GetBytes(bUpper);

            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}
