import type { MaybeRefOrGetter } from 'vue'

/**
 * Composable for formatting dates based on the current locale
 * 
 * Hungarian: yyyy.MM.dd
 * English: dd/MM/yyyy (British format)
 * German: dd.MM.yyyy (German format)
 */
export const useDateFormat = () => {
  const { locale } = useI18n()

  const formatDate = (dateString: string | undefined): string => {
    if (!dateString) return ''

    try {
      const date = new Date(dateString)
      
      // Check if date is valid
      if (isNaN(date.getTime())) {
        return dateString
      }

      const year = date.getFullYear()
      const month = String(date.getMonth() + 1).padStart(2, '0')
      const day = String(date.getDate()).padStart(2, '0')

      switch (locale.value) {
        case 'hu':
          // Hungarian format: yyyy.MM.dd
          return `${year}.${month}.${day}`
        
        case 'de':
          // German format: dd.MM.yyyy
          return `${day}.${month}.${year}`
        
        case 'en':
        default:
          // British format: dd/MM/yyyy
          return `${day}/${month}/${year}`
      }
    } catch (error) {
      console.error('Date formatting error:', error)
      return dateString
    }
  }

  const formatDateTime = (dateString: string | undefined): string => {
    if (!dateString) return ''

    try {
      const date = new Date(dateString)
      
      // Check if date is valid
      if (isNaN(date.getTime())) {
        return dateString
      }

      const datePart = formatDate(dateString)
      const hours = String(date.getHours()).padStart(2, '0')
      const minutes = String(date.getMinutes()).padStart(2, '0')

      return `${datePart} ${hours}:${minutes}`
    } catch (error) {
      console.error('DateTime formatting error:', error)
      return dateString
    }
  }

  return {
    formatDate,
    formatDateTime
  }
}

/**
 * BCP-47 tag for `Intl`, per app locale. The app's own date formats are British
 * / Hungarian / German, so the relative formatter follows the same regions.
 */
const INTL_LOCALES: Record<string, string> = {
  en: 'en-GB',
  hu: 'hu-HU',
  de: 'de-DE'
}

const MINUTE_MS = 60_000
const HOUR_MS = 60 * MINUTE_MS
const DAY_MS = 24 * HOUR_MS
/** Past this age a relative phrase stops being useful — show the real date instead. */
const ABSOLUTE_AFTER_MS = 7 * DAY_MS
/** Anything fresher than this reads as "now" rather than "0 minutes ago". */
const JUST_NOW_MS = 45_000

// ---------------------------------------------------------------------------
// Shared ticker
//
// One timer for the entire app, never one per component: a screen with fifty
// timestamps would otherwise wake the main thread fifty times a minute. Every
// `useRelativeTime` reads the same `sharedNow` and registers the timestamp it
// cares about, so the cadence can follow the *youngest* value on screen and stop
// altogether once nothing on screen can still change.
// ---------------------------------------------------------------------------

const sharedNow = ref(Date.now())
const trackedTimestamps = new Map<symbol, () => number | null>()
let tickTimer: ReturnType<typeof setTimeout> | null = null
let visibilityBound = false

/** Coarse cadence: half-minute while minutes still tick over, then a minute, then never. */
const nextTickDelay = (): number | null => {
  let youngest = Number.POSITIVE_INFINITY
  const now = Date.now()
  for (const read of trackedTimestamps.values()) {
    const timestamp = read()
    if (timestamp === null) continue
    youngest = Math.min(youngest, Math.abs(now - timestamp))
  }
  if (!Number.isFinite(youngest)) return null
  if (youngest < HOUR_MS) return 30_000
  if (youngest < ABSOLUTE_AFTER_MS) return MINUTE_MS
  return null
}

const scheduleTick = () => {
  if (!import.meta.client) return
  if (tickTimer) {
    clearTimeout(tickTimer)
    tickTimer = null
  }
  // A hidden document gets no timer at all; `onVisible` catches it back up.
  if (document.hidden) return

  const delay = nextTickDelay()
  if (delay === null) return

  tickTimer = setTimeout(() => {
    tickTimer = null
    sharedNow.value = Date.now()
    scheduleTick()
  }, delay)
}

