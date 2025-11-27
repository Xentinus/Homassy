using Homassy.API.Enums;

namespace Homassy.API.Extensions
{
    public static class UserTimeZoneExtensions
    {
        public static string ToTimeZoneId(this UserTimeZone timeZone)
        {
            return timeZone switch
            {
                // UTC
                UserTimeZone.UTC => "UTC",

                // Americas
                UserTimeZone.EasternStandardTime => "America/New_York",
                UserTimeZone.CentralStandardTime => "America/Chicago",
                UserTimeZone.MountainStandardTime => "America/Denver",
                UserTimeZone.PacificStandardTime => "America/Los_Angeles",
                UserTimeZone.AlaskanStandardTime => "America/Anchorage",
                UserTimeZone.HawaiianStandardTime => "Pacific/Honolulu",
                UserTimeZone.AtlanticStandardTime => "America/Halifax",
                UserTimeZone.ArgentinaStandardTime => "America/Argentina/Buenos_Aires",
                UserTimeZone.BrazilianStandardTime => "America/Sao_Paulo",

                // Europe
                UserTimeZone.GreenwichStandardTime => "Europe/London",
                UserTimeZone.CentralEuropeStandardTime => "Europe/Budapest",
                UserTimeZone.EasternEuropeStandardTime => "Europe/Athens",
                UserTimeZone.RussianStandardTime => "Europe/Moscow",
                UserTimeZone.TurkeyStandardTime => "Europe/Istanbul",

                // Asia
                UserTimeZone.ArabianStandardTime => "Asia/Dubai",
                UserTimeZone.PakistanStandardTime => "Asia/Karachi",
                UserTimeZone.IndiaStandardTime => "Asia/Kolkata",
                UserTimeZone.BangladeshStandardTime => "Asia/Dhaka",
                UserTimeZone.ChinaStandardTime => "Asia/Shanghai",
                UserTimeZone.SingaporeStandardTime => "Asia/Singapore",
                UserTimeZone.TokyoStandardTime => "Asia/Tokyo",
                UserTimeZone.KoreaStandardTime => "Asia/Seoul",

                // Australia & Pacific
                UserTimeZone.AustralianWesternStandardTime => "Australia/Perth",
                UserTimeZone.AustralianCentralStandardTime => "Australia/Adelaide",
                UserTimeZone.AustralianEasternStandardTime => "Australia/Sydney",
                UserTimeZone.NewZealandStandardTime => "Pacific/Auckland",

                // Africa
                UserTimeZone.SouthAfricaStandardTime => "Africa/Johannesburg",
                UserTimeZone.EgyptStandardTime => "Africa/Cairo",
                UserTimeZone.WestAfricaStandardTime => "Africa/Lagos",

                // Middle East
                UserTimeZone.IsraelStandardTime => "Asia/Jerusalem",
                UserTimeZone.SaudiArabiaStandardTime => "Asia/Riyadh",
                UserTimeZone.IranStandardTime => "Asia/Tehran",

                _ => "Europe/Budapest"
            };
        }

        public static UserTimeZone FromTimeZoneId(string timeZoneId)
        {
            return timeZoneId switch
            {
                "UTC" => UserTimeZone.UTC,

                // Americas
                "America/New_York" => UserTimeZone.EasternStandardTime,
                "America/Chicago" => UserTimeZone.CentralStandardTime,
                "America/Denver" => UserTimeZone.MountainStandardTime,
                "America/Los_Angeles" => UserTimeZone.PacificStandardTime,
                "America/Anchorage" => UserTimeZone.AlaskanStandardTime,
                "Pacific/Honolulu" => UserTimeZone.HawaiianStandardTime,
                "America/Halifax" => UserTimeZone.AtlanticStandardTime,
                "America/Argentina/Buenos_Aires" => UserTimeZone.ArgentinaStandardTime,
                "America/Sao_Paulo" => UserTimeZone.BrazilianStandardTime,

                // Europe
                "Europe/London" => UserTimeZone.GreenwichStandardTime,
                "Europe/Budapest" => UserTimeZone.CentralEuropeStandardTime,
                "Europe/Athens" => UserTimeZone.EasternEuropeStandardTime,
                "Europe/Moscow" => UserTimeZone.RussianStandardTime,
                "Europe/Istanbul" => UserTimeZone.TurkeyStandardTime,

                // Asia
                "Asia/Dubai" => UserTimeZone.ArabianStandardTime,
                "Asia/Karachi" => UserTimeZone.PakistanStandardTime,
                "Asia/Kolkata" => UserTimeZone.IndiaStandardTime,
                "Asia/Dhaka" => UserTimeZone.BangladeshStandardTime,
                "Asia/Shanghai" => UserTimeZone.ChinaStandardTime,
                "Asia/Singapore" => UserTimeZone.SingaporeStandardTime,
                "Asia/Tokyo" => UserTimeZone.TokyoStandardTime,
                "Asia/Seoul" => UserTimeZone.KoreaStandardTime,

                // Australia & Pacific
                "Australia/Perth" => UserTimeZone.AustralianWesternStandardTime,
                "Australia/Adelaide" => UserTimeZone.AustralianCentralStandardTime,
                "Australia/Sydney" => UserTimeZone.AustralianEasternStandardTime,
                "Pacific/Auckland" => UserTimeZone.NewZealandStandardTime,

                // Africa
                "Africa/Johannesburg" => UserTimeZone.SouthAfricaStandardTime,
                "Africa/Cairo" => UserTimeZone.EgyptStandardTime,
                "Africa/Lagos" => UserTimeZone.WestAfricaStandardTime,

                // Middle East
                "Asia/Jerusalem" => UserTimeZone.IsraelStandardTime,
                "Asia/Riyadh" => UserTimeZone.SaudiArabiaStandardTime,
                "Asia/Tehran" => UserTimeZone.IranStandardTime,

                _ => UserTimeZone.CentralEuropeStandardTime
            };
        }
    }
}
