import { describe, expect, it } from 'vitest'
import { linkLabel, linkifySegments } from '~/utils/linkify'

describe('linkifySegments', () => {
  it('returns a single text segment for a message with no link', () => {
    expect(linkifySegments('milk and bread please')).toEqual([
      { type: 'text', value: 'milk and bread please' }
    ])
  })

  it('splits text around a link, keeping the surrounding text intact', () => {
    const segments = linkifySegments('look at https://example.com/thing now')

    expect(segments[0]).toEqual({ type: 'text', value: 'look at ' })
    expect(segments[1]).toMatchObject({ type: 'link', href: 'https://example.com/thing' })
    expect(segments[2]).toEqual({ type: 'text', value: ' now' })
  })

  it('reassembles to the original message', () => {
    // The safety property: linkifying never rewrites the text, it only marks parts of it.
    const text = 'a https://example.com/x b www.example.org c'
    const rebuilt = linkifySegments(text)
      .map(s => (s.type === 'text' ? s.value : s.href.replace(/^https:\/\/(?=www\.)/, '')))
      .join('')

    expect(rebuilt).toBe(text)
  })

  it('gives a bare www link an https scheme', () => {
    const segments = linkifySegments('www.example.com')
    expect(segments[0]).toMatchObject({ type: 'link', href: 'https://www.example.com' })
  })

  it('leaves sentence punctuation out of the link', () => {
    const segments = linkifySegments('see https://example.com/a.')

    expect(segments[0]).toEqual({ type: 'text', value: 'see ' })
    expect(segments[1]).toMatchObject({ href: 'https://example.com/a' })
    expect(segments[2]).toEqual({ type: 'text', value: '.' })
  })

  it('keeps a closing bracket that belongs to the URL', () => {
    const segments = linkifySegments('https://example.com/wiki/Thing_(disambiguation)')
    expect(segments[0]).toMatchObject({ href: 'https://example.com/wiki/Thing_(disambiguation)' })
  })

  it('drops an unbalanced closing bracket from around the URL', () => {
    const segments = linkifySegments('(https://example.com/a)')

    expect(segments[1]).toMatchObject({ href: 'https://example.com/a' })
    expect(segments[2]).toEqual({ type: 'text', value: ')' })
  })

  it('finds several links in one message', () => {
    const links = linkifySegments('https://a.example https://b.example').filter(s => s.type === 'link')
    expect(links).toHaveLength(2)
  })
})

describe('linkLabel', () => {
  it('shows the host alone for a bare domain', () => {
    expect(linkLabel('https://example.com')).toEqual({ label: 'example.com', host: 'example.com' })
  })

  it('keeps a short path', () => {
    expect(linkLabel('https://example.com/milk').label).toBe('example.com/milk')
  })

  it('elides a long path but never the host', () => {
    const { label, host } = linkLabel('https://example.com/a-very-long-path-that-goes-on-and-on/and-on')

    expect(host).toBe('example.com')
    expect(label.startsWith('example.com/')).toBe(true)
    expect(label.endsWith('…')).toBe(true)
  })

  it('shows the real host of a link whose text tries to look like another site', () => {
    // The spoof case: the label is derived from the href, never from what the message claimed.
    const { host } = linkLabel('https://evil.example/login?next=https://bank.example')
    expect(host).toBe('evil.example')
  })
})
