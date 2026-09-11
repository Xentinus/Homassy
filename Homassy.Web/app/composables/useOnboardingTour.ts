/**
 * Drives the first-run spotlight tour (#98): which step is showing, where its target
 * is, and when the tour is allowed to start at all.
 *
 * The step list and the geometry live in `~/utils/onboardingTour` (pure, unit-tested);
 * this composable owns the parts that need a browser — finding the target element,
 * navigating between the two pages the tour visits, and persisting completion.
 *
 * **Completion is a field on the user, not a localStorage key.** The tour explains the
 * app, not this browser, and being walked through the bottom nav again on every new
 * phone reads as a bug. `localStorage` is kept as a same-device echo so that a failed
 * write (offline, or a 500) does not mean the tour reopens on the next navigation; the
 * server value is what makes it stay away on a new device.
 *
 * All state is module-scoped: the overlay lives in the auth layout, the "replay" row
 * lives in the profile page, and both talk to the same tour.
 */
import { ref, computed } from 'vue'
import { useAuthStore } from '~/stores/auth'
import { useUserApi } from '~/composables/api/useUserApi'
import { TOUR_STEPS, nextVisibleStep, type TourStep } from '~/utils/onboardingTour'

/** Same-device echo of the server flag. Never the only record — see the note above. */
const STORAGE_KEY = 'homassy_onboarding_done'

/**
 * How long to wait for a step's target to appear after a navigation before giving up
 * on it. Generous enough for the `out-in` page transition plus a first data load,
 * short enough that a genuinely missing target does not stall the tour.
 */
const TARGET_TIMEOUT_MS = 2500

/** How long to keep waiting for the boot splash to clear before starting anyway. */
const SPLASH_TIMEOUT_MS = 6000

const isActive = ref(false)
const stepIndex = ref(0)
/** The spotlit element's viewport rect, or null while it is being resolved. */
const targetRect = ref<DOMRect | null>(null)
/** Guards auto-start so it is attempted once per app load, not once per layout mount. */
let autoStartAttempted = false

function selectorFor(target: string) {
  return `[data-tour="${CSS.escape(target)}"]`
}

function findTarget(target: string): HTMLElement | null {
  if (!import.meta.client) return null
  return document.querySelector<HTMLElement>(selectorFor(target))
}

/**
 * Resolve a target that may not be in the DOM yet (a navigation is landing, a page is
 * still loading). Polls on animation frames rather than a `MutationObserver`: the
 * element also has to be *laid out* before its rect is usable, and a frame boundary is
 * exactly when that is true.
 */
function waitForTarget(target: string, timeoutMs = TARGET_TIMEOUT_MS): Promise<HTMLElement | null> {
  return new Promise((resolve) => {
    const deadline = performance.now() + timeoutMs
    let settled = false

    const settle = (el: HTMLElement | null) => {
      if (settled) return
      settled = true
      window.clearTimeout(timer)
      resolve(el)
    }

    const poll = () => {
      if (settled) return
      const el = findTarget(target)
      // Width, not mere presence: an element that is in the DOM but not yet laid out
      // has a zero rect, and a zero rect is a hole nobody can see.
      if (el && el.getBoundingClientRect().width > 0) {
        settle(el)
        return
      }
      if (performance.now() >= deadline) {
        settle(null)
        return
      }
      requestAnimationFrame(poll)
    }

    // A timeout alongside the frame loop, because requestAnimationFrame does not fire
    // in a hidden tab — without it the promise would never settle there and the tour
    // would hang on whichever step the user backgrounded the app on.
    const timer = window.setTimeout(() => settle(findTarget(target)), timeoutMs + 50)

    requestAnimationFrame(poll)
  })
}

