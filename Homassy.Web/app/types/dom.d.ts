/**
 * Browser APIs the app relies on that lib.dom does not describe.
 *
 * Two kinds live here: a vendor-prefixed constructor that is still the only one
 * Safari offers, and camera controls that every mobile browser implements but
 * the MediaStream spec leaves out of its own dictionaries. Declaring them is
 * what lets the call sites feature-check instead of casting to `any`.
 */

interface Window {
  /** Safari has never unprefixed this. */
  webkitAudioContext?: typeof AudioContext
}

/**
 * `torch` and `zoom` are real on Android/Chrome camera tracks but are not in the
 * Media Capture spec's `MediaTrackCapabilities`, so they stay optional and every
 * read has to check.
 */
interface MediaTrackCapabilities {
  torch?: boolean
  zoom?: { min: number, max: number, step?: number }
}

/** The matching constraint side, for `applyConstraints({ advanced: [...] })`. */
interface MediaTrackConstraintSet {
  torch?: ConstrainBoolean
  zoom?: ConstrainDouble
}
