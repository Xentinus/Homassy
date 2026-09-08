import { describe, expect, it } from 'vitest'
import {
  MEMBER_COLORS,
  isMemberColorKey,
  pickMemberColor,
  resolveMemberColor
} from '~/utils/memberColors'

const ID_A = '3f2504e0-4f89-11d3-9a0c-0305e82c3301'
const ID_B = 'b1f0c6a2-8d55-4c1e-9a2b-7e4d1f6c8a90'

describe('pickMemberColor', () => {
  it('is deterministic for the same public id', () => {
    expect(pickMemberColor(ID_A).key).toBe(pickMemberColor(ID_A).key)
  })

  it('always returns a colour from the curated palette', () => {
    const keys = MEMBER_COLORS.map(c => c.key)
    expect(keys).toContain(pickMemberColor(ID_A).key)
    expect(keys).toContain(pickMemberColor('').key)
    expect(keys).toContain(pickMemberColor('not-a-guid').key)
  })

  it('is case- and dash-insensitive, so the same user never changes colour on formatting', () => {
    expect(pickMemberColor(ID_A.toUpperCase()).key).toBe(pickMemberColor(ID_A).key)
    expect(pickMemberColor(ID_A.replace(/-/g, '')).key).toBe(pickMemberColor(ID_A).key)
  })

  it('spreads a realistic family across distinct colours', () => {
    const ids = [ID_A, ID_B, '00000000-0000-0000-0000-000000000001', '7c9e6679-7425-40de-944b-e07fc1f90ae7']
    const keys = new Set(ids.map(id => pickMemberColor(id).key))
    expect(keys.size).toBe(ids.length)
  })
})

describe('resolveMemberColor', () => {
  it('honours a valid override', () => {
    expect(resolveMemberColor(ID_A, 'teal').key).toBe('teal')
  })

  it('falls back to the deterministic pick for a null, empty or unknown override', () => {
    const fallback = pickMemberColor(ID_A).key
    expect(resolveMemberColor(ID_A, null).key).toBe(fallback)
    expect(resolveMemberColor(ID_A, '').key).toBe(fallback)
    expect(resolveMemberColor(ID_A, 'chartreuse').key).toBe(fallback)
    expect(resolveMemberColor(ID_A, '#ff0000').key).toBe(fallback)
  })
})

describe('isMemberColorKey', () => {
  it('accepts palette keys and rejects everything else', () => {
    expect(isMemberColorKey('rose')).toBe(true)
    expect(isMemberColorKey('ROSE')).toBe(false)
    expect(isMemberColorKey('#abcdef')).toBe(false)
    expect(isMemberColorKey(null)).toBe(false)
  })
})
