using Homassy.API.Enums;

namespace Homassy.API.Functions
{
    public static class TimeZoneFunctions
    {
        private static readonly Dictionary<UserTimeZone, string> TimeZoneIdMap = new()
        {
            // UTC
            { UserTimeZone.UTC, "UTC" },

            // Americas
            { UserTimeZone.EasternStandardTime, "Eastern Standard Time" },
            { UserTimeZone.CentralStandardTime, "Central Standard Time" },
            { UserTimeZone.MountainStandardTime, "Mountain Standard Time" },
            { UserTimeZone.PacificStandardTime, "Pacific Standard Time" },
            { UserTimeZone.AlaskanStandardTime, "Alaskan Standard Time" },
            { UserTimeZone.HawaiianStandardTime, "Hawaiian Standard Time" },
            { UserTimeZone.AtlanticStandardTime, "Atlantic Standard Time" },
            { UserTimeZone.ArgentinaStandardTime, "Argentina Standard Time" },
            { UserTimeZone.BrazilianStandardTime, "E. South America Standard Time" },

            // Europe
            { UserTimeZone.GreenwichStandardTime, "GMT Standard Time" },
            { UserTimeZone.CentralEuropeStandardTime, "Central Europe Standard Time" },
            { UserTimeZone.EasternEuropeStandardTime, "E. Europe Standard Time" },
            { UserTimeZone.RussianStandardTime, "Russian Standard Time" },
            { UserTimeZone.TurkeyStandardTime, "Turkey Standard Time" },

            // Asia
            { UserTimeZone.ArabianStandardTime, "Arabian Standard Time" },
            { UserTimeZone.PakistanStandardTime, "Pakistan Standard Time" },
            { UserTimeZone.IndiaStandardTime, "India Standard Time" },
            { UserTimeZone.BangladeshStandardTime, "Bangladesh Standard Time" },
            { UserTimeZone.ChinaStandardTime, "China Standard Time" },
            { UserTimeZone.SingaporeStandardTime, "Singapore Standard Time" },
            { UserTimeZone.TokyoStandardTime, "Tokyo Standard Time" },
            { UserTimeZone.KoreaStandardTime, "Korea Standard Time" },

            // Australia & Pacific
            { UserTimeZone.AustralianWesternStandardTime, "W. Australia Standard Time" },
            { UserTimeZone.AustralianCentralStandardTime, "Cen. Australia Standard Time" },
            { UserTimeZone.AustralianEasternStandardTime, "AUS Eastern Standard Time" },
            { UserTimeZone.NewZealandStandardTime, "New Zealand Standard Time" },

            // Africa
            { UserTimeZone.SouthAfricaStandardTime, "South Africa Standard Time" },
            { UserTimeZone.EgyptStandardTime, "Egypt Standard Time" },
            { UserTimeZone.WestAfricaStandardTime, "W. Central Africa Standard Time" },

            // Middle East
            { UserTimeZone.IsraelStandardTime, "Israel Standard Time" },
            { UserTimeZone.SaudiArabiaStandardTime, "Arab Standard Time" },
            { UserTimeZone.IranStandardTime, "Iran Standard Time" }
        };

        public static string GetTimeZoneId(UserTimeZone timeZone)
        {
            return TimeZoneIdMap.TryGetValue(timeZone, out var id) ? id : "UTC";
        }

        public static TimeZoneInfo GetTimeZoneInfo(UserTimeZone timeZone)
        {
            var timeZoneId = GetTimeZoneId(timeZone);
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }

        public static string GetDisplayName(UserTimeZone timeZone)
        {
            var info = GetTimeZoneInfo(timeZone);
            return info.DisplayName;
        }

        public static string GetShortName(UserTimeZone timeZone)
        {
            var info = GetTimeZoneInfo(timeZone);
            return info.StandardName;
        }
    }
}
