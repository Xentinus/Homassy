/** Default reminder time offered for a new note — mirrors "morning of the day". */
export const DEFAULT_NOTE_REMINDER_TIME = '08:00'

/** `HH:mm`, 24-hour. Used by the note form's Zod schema and matches what the API's parser accepts. */
export const NOTE_REMINDER_TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/

/**
 * Timezone-safe `YYYY-MM-DD` for a `CalendarDate`-style triple. Deliberately not via `Date`/`toISOString()`,
 * which would shift the day for anyone east or west of UTC.
 */
export function toDateKey(year: number, month: number, day: number): string {
  const mm = String(month).padStart(2, '0')
  const dd = String(day).padStart(2, '0')
  return `${year}-${mm}-${dd}`
}

/** The API may serialize the note date as a bare date or with a time part; the day key is all the UI needs. */
export function noteDayKey(date: string): string {
  return date.split('T')[0] ?? date
}
