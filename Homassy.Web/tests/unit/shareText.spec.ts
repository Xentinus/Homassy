import { describe, expect, it } from 'vitest'
import { SHARED_NAME_MAX_LENGTH, safeReturnTo, shareNameFrom } from '~/utils/shareText'

describe('shareNameFrom', () => {
  it('prefers the title the sharing app supplied', () => {
    expect(shareNameFrom({ title: 'Oat milk', text: 'buy this on the way home' })).toBe('Oat milk')
  })

  it('falls back to the first line of the shared text', () => {
    // Apps share paragraphs; a name field wants a name, so only the first line is used.
    expect(shareNameFrom({ text: 'Oat milk\nthe barista one\n1 litre' })).toBe('Oat milk')
  })

  it('trims surrounding whitespace from either source', () => {
    expect(shareNameFrom({ title: '  Oat milk \n' })).toBe('Oat milk')
    expect(shareNameFrom({ text: '  Oat milk  \nrest' })).toBe('Oat milk')
  })

  it('skips leading blank lines rather than reading them as the name', () => {
    // Plenty of apps prefix the shared text with a newline; taking split('\n')[0]
    // verbatim turned the whole share into "nothing to suggest".
    expect(shareNameFrom({ text: '\n\n  Oat milk  \nrest' })).toBe('Oat milk')
  })

  it('treats a blank title as absent rather than as a name', () => {
    expect(shareNameFrom({ title: '   ', text: 'Oat milk' })).toBe('Oat milk')
  })

  it('caps at the length both target fields accept', () => {
    const long = 'x'.repeat(SHARED_NAME_MAX_LENGTH + 50)

    // A field seeded past the server's own limit would fail validation on sight.
    expect(shareNameFrom({ title: long })).toHaveLength(SHARED_NAME_MAX_LENGTH)
  })

  it('has nothing to offer for an image-only share', () => {
    expect(shareNameFrom({})).toBeNull()
    expect(shareNameFrom({ title: null, text: null })).toBeNull()
    expect(shareNameFrom({ text: '   \n  ' })).toBeNull()
  })
})

describe('safeReturnTo', () => {
  it('passes an in-app path through', () => {
    expect(safeReturnTo('/share')).toBe('/share')
    expect(safeReturnTo('/products?action=scan')).toBe('/products?action=scan')
  })

  it('falls back when there is no destination', () => {
    expect(safeReturnTo(undefined)).toBe('/calendar')
    expect(safeReturnTo(null)).toBe('/calendar')
    expect(safeReturnTo('')).toBe('/calendar')
  })

  it('refuses a protocol-relative or absolute target', () => {
    // The shape worth refusing outright: `//host` is a URL, not a path.
    expect(safeReturnTo('//evil.example/products')).toBe('/calendar')
    expect(safeReturnTo('https://evil.example')).toBe('/calendar')
    expect(safeReturnTo('javascript:alert(1)')).toBe('/calendar')
  })

  it('refuses a relative target', () => {
    expect(safeReturnTo('products')).toBe('/calendar')
  })

  it('refuses a target back inside the auth flow', () => {
    // A login page that sends you to the login page is a loop.
    expect(safeReturnTo('/auth/login')).toBe('/calendar')
    expect(safeReturnTo('/auth/register')).toBe('/calendar')
  })

  it('takes the first value of a repeated query parameter', () => {
    expect(safeReturnTo(['/share', '//evil.example'])).toBe('/share')
    expect(safeReturnTo(['//evil.example', '/share'])).toBe('/calendar')
  })

  it('honours a caller-supplied fallback', () => {
    expect(safeReturnTo('//evil.example', '/products')).toBe('/products')
  })
})
