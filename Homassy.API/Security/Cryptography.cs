using System.Security.Cryptography;

namespace Homassy.API.Security
{
    public static class Cryptography
    {
        public static string GenerateShareCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ1234567890";
            var codeLength = 8;

            var code = new char[codeLength];
            var randomBytes = new byte[codeLength];

            RandomNumberGenerator.Fill(randomBytes);

            for (int i = 0; i < codeLength; i++)
            {
                code[i] = chars[randomBytes[i] % chars.Length];
            }

            return new string(code);
        }
    }
}
