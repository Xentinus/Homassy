import { describe, expect, it } from 'vitest'
import { deriveRealtimeStatus } from '~/utils/realtimeStatus'

describe('deriveRealtimeStatus', () => {
  it('is idle when no hub on this page has ever connected', () => {
    expect(deriveRealtimeStatus(['idle', 'idle'], true)).toBe('idle')
  })

  it('is connected when every live hub is connected', () => {
    expect(deriveRealtimeStatus(['connected', 'idle'], true)).toBe('connected')
    expect(deriveRealtimeStatus(['connected', 'connected'], true)).toBe('connected')
  })

  it('reports the worst live hub, so two hubs never contradict each other', () => {
    expect(deriveRealtimeStatus(['connected', 'reconnecting'], true)).toBe('reconnecting')
    expect(deriveRealtimeStatus(['reconnecting', 'closed'], true)).toBe('offline')
    expect(deriveRealtimeStatus(['connected', 'closed'], true)).toBe('offline')
  })

  it('says device-offline rather than socket-offline when the device has no network', () => {
    // The wording and the user's expectation differ: "we are retrying" is a lie when the
    // problem is that their phone left the tunnel.
    expect(deriveRealtimeStatus(['closed'], false)).toBe('device-offline')
    expect(deriveRealtimeStatus(['reconnecting'], false)).toBe('device-offline')
  })

  it('still trusts a connected socket over a lying navigator.onLine', () => {
    // navigator.onLine is false-positive prone; a live socket is proof of connectivity.
    expect(deriveRealtimeStatus(['connected'], false)).toBe('connected')
  })

  it('is idle for an empty hub list', () => {
    expect(deriveRealtimeStatus([], true)).toBe('idle')
  })
})