/** Returning to the app must never show a value frozen at the moment it was backgrounded. */
const onVisible = () => {
  if (document.hidden) {
    if (tickTimer) {
      clearTimeout(tickTimer)
      tickTimer = null
    }
    return
  }
  sharedNow.value = Date.now()
  scheduleTick()
}

const bindVisibility = () => {
  if (visibilityBound || !import.meta.client) return
  visibilityBound = true
  document.addEventListener('visibilitychange', onVisible)
  // bfcache restores don't fire visibilitychange on every browser.
  window.addEventListener('pageshow', onVisible)
}

const toTimestamp = (value: string | Date | null | undefined): number | null => {
  if (!value) return null
  const date = value instanceof Date ? value : new Date(value)
  const ms = date.getTime()
  return Number.isNaN(ms) ? null : ms
}

/**
 * A relative timestamp that keeps itself current — "2 minutes ago" becomes
 * "3 minutes ago" without the component re-rendering for any other reason.
 *
 * Built on `Intl.RelativeTimeFormat`, so all three locales get grammatical
 * phrasing for free (including "yesterday" / "tegnap" / "gestern" via
 * `numeric: 'auto'`), and it handles future dates as naturally as past ones.
 *
 * Past `ABSOLUTE_AFTER_MS` the relative phrase stops carrying information and
 * `text` switches to the absolute date. `absolute` is always available for a
 * `title` tooltip.
 */
export const useRelativeTime = (
  date: MaybeRefOrGetter<string | Date | null | undefined>,
  options: { thresholdMs?: MaybeRefOrGetter<number | undefined> } = {}
) => {
  const { locale } = useI18n()
  const { formatDate, formatDateTime } = useDateFormat()
  const threshold = () => toValue(options.thresholdMs) ?? ABSOLUTE_AFTER_MS

  const timestamp = computed(() => toTimestamp(toValue(date)))

  if (import.meta.client) {
    const key = Symbol('relative-time')
    bindVisibility()
    trackedTimestamps.set(key, () => timestamp.value)
    // A changed (or newly arrived) timestamp can make the cadence too slow.
    watch(timestamp, scheduleTick, { immediate: true })
    onScopeDispose(() => {
      trackedTimestamps.delete(key)
      scheduleTick()
    })
  }

  const formatter = computed(() =>
    new Intl.RelativeTimeFormat(INTL_LOCALES[locale.value] ?? 'en-GB', { numeric: 'auto' })
  )

  /** Signed age in ms: negative in the past, positive in the future. */
  const delta = computed(() => (timestamp.value === null ? null : timestamp.value - sharedNow.value))

  const isAbsolute = computed(() => delta.value !== null && Math.abs(delta.value) >= threshold())

  const relative = computed(() => {
    const diff = delta.value
    if (diff === null) return ''
    const magnitude = Math.abs(diff)
    if (magnitude < JUST_NOW_MS) return formatter.value.format(0, 'second')
    if (magnitude < HOUR_MS) return formatter.value.format(Math.round(diff / MINUTE_MS), 'minute')
    if (magnitude < DAY_MS) return formatter.value.format(Math.round(diff / HOUR_MS), 'hour')
    return formatter.value.format(Math.round(diff / DAY_MS), 'day')
  })

  const absolute = computed(() => {
    if (timestamp.value === null) return ''
    return formatDateTime(new Date(timestamp.value).toISOString())
  })

  const absoluteDate = computed(() => {
    if (timestamp.value === null) return ''
    return formatDate(new Date(timestamp.value).toISOString())
  })

  const absoluteTime = computed(() => {
    if (timestamp.value === null) return ''
    return new Date(timestamp.value).toLocaleTimeString(INTL_LOCALES[locale.value] ?? 'en-GB', {
      hour: '2-digit',
      minute: '2-digit'
    })
  })

  /** What to render: relative while that means something, the plain date after that. */
  const text = computed(() => (isAbsolute.value ? absoluteDate.value : relative.value))

  return { text, relative, absolute, absoluteDate, absoluteTime, isAbsolute }
}
