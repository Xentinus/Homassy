/** Lead times offered in the external calendar settings form, in minutes before the event starts. */
export const REMINDER_LEAD_TIME_PRESETS = [0, 5, 15, 30, 60, 1440]

/** Mirrors the API's default for `FamilyExternalCalendar.AllDayNotifyTime`. */
export const DEFAULT_ALL_DAY_NOTIFY_TIME = '08:00'

/**
 * i18n key + params for a reminder lead time, so the settings form and the calendar card word it the
 * same way. Falls back to minutes for any value that is not a whole number of hours or days (the API
 * accepts arbitrary minutes, the presets are only a shortcut).
 */
export function reminderLeadTimeLabel(minutes: number): { key: string, params: Record<string, number> } {
  const base = 'profile.family.externalCalendars'

  if (minutes === 0) return { key: `${base}.leadAtStart`, params: {} }
  if (minutes % 1440 === 0) return { key: `${base}.leadDays`, params: { count: minutes / 1440 } }
  if (minutes % 60 === 0) return { key: `${base}.leadHours`, params: { count: minutes / 60 } }

  return { key: `${base}.leadMinutes`, params: { count: minutes } }
}