export const useOnboardingTour = () => {
  const authStore = useAuthStore()
  const { updateOnboarding } = useUserApi()

  const steps = TOUR_STEPS
  const currentStep = computed<TourStep | null>(() => steps[stepIndex.value] ?? null)
  const stepNumber = computed(() => stepIndex.value + 1)
  const totalSteps = computed(() => steps.length)
  const isLastStep = computed(() => stepIndex.value >= steps.length - 1)

  /** True once this user has finished or skipped the tour, by either record. */
  const isCompleted = computed(() => {
    if (authStore.user?.onboardingCompletedAt) return true
    if (!import.meta.client) return false
    try {
      return localStorage.getItem(STORAGE_KEY) === 'true'
    } catch {
      // A browser with site data blocked. The server flag above is the real answer.
      return false
    }
  })

  function rememberLocally(done: boolean) {
    if (!import.meta.client) return
    try {
      if (done) localStorage.setItem(STORAGE_KEY, 'true')
      else localStorage.removeItem(STORAGE_KEY)
    } catch {
      // Nothing to do; the server flag is the record that matters.
    }
  }

  /**
   * Show the step at `index`, navigating and waiting for its target first. Steps whose
   * target never turns up are skipped; if none is left, the tour finishes.
   */
  async function showStep(index: number) {
    const resolved = nextVisibleStep(steps, index, target => !!findTarget(target))
    if (resolved === -1) {
      await complete()
      return
    }

    const step = steps[resolved]
    if (!step) {
      await complete()
      return
    }

    stepIndex.value = resolved

    if (step.route && useRoute().path !== step.route) {
      // Hide the cut-out while the page changes: a hole left over the old page's
      // layout would spotlight whatever slid into that spot.
      targetRect.value = null
      await navigateTo(step.route)
    }

    const el = await waitForTarget(step.target)
    if (!el) {
      // Not there after all (no camera, an empty page that renders no such control).
      // Move past it rather than dimming the screen around nothing.
      await showStep(resolved + 1)
      return
    }

    targetRect.value = el.getBoundingClientRect()
  }

  /** Re-read the current step's rect. Called on resize/scroll while the tour is up. */
  function remeasure() {
    const step = currentStep.value
    if (!isActive.value || !step) return
    const el = findTarget(step.target)
    targetRect.value = el ? el.getBoundingClientRect() : null
  }

  /** Start (or restart) the tour from the first showable step. */
  async function start() {
    if (!import.meta.client) return
    isActive.value = true
    stepIndex.value = 0
    targetRect.value = null
    await showStep(0)
  }

  async function next() {
    if (!isActive.value) return
    if (isLastStep.value) {
      await complete()
      return
    }
    await showStep(stepIndex.value + 1)
  }

  /**
   * End the tour and record it. Skipping and finishing are the same outcome on
   * purpose: both mean "do not show me this again", and a user who skipped has made
   * that call just as deliberately as one who read every step.
   */
  async function complete() {
    isActive.value = false
    targetRect.value = null
    rememberLocally(true)

    // Patch the store first so `isCompleted` is true immediately — otherwise a
    // navigation before the request lands could re-trigger the auto-start check.
    authStore.applyUserPatch({ onboardingCompletedAt: new Date().toISOString() })
    await updateOnboarding(true)
  }

  /** Arm the tour again, from the profile's "Replay the tour" row. */
  async function replay() {
    rememberLocally(false)
    authStore.applyUserPatch({ onboardingCompletedAt: null })
    await updateOnboarding(false)
    await start()
  }

  /**
   * Start the tour if this is the user's first run. Called by the auth layout once per
   * app load.
   *
   * Waits for the boot splash to clear: the splash is a `position: fixed` overlay of
   * its own, and a spotlight underneath it would spend its first seconds invisible and
   * then appear mid-step.
   */
  async function maybeAutoStart() {
    if (!import.meta.client || autoStartAttempted) return
    autoStartAttempted = true

    // No user yet means the session is still resolving; the layout calls again once it
    // has one, and `autoStartAttempted` is only set on a run that could decide.
    if (!authStore.user) {
      autoStartAttempted = false
      return
    }
    if (isCompleted.value) return

    await waitForSplash()
    if (isCompleted.value) return
    await start()
  }

  return {
    isActive: computed(() => isActive.value),
    targetRect: computed(() => targetRect.value),
    currentStep,
    stepNumber,
    totalSteps,
    isLastStep,
    isCompleted,
    start,
    next,
    skip: complete,
    replay,
    remeasure,
    maybeAutoStart
  }
}

/**
 * Resolves once `SplashScreen.vue`'s dismissal attribute is on `<html>`, or after
 * `SPLASH_TIMEOUT_MS`. Non-standalone launches never show the splash and never stamp
 * the attribute, so the timeout is the normal path there — hence a plain poll rather
 * than waiting on an event that may never come.
 */
function waitForSplash(): Promise<void> {
  return new Promise((resolve) => {
    const root = document.documentElement
    if (root.hasAttribute('data-splash-ready')) {
      resolve()
      return
    }

    const observer = new MutationObserver(() => {
      if (root.hasAttribute('data-splash-ready')) {
        observer.disconnect()
        window.clearTimeout(timer)
        resolve()
      }
    })
    observer.observe(root, { attributes: true, attributeFilter: ['data-splash-ready'] })

    const timer = window.setTimeout(() => {
      observer.disconnect()
      resolve()
    }, SPLASH_TIMEOUT_MS)
  })
}
