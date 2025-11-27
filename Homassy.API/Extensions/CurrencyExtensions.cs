using Homassy.API.Enums;

namespace Homassy.API.Extensions
{
    public static class CurrencyExtensions
    {
        public static string ToCurrencyCode(this Currency currency)
        {
            return currency.ToString().ToUpperInvariant();
        }

        public static Currency FromCurrencyCode(string currencyCode)
        {
            if (Enum.TryParse<Currency>(currencyCode, ignoreCase: true, out var currency))
            {
                return currency;
            }

            // Default fallback
            return Currency.Huf;
        }
    }
}
