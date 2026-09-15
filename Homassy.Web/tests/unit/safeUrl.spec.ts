import { describe, expect, it } from 'vitest'
import { toSafeHttpUrl } from '~/utils/safeUrl'

describe('toSafeHttpUrl', () => {
  it('accepts http and https', () => {
    expect(toSafeHttpUrl('https://example.com/a?b=1')).toBe('https://example.com/a?b=1')
    expect(toSafeHttpUrl('http://example.com')).toBe('http://example.com')
  })

  it('trims surrounding whitespace', () => {
    expect(toSafeHttpUrl('  https://example.com  ')).toBe('https://example.com')
  })

  it('rejects every other scheme', () => {
    expect(toSafeHttpUrl('javascript:alert(1)')).toBeNull()
    expect(toSafeHttpUrl('data:text/html,<script>')).toBeNull()
    expect(toSafeHttpUrl('file:///etc/passwd')).toBeNull()
  })

  it('rejects what does not parse as a URL at all', () => {
    expect(toSafeHttpUrl('example.com')).toBeNull()
    expect(toSafeHttpUrl('not a url')).toBeNull()
  })

  it('returns null for empty and missing input', () => {
    expect(toSafeHttpUrl('')).toBeNull()
    expect(toSafeHttpUrl('   ')).toBeNull()
    expect(toSafeHttpUrl(null)).toBeNull()
    expect(toSafeHttpUrl(undefined)).toBeNull()
  })
})
