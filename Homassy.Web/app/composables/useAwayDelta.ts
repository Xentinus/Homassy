/**
 * "What changed while you were away" (#127): decides whether the user has actually been away,
 * fetches the summary if so, and remembers when they last saw the household.
 *
 * All the decision logic lives in `~/utils/awayGap` as a plain function (`decideAwayGap`) so it can
 * be tested without a Nuxt runtime; this composable is the side-effecting shell around it -
 * `localStorage`, the request, and when the stored stamp moves.
 */
import { computed, onMounted, ref } from 'vue'
import { decideAwayGap } from '~/utils/awayGap'
import type { AwayDeltaResponse } from '~/types/insights'

/** One entry per user, so two people sharing a device do not inherit each other's last visit. */
const storageKeyFor = (userPublicId: string): string => `homassy_last_seen_${userPublicId}`

/**
 * Every `localStorage` access is wrapped: a private window throws on access outright, and a
 * browser with site data blocked throws on write. A device that cannot remember its last visit
 * simply never shows the card - which is the same as a first launch, and strictly better than a
 * boot that fails.
 */
const readLastSeen = (userPublicId: string): number | null => {
  try {
    const raw = window.localStorage.getItem(storageKeyFor(userPublicId))
    if (!raw) return null
    const parsed = Number(raw)
    return Number.isFinite(parsed) ? parsed : null
  } catch {
    return null
  }
}

const writeLastSeen = (userPublicId: string, valueMs: number): void => {
  try {
    window.localStorage.setItem(storageKeyFor(userPublicId), String(valueMs))
  } catch {
    // Nothing to do and nothing to report: the next visit behaves like a first one.
  }
}

export const useAwayDelta = () => {
  const authStore = useAuthStore()
  const { getAwayDelta } = useInsightsApi()

  const delta = ref<AwayDeltaResponse | null>(null)

  /**
   * The card renders only when something actually happened. An empty delta is a legitimate answer
   * (the API returns 200 with zeroes rather than 204), and "0 changes since your last visit" is
   * not worth a card.
   */
  const hasDelta = computed(() => (delta.value?.total ?? 0) > 0)

  /**
   * Called once the summary has actually been shown - which is when it becomes safe to move the
   * stored last-seen forward.
   *
   * Deliberately not done at fetch time: a reload (or a tab closed) between the response landing
   * and the card being read would otherwise swallow the delta entirely, and the user would never
   * learn what changed. Moving the stamp only after the card exists means the worst case is seeing
   * the same summary twice, which is far better than never seeing it.
   *
   * It does <b>not</b> clear `delta`: the card emits this the moment it mounts, and clearing here
   * would unmount the very card that was reporting itself as shown. The card's lifetime is its
   * host screen's - navigating away is what removes it.
   */
  const acknowledge = (): void => {
    const userPublicId = authStore.user?.publicId
    if (!userPublicId) return

    writeLastSeen(userPublicId, Date.now())
  }

  /**
   * Resolves the window and, if the user has genuinely been away, fetches the summary.
   *
   * <b>Never awaited on the boot path.</b> `onMounted` below fires this without awaiting it, and it
   * is scheduled on a timeout rather than run inline, so the splash's own dismissal (owned by
   * `plugins/auth.ts` and the first-screen page - see `useSplashScreen`) cannot be delayed by this
   * request even on a slow network. This is the requirement most likely to be undone by a later
   * refactor: if this ever becomes `await load()` somewhere on the boot path, the splash starts
   * waiting for an away summary nobody asked to wait for.
   */
  const load = async (): Promise<void> => {
    const user = authStore.user
    const userPublicId = user?.publicId
    if (!userPublicId) return

    const now = Date.now()
    const serverLastSeen = user?.lastSeenAt ? Date.parse(user.lastSeenAt) : null
    const decision = decideAwayGap(
      readLastSeen(userPublicId),
      now,
      Number.isFinite(serverLastSeen as number) ? serverLastSeen : null
    )

    if (!decision.shouldFetch || decision.since === null) {
      // Not away: no request at all. The stamp still moves, because they did see the household
      // just now - without this, a user bouncing in and out every few minutes would eventually
      // accumulate a "gap" covering time they were present for.
      writeLastSeen(userPublicId, now)
      return
    }

    try {
      const response = await getAwayDelta(new Date(decision.since).toISOString())
      if (!response.success || !response.data) return

      if (response.data.total > 0) {
        delta.value = response.data
        return
      }

      // Nothing happened while they were away: no card, and the stamp moves now rather than
      // waiting for an acknowledgement that will never come.
      writeLastSeen(userPublicId, now)
    } catch (error) {
      // A missing summary is a missing nicety. Leave the stamp alone so the next visit can still
      // report the same window rather than silently losing it.
      console.error('Failed to load the away delta:', error)
    }
  }

  onMounted(() => {
    // Deferred, not awaited - see load()'s own comment. A macrotask is enough: it puts the request
    // after the current render and after the splash's own dismissal path has been kicked off.
    window.setTimeout(() => { void load() }, 0)
  })

  return { delta, hasDelta, acknowledge, load }
}
