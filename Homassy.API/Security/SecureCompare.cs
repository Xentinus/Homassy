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
    }
}
