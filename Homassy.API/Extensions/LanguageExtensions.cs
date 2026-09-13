using Homassy.Data.Enums;

namespace Homassy.API.Extensions
{
    public static class LanguageExtensions
    {
        public static string ToLanguageCode(this Language language)
        {
            return language switch
            {
                Language.Hungarian => "hu",
                Language.German => "de",
                Language.English => "en",
                _ => "hu"
            };
        }

        public static Language FromLanguageCode(string code)
        {
            return code?.ToLower() switch
            {
                "hu" => Language.Hungarian,
                "de" => Language.German,
                "en" => Language.English,
                _ => Language.Hungarian
            };
        }
    }
}
