using Homassy.API.Enums;
using Serilog;

namespace Homassy.API.Extensions
{
    public static class UserTimeZoneExtensions
    {
        /// <summary>
        /// Resolves the zone, falling back to UTC so an id the host cannot map takes down neither a request
        /// nor a background worker.
        /// </summary>
        public static TimeZoneInfo ToTimeZoneInfo(this UserTimeZone timeZone)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZone.ToTimeZoneId());
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unknown timezone {TimeZone}; falling back to UTC", timeZone);
                return TimeZoneInfo.Utc;
            }
        }

        /// <summary>
        /// Converts a wall-clock local time to its absolute UTC instant.
        /// <para>
        /// A local time inside a DST spring-forward gap does not exist, and
        /// <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/> throws on it, so the value is
        /// nudged forward onto the first instant that does exist (a 02:30 reminder on the gap day fires at
        /// 03:00). An *ambiguous* local time — the fall-back hour, which happens twice — is left to
        /// <c>ConvertTimeToUtc</c>, which silently picks the standard offset. That is deliberate: the reminder
        /// fires once, at the later 02:30, which is the reasonable reading of "02:30 that day".
        /// </para>
        /// </summary>
        public static DateTime LocalToUtc(this TimeZoneInfo timeZone, DateTime local)
        {
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

            while (timeZone.IsInvalidTime(local))
            {
                local = local.AddMinutes(1);
            }

            return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
        }

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

        /// <summary>
        /// Converts UserTimeZone to Kratos identity schema timezone enum.
        /// Kratos only supports: CentralEuropeStandardTime, GMTStandardTime, EasternStandardTime,
        /// PacificStandardTime, WEuropeStandardTime, TokyoStandardTime
        /// </summary>
        public static string ToKratosTimezoneEnum(this UserTimeZone timeZone)
        {
            return timeZone switch
            {
                // Direct mappings
                UserTimeZone.CentralEuropeStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.GreenwichStandardTime => "GMTStandardTime",
                UserTimeZone.EasternStandardTime => "EasternStandardTime",
                UserTimeZone.PacificStandardTime => "PacificStandardTime",
                UserTimeZone.TokyoStandardTime => "TokyoStandardTime",

                // Map to closest Kratos enum
                UserTimeZone.UTC => "GMTStandardTime",
                UserTimeZone.CentralStandardTime => "EasternStandardTime",
                UserTimeZone.MountainStandardTime => "PacificStandardTime",
                UserTimeZone.AlaskanStandardTime => "PacificStandardTime",
                UserTimeZone.HawaiianStandardTime => "PacificStandardTime",
                UserTimeZone.AtlanticStandardTime => "EasternStandardTime",
                UserTimeZone.ArgentinaStandardTime => "EasternStandardTime",
                UserTimeZone.BrazilianStandardTime => "EasternStandardTime",
                UserTimeZone.EasternEuropeStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.RussianStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.TurkeyStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.ArabianStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.PakistanStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.IndiaStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.BangladeshStandardTime => "TokyoStandardTime",
                UserTimeZone.ChinaStandardTime => "TokyoStandardTime",
                UserTimeZone.SingaporeStandardTime => "TokyoStandardTime",
                UserTimeZone.KoreaStandardTime => "TokyoStandardTime",
                UserTimeZone.AustralianWesternStandardTime => "TokyoStandardTime",
                UserTimeZone.AustralianCentralStandardTime => "TokyoStandardTime",
                UserTimeZone.AustralianEasternStandardTime => "TokyoStandardTime",
                UserTimeZone.NewZealandStandardTime => "TokyoStandardTime",
                UserTimeZone.SouthAfricaStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.EgyptStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.WestAfricaStandardTime => "GMTStandardTime",
                UserTimeZone.IsraelStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.SaudiArabiaStandardTime => "CentralEuropeStandardTime",
                UserTimeZone.IranStandardTime => "CentralEuropeStandardTime",

                _ => "CentralEuropeStandardTime"
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
