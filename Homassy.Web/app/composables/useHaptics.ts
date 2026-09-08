import { computed, ref } from 'vue'

/**
 * The app's haptic vocabulary. Every call site picks a *meaning*, never a raw
 * duration, so the whole app can be retuned from the `PATTERNS` table below.
 *
 * - `tap`     — a light acknowledgement (pull-to-refresh release, a plain toggle)
 * - `select`  — crossing a gesture threshold / landing on a snap point
 * - `impact`  — a gesture actually committing (swipe action fires)
 * - `success` — an operation completed (purchase confirmed, barcode decoded)
 * - `warning` — a destructive action about to happen or just done
 * - `error`   — an operation failed (scan gave nothing back)
 */
export type HapticPattern = 'tap' | 'select' | 'impact' | 'success' | 'warning' | 'error'

const PATTERNS: Record<HapticPattern, number | number[]> = {
  tap: 8,
  select: 12,
  impact: 22,
  success: [14, 60, 28],
  warning: [24, 70, 24],
  error: [30, 55, 30, 55, 60]
}

const STORAGE_KEY = 'homassy_haptics'

// Module-scoped so the preference is shared by every caller (and by the switch
// on the profile page) without a store.
const enabledState = ref(true)
const supported = ref(false)
let hydrated = false

/**
 * Read the persisted preference once, on the client. With nothing stored yet,
 * `prefers-reduced-motion: reduce` is taken as a hint that the user does not
 * want incidental physical feedback either, so haptics start off.
 */
const hydrate = () => {
  if (hydrated || !import.meta.client) return
  hydrated = true

  supported.value = typeof navigator.vibrate === 'function'

  let stored: string | null = null
  try {
    stored = localStorage.getItem(STORAGE_KEY)
  } catch {
    // Private mode / blocked storage — fall through to the default.
  }

  if (stored === 'on' || stored === 'off') {
    enabledState.value = stored === 'on'
    return
  }

  enabledState.value = !window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

const persist = (value: boolean) => {
  try {
    localStorage.setItem(STORAGE_KEY, value ? 'on' : 'off')
  } catch {
    // Preference stays in memory for this session only.
  }
}

/**
 * Single entry point for vibration feedback. Feature-detects `navigator.vibrate`
 * (absent on iOS Safari, where every call is a silent no-op) and honours the
 * user's haptics switch.
 *
 * Safe to call from anywhere — plain functions included — since all of its state
 * is module-scoped rather than injected.
 */
export const useHaptics = () => {
  hydrate()

  const fire = (pattern: HapticPattern) => {
    if (!supported.value || !enabledState.value) return
    try {
      navigator.vibrate(PATTERNS[pattern])
    } catch {
      // Some browsers throw when vibration is blocked by a permission policy.
    }
  }

  const enabled = computed({
    get: () => enabledState.value,
    set: (value: boolean) => {
      enabledState.value = value
      persist(value)
    }
  })

  return {
    /** False where the browser has no Vibration API (iOS Safari) — hide the setting there. */
    isSupported: computed(() => supported.value),
    /** Writable: bind straight to a switch, it persists itself. */
    enabled,
    haptic: fire,
    tap: () => fire('tap'),
    select: () => fire('select'),
    impact: () => fire('impact'),
    success: () => fire('success'),
    warning: () => fire('warning'),
    error: () => fire('error')
  }
}
