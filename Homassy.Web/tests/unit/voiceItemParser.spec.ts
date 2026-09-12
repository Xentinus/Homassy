import { describe, expect, it } from 'vitest'
import { parseVoiceItems } from '~/utils/voiceItemParser'
import { Unit } from '~/types/enums'

/** A dictated sentence is only ever one string — every case here is one call. */
const parse = (transcript: string, locale = 'en') => parseVoiceItems(transcript, locale)

describe('parseVoiceItems', () => {
  it('reads quantity, unit and name out of the leading form', () => {
    expect(parse('two litres of milk')).toEqual([
      { quantity: 2, unit: Unit.Liter, name: 'milk', raw: 'two litres of milk' }
    ])
  })

  it('reads digits as well as spoken numbers', () => {
    expect(parse('3 kg potatoes')[0]).toMatchObject({ quantity: 3, unit: Unit.Kilogram, name: 'potatoes' })
  })

  it('defaults to one of a thing when no number was spoken', () => {
    expect(parse('bread')[0]).toMatchObject({ quantity: 1, unit: undefined, name: 'bread' })
  })

  it('splits one utterance into several items', () => {
    const items = parse('milk, bread and six eggs')

    expect(items.map(item => item.name)).toEqual(['milk', 'bread', 'eggs'])
    expect(items[2]).toMatchObject({ quantity: 6 })
  })

  it('reads the trailing form people use when reading a written list aloud', () => {
    expect(parse('milk 2 litres')[0]).toMatchObject({ quantity: 2, unit: Unit.Liter, name: 'milk' })
    expect(parse('apples 5')[0]).toMatchObject({ quantity: 5, unit: undefined, name: 'apples' })
  })

  it('keeps a unit that is the whole name rather than eating it', () => {
    // "a box" is a thing you can buy. Only a unit *followed by a name* is read as a unit.
    expect(parse('box')[0]).toMatchObject({ quantity: 1, unit: undefined, name: 'box' })
    expect(parse('two boxes')[0]).toMatchObject({ quantity: 2, unit: undefined, name: 'boxes' })
  })

  it('drops the verb people start with', () => {
    expect(parse('add some tomatoes')[0]).toMatchObject({ name: 'tomatoes' })
  })

  it('keeps the name as spoken, accents and capitals intact', () => {
    expect(parse('2 Flaschen Apfelsaft', 'de')[0]).toMatchObject({
      quantity: 2,
      unit: Unit.Bottle,
      name: 'Apfelsaft'
    })
  })

  it('parses Hungarian, including its own conjunction', () => {
    const items = parse('két liter tej és hat tojás', 'hu')

    expect(items).toHaveLength(2)
    expect(items[0]).toMatchObject({ quantity: 2, unit: Unit.Liter, name: 'tej' })
    expect(items[1]).toMatchObject({ quantity: 6, unit: undefined, name: 'tojás' })
  })

  it('parses German, including its own conjunction', () => {
    const items = parse('zwei Liter Milch und drei Packungen Nudeln', 'de')

    expect(items).toHaveLength(2)
    expect(items[0]).toMatchObject({ quantity: 2, unit: Unit.Liter, name: 'Milch' })
    expect(items[1]).toMatchObject({ quantity: 3, unit: Unit.Pack, name: 'Nudeln' })
  })

  it('accepts a decimal with either separator', () => {
    expect(parse('1,5 kg flour')[0]).toMatchObject({ quantity: 1.5 })
    expect(parse('1.5 kg flour')[0]).toMatchObject({ quantity: 1.5 })
  })

  it('takes a multi-word name whole', () => {
    expect(parse('two packs of whole wheat pasta')[0]).toMatchObject({
      quantity: 2,
      unit: Unit.Pack,
      name: 'whole wheat pasta'
    })
  })

  it('ignores punctuation the speech engine adds', () => {
    expect(parse('Milk, bread.').map(item => item.name)).toEqual(['Milk', 'bread'])
  })

  it('returns nothing for an utterance with no item in it', () => {
    expect(parse('')).toEqual([])
    expect(parse('   ')).toEqual([])
    expect(parse('add please')).toEqual([])
  })

  it('treats a bare unit word as the name rather than throwing it away', () => {
    // "two litres" of nothing is not a sentence a parser can rescue, and a row saying "litres"
    // is something the reader can fix in one tap. Dropping it silently is the one thing this
    // feature must never do.
    expect(parse('two litres')[0]).toMatchObject({ quantity: 2, name: 'litres' })
  })

  it('falls back to the English grammar for a locale it does not know', () => {
    expect(parse('two litres of milk', 'fr')[0]).toMatchObject({ quantity: 2, name: 'milk' })
  })
})
