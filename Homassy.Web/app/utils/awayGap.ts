/**
 * When a return to the app counts as "coming back" rather than a quick app-switch (#127), and
 * which moment the away delta should be measured from.
 *
 * Pure and dependency-free: the decision is the one part of this feature with rules worth pinning
 * down (a short gap must cost no request at all, a new device has no local last-seen, a skewed
 * clock must not produce a negative window), and none of them need a Nuxt runtime to state.
 */

/**
 * How long the app has to have been away before returning to it is treated as a fresh visit.
 *
 * This is the same threshold the boot splash re-arms itself on (see
 * `plugins/splash-resume.client.ts`, which imports this constant rather than keeping its own).
 * They have to be one value: the splash saying "welcome back" while the delta card says nothing
 * happened - or the reverse - is the same disagreement seen twice, and two copies of a number
 * called RESUME_SPLASH_MS and AWAY_GAP_MS would drift the first time either was tuned.
 *
 * Ten minutes was chosen for the splash because it is long enough that a glance at another app,
 * answering a message or taking a photo does not count as leaving, and short enough that coming
 * back after lunch does.
 */
export const AWAY_GAP_MS = 10 * 60 * 1000

export interface GapDecision {
  /** Whether to ask the server for a delta at all. */
  shouldFetch: boolean
  /** The moment to measure from (epoch ms), or null when there is nothing to ask about. */
  since: number | null
}

const NO_FETCH: GapDecision = { shouldFetch: false, since: null }

/**
 * Decides whether a returning visit deserves an away-delta request, and from when.
 *
 * @param lastSeenMs This device's own last-seen (from `localStorage`), or null if it has none.
 * @param nowMs The current time.
 * @param serverLastSeenMs The server's stored last-seen for this user, or null.
 *
 * The rules, in order:
 *
 * 1. **A local last-seen in the future is ignored.** A device whose clock moved backwards would
 *    otherwise produce a negative window - and asking the server about a period that has not
 *    happened yet is never right. Treated as "no local value" rather than as an error.
 * 2. **A gap under `AWAY_GAP_MS` costs nothing.** No request at all for a quick app-switch: the
 *    point of the threshold is that the common case is free, not merely quiet.
 * 3. **A long gap measures from the local last-seen** - this device knows exactly when it last
 *    showed the user the household, which is a better answer than the server's own stamp (which
 *    lags by up to a flush interval and counts every device).
 * 4. **No local value but a server one: the new-device case.** Measure from the server's stamp;
 *    it is the only thing that knows the user has been anywhere at all.
 * 5. **Neither: nothing to catch up on.** A first-ever launch has no "while you were away", and
 *    the honest answer is silence rather than the household's whole history.
 */
export const decideAwayGap = (
  lastSeenMs: number | null,
  nowMs: number,
  serverLastSeenMs: number | null
): GapDecision => {
  const localLastSeen = lastSeenMs !== null && lastSeenMs <= nowMs ? lastSeenMs : null

  if (localLastSeen !== null) {
    return nowMs - localLastSeen >= AWAY_GAP_MS
      ? { shouldFetch: true, since: localLastSeen }
      : NO_FETCH
  }

  // The new-device case. A server stamp in the future is ignored for the same reason a local one
  // is - it can only be skew, and it would ask about a window that has not happened.
  if (serverLastSeenMs !== null && serverLastSeenMs <= nowMs) {
    return { shouldFetch: true, since: serverLastSeenMs }
  }

  return NO_FETCH
}
