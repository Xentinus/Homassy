// `UserTimeZone` is a value import, not a type-only one: the timezone map below names its members
// rather than repeating their numbers.
import { UserTimeZone } from '~/types/enums'
import type { Language, Currency } from '~/types/enums'

/**
 * Valid Kratos timezone values (Windows-style timezone IDs)
 */
export type KratosTimezone = 
  | 'CentralEuropeStandardTime'
  | 'GMTStandardTime'
  | 'EasternStandardTime'
  | 'PacificStandardTime'
  | 'WEuropeStandardTime'
  | 'TokyoStandardTime'

/**
 * Map IANA timezone to Kratos timezone string
 * Kratos identity schema expects Windows-style timezone IDs
 * @param ianaTimezone - IANA timezone identifier (e.g., "Europe/Budapest")
 * @returns Kratos timezone string
 */
export function ianaToKratosTimezone(ianaTimezone: string): KratosTimezone {
  const map: Record<string, KratosTimezone> = {
    // Central Europe (CET/CEST)
    'Europe/Budapest': 'CentralEuropeStandardTime',
    'Europe/Prague': 'CentralEuropeStandardTime',
    'Europe/Vienna': 'CentralEuropeStandardTime',
    'Europe/Warsaw': 'CentralEuropeStandardTime',
    'Europe/Bratislava': 'CentralEuropeStandardTime',
    'Europe/Ljubljana': 'CentralEuropeStandardTime',
    'Europe/Zagreb': 'CentralEuropeStandardTime',
    'Europe/Belgrade': 'CentralEuropeStandardTime',
    'Europe/Sarajevo': 'CentralEuropeStandardTime',
    'Europe/Skopje': 'CentralEuropeStandardTime',
    
    // Western Europe (CET/CEST but different Windows zone)
    'Europe/Berlin': 'WEuropeStandardTime',
    'Europe/Paris': 'WEuropeStandardTime',
    'Europe/Amsterdam': 'WEuropeStandardTime',
    'Europe/Brussels': 'WEuropeStandardTime',
    'Europe/Rome': 'WEuropeStandardTime',
    'Europe/Madrid': 'WEuropeStandardTime',
    'Europe/Zurich': 'WEuropeStandardTime',
    'Europe/Stockholm': 'WEuropeStandardTime',
    'Europe/Oslo': 'WEuropeStandardTime',
    'Europe/Copenhagen': 'WEuropeStandardTime',
    
    // GMT/BST
    'Europe/London': 'GMTStandardTime',
    'Europe/Dublin': 'GMTStandardTime',
    'Europe/Lisbon': 'GMTStandardTime',
    'Atlantic/Reykjavik': 'GMTStandardTime',
    'UTC': 'GMTStandardTime',
    'Etc/UTC': 'GMTStandardTime',
    'Etc/GMT': 'GMTStandardTime',
    
    // Eastern US
    'America/New_York': 'EasternStandardTime',
    'America/Detroit': 'EasternStandardTime',
    'America/Toronto': 'EasternStandardTime',
    'America/Montreal': 'EasternStandardTime',
    
    // Pacific US
    'America/Los_Angeles': 'PacificStandardTime',
    'America/Vancouver': 'PacificStandardTime',
    'America/Tijuana': 'PacificStandardTime',
    
    // Japan
    'Asia/Tokyo': 'TokyoStandardTime',
    'Asia/Seoul': 'TokyoStandardTime',
  }
  
  return map[ianaTimezone] ?? 'CentralEuropeStandardTime'
}

/**
 * Get user's timezone as Kratos timezone string
 * Uses browser's Intl API to detect timezone
 * @returns Kratos timezone string
 */
export function getBrowserKratosTimezone(): KratosTimezone {
  try {
    const ianaTimezone = Intl.DateTimeFormat().resolvedOptions().timeZone
    return ianaToKratosTimezone(ianaTimezone)
  } catch {
    return 'CentralEuropeStandardTime'
  }
}

/**
 * Map language code to Language enum
 * @param code - Language code (e.g., "hu", "de", "en")
 * @returns Language enum value
 */
export function languageCodeToEnum(code: string): Language {
  const map: Record<string, Language> = {
    'hu': 0, // Hungarian
    'de': 1, // German
    'en': 2  // English
  }
  return map[code.toLowerCase()] ?? 0
}

/**
 * Map currency code to Currency enum
 * @param code - Currency code (e.g., "HUF", "EUR", "USD")
 * @returns Currency enum value
 */
export function currencyCodeToEnum(code: string): Currency {
  const map: Record<string, Currency> = {
    'HUF': 135,
    'EUR': 105,
    'USD': 279
  }
  return map[code.toUpperCase()] ?? 135
}

/**
 * Map timezone ID to UserTimeZone enum
 * @param tzId - IANA timezone identifier (e.g., "Europe/Budapest")
 * @returns UserTimeZone enum value
 */
export function timeZoneIdToEnum(tzId: string): UserTimeZone {
  const map: Record<string, UserTimeZone> = {
    'UTC': UserTimeZone.Utc,
    'America/New_York': UserTimeZone.EasternStandardTime,
    'America/Chicago': UserTimeZone.CentralStandardTime,
    'America/Denver': UserTimeZone.MountainStandardTime,
    'America/Los_Angeles': UserTimeZone.PacificStandardTime,
    'America/Anchorage': UserTimeZone.AlaskanStandardTime,
    'Pacific/Honolulu': UserTimeZone.HawaiianStandardTime,
    'America/Halifax': UserTimeZone.AtlanticStandardTime,
    'America/Argentina/Buenos_Aires': UserTimeZone.ArgentinaStandardTime,
    'America/Sao_Paulo': UserTimeZone.BrazilianStandardTime,
    'Europe/London': UserTimeZone.GreenwichStandardTime,
    'Europe/Budapest': UserTimeZone.CentralEuropeStandardTime,
    'Europe/Athens': UserTimeZone.EasternEuropeStandardTime,
    'Europe/Moscow': UserTimeZone.RussianStandardTime,
    'Europe/Istanbul': UserTimeZone.TurkeyStandardTime,
    'Asia/Dubai': UserTimeZone.ArabianStandardTime,
    'Asia/Karachi': UserTimeZone.PakistanStandardTime,
    'Asia/Kolkata': UserTimeZone.IndiaStandardTime,
    'Asia/Dhaka': UserTimeZone.BangladeshStandardTime,
    'Asia/Shanghai': UserTimeZone.ChinaStandardTime,
    'Asia/Singapore': UserTimeZone.SingaporeStandardTime,
    'Asia/Tokyo': UserTimeZone.TokyoStandardTime,
    'Asia/Seoul': UserTimeZone.KoreaStandardTime,
    'Australia/Perth': UserTimeZone.AustralianWesternStandardTime,
    'Australia/Adelaide': UserTimeZone.AustralianCentralStandardTime,
    'Australia/Sydney': UserTimeZone.AustralianEasternStandardTime,
    'Pacific/Auckland': UserTimeZone.NewZealandStandardTime,
    'Africa/Johannesburg': UserTimeZone.SouthAfricaStandardTime,
    'Africa/Cairo': UserTimeZone.EgyptStandardTime,
    'Africa/Lagos': UserTimeZone.WestAfricaStandardTime,
    'Asia/Jerusalem': UserTimeZone.IsraelStandardTime,
    'Asia/Riyadh': UserTimeZone.SaudiArabiaStandardTime,
    'Asia/Tehran': UserTimeZone.IranStandardTime
  }
  return map[tzId] ?? UserTimeZone.CentralEuropeStandardTime
}
