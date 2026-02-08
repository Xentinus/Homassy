using Homassy.API.Enums;

namespace Homassy.API.Extensions
{
    public static class CurrencyExtensions
    {
        public static string ToCurrencyCode(this Currency currency)
        {
            return currency.ToString().ToUpperInvariant();
        }

        /// <summary>
        /// Converts Currency to Kratos identity schema currency enum.
        /// Kratos only supports: HUF, EUR, USD, GBP, CHF, PLN, CZK, RON
        /// All other currencies (including cryptocurrencies) are mapped to EUR as fallback.
        /// </summary>
        public static string ToKratosCurrencyEnum(this Currency currency)
        {
            return currency switch
            {
                // Direct mappings to Kratos supported currencies
                Currency.Huf => "HUF",
                Currency.Eur => "EUR",
                Currency.Usd => "USD",
                Currency.Gbp => "GBP",
                Currency.Chf => "CHF",
                Currency.Pln => "PLN",
                Currency.Czk => "CZK",
                Currency.Ron => "RON",
                
                // All other currencies (crypto, other fiat) fallback to EUR
                _ => "EUR"
            };
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
