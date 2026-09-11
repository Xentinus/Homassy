/**
 * Screen Wake Lock wrapper — keeps the display awake while a screen has to stay readable without
 * being touched (#131: shopping mode, where the phone is in one hand and the trolley in the other).
 *
 * Three things make this more than a one-line call:
 *
 * 1. **The lock is dropped by the browser, not just by us.** Backgrounding the tab, locking the
 *    phone, or switching apps releases the sentinel. Nothing tells the page to ask again, so this
 *    re-requests on `visibilitychange` for as long as the caller still wants the lock.
 * 2. **It is not everywhere.** Firefox and older iOS have no `wakeLock` at all, and a request can
 *    be rejected outright (a low battery, a policy). Both are ordinary outcomes here, not errors —
 *    the caller shows its screen either way, `isActive` just stays false.
 * 3. **It must be released.** A lock left held keeps the screen on after the user has moved on,
 *    which is the worst possible bug for a feature whose whole point is the battery.
 */
export const useWakeLock = () => {
  /** False where the browser has no Screen Wake Lock API — the caller hides any "screen stays on" hint. */
  const isSupported = computed(() => import.meta.client && 'wakeLock' in navigator)

  /** True only while a lock is actually held, never merely because one was asked for. */
  const isActive = ref(false)

  let sentinel: WakeLockSentinel | null = null
  // What the caller wants, as opposed to what the browser is currently granting. The
  // visibilitychange handler re-requests only while this is true.
  let wanted = false

  const acquire = async () => {
    if (!isSupported.value || sentinel) return

    try {
      sentinel = await navigator.wakeLock.request('screen')
      isActive.value = true
      sentinel.addEventListener('release', () => {
        // Fires for a browser-initiated release as well as our own, so the state is corrected
        // either way; re-acquiring is left to the visibility handler, which knows the tab is back.
        sentinel = null
        isActive.value = false
      })
    } catch {
      // Denied (battery saver, permissions policy) or the document was not visible. Not an error
      // worth surfacing: the screen simply behaves the way it normally would.
      sentinel = null
      isActive.value = false
    }
  }

  const onVisibilityChange = () => {
    if (wanted && document.visibilityState === 'visible') void acquire()
  }

  /** Starts keeping the screen awake, and keeps re-acquiring after the tab is backgrounded. */
  const request = async () => {
    if (!import.meta.client) return
    wanted = true
    document.addEventListener('visibilitychange', onVisibilityChange)
    await acquire()
  }

  /** Releases the lock and stops re-acquiring it. Safe to call when nothing is held. */
  const release = async () => {
    wanted = false
    if (import.meta.client) document.removeEventListener('visibilitychange', onVisibilityChange)

    const held = sentinel
    sentinel = null
    isActive.value = false
    if (!held) return

    try {
      await held.release()
    } catch {
      // Already released by the browser — the state above is what matters.
    }
  }

  // A lock outliving the screen that asked for it is the one failure mode worth being careful
  // about, so releasing is tied to the component's lifetime as well as to the caller's own exit.
  onBeforeUnmount(() => { void release() })

  return { isSupported, isActive, request, release }
}
