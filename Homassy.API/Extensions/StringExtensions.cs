using System.Globalization;
using System.Text;

namespace Homassy.API.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Removes diacritics (accents) from a string and converts to lowercase for search comparison.
        /// </summary>
        public static string NormalizeForSearch(this string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString()
                .Normalize(NormalizationForm.FormC)
                .ToLowerInvariant();
        }
    }
}
