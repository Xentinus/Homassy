import type { Language, Currency, UserTimeZone } from '~/types/enums'

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
    'UTC': 0,
    'America/New_York': 1,
    'America/Chicago': 2,
    'America/Denver': 3,
    'America/Los_Angeles': 4,
    'America/Anchorage': 5,
    'Pacific/Honolulu': 6,
    'America/Halifax': 7,
    'America/Argentina/Buenos_Aires': 8,
    'America/Sao_Paulo': 9,
    'Europe/London': 10,
    'Europe/Budapest': 11,
    'Europe/Athens': 12,
    'Europe/Moscow': 13,
    'Europe/Istanbul': 14,
    'Asia/Dubai': 15,
    'Asia/Karachi': 16,
    'Asia/Kolkata': 17,
    'Asia/Dhaka': 18,
    'Asia/Shanghai': 19,
    'Asia/Singapore': 20,
    'Asia/Tokyo': 21,
    'Asia/Seoul': 22,
    'Australia/Perth': 23,
    'Australia/Adelaide': 24,
    'Australia/Sydney': 25,
    'Pacific/Auckland': 26,
    'Africa/Johannesburg': 27,
    'Africa/Cairo': 28,
    'Africa/Lagos': 29,
    'Asia/Jerusalem': 30,
    'Asia/Riyadh': 31,
    'Asia/Tehran': 32
  }
  return map[tzId] ?? 11 // Default: Europe/Budapest
}
