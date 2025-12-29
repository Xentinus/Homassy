import type { Language, Currency, UserTimeZone } from '~/types/enums'

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
