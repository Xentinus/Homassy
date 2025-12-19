using System.Text;
using System.Web;

namespace Homassy.API.Services.Sanitization
{
    public class InputSanitizationService : IInputSanitizationService
    {
        public string SanitizePlainText(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var encoded = HttpUtility.HtmlEncode(input);
            return NormalizeWhitespace(encoded);
        }

        public string? SanitizePlainTextOrNull(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            return SanitizePlainText(input);
        }

        private static string NormalizeWhitespace(string input)
        {
            var sb = new StringBuilder();
            var previousWasWhitespace = false;

            foreach (var c in input)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!previousWasWhitespace)
                    {
                        sb.Append(' ');
                        previousWasWhitespace = true;
                    }
                }
                else
                {
                    sb.Append(c);
                    previousWasWhitespace = false;
                }
            }

            return sb.ToString().Trim();
        }
    }
}
