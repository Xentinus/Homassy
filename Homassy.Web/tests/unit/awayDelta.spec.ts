import { describe, expect, it } from 'vitest'
import { AWAY_GAP_MS, decideAwayGap } from '~/utils/awayGap'

const NOW = 1_757_500_000_000

describe('decideAwayGap', () => {
  it('does not fetch for a gap under the threshold', () => {
    const decision = decideAwayGap(NOW - (AWAY_GAP_MS - 1), NOW, null)

    // No request at all for a quick app-switch: the common case has to be free, not just quiet.
    expect(decision).toEqual({ shouldFetch: false, since: null })
  })

  it('fetches from the local last-seen for a gap over the threshold', () => {
    const lastSeen = NOW - AWAY_GAP_MS * 3
    const decision = decideAwayGap(lastSeen, NOW, null)

    expect(decision).toEqual({ shouldFetch: true, since: lastSeen })
  })

  it('fetches exactly at the threshold', () => {
    const lastSeen = NOW - AWAY_GAP_MS
    expect(decideAwayGap(lastSeen, NOW, null)).toEqual({ shouldFetch: true, since: lastSeen })
  })

  it('uses the server last-seen when this device has none — the new-device case', () => {
    const serverLastSeen = NOW - AWAY_GAP_MS * 10
    const decision = decideAwayGap(null, NOW, serverLastSeen)

    expect(decision).toEqual({ shouldFetch: true, since: serverLastSeen })
  })

  it('prefers the local last-seen over the server one when both exist', () => {
    const localLastSeen = NOW - AWAY_GAP_MS * 2
    const serverLastSeen = NOW - AWAY_GAP_MS * 20

    expect(decideAwayGap(localLastSeen, NOW, serverLastSeen)).toEqual({
      shouldFetch: true,
      since: localLastSeen
    })
  })

  it('does not fetch with neither a local nor a server last-seen', () => {
    // A first-ever launch has nothing to catch up on.
    expect(decideAwayGap(null, NOW, null)).toEqual({ shouldFetch: false, since: null })
  })

  it('ignores a local last-seen in the future rather than asking about a negative window', () => {
    expect(decideAwayGap(NOW + AWAY_GAP_MS, NOW, null)).toEqual({ shouldFetch: false, since: null })
  })

  it('falls back to the server value when the local one is skewed into the future', () => {
    const serverLastSeen = NOW - AWAY_GAP_MS * 5

    expect(decideAwayGap(NOW + AWAY_GAP_MS, NOW, serverLastSeen)).toEqual({
      shouldFetch: true,
      since: serverLastSeen
    })
  })

  it('ignores a server last-seen in the future too', () => {
    expect(decideAwayGap(null, NOW, NOW + AWAY_GAP_MS)).toEqual({ shouldFetch: false, since: null })
  })
})
