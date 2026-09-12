/**
 * Turns one dictated sentence into a list of items (#132).
 *
 * "add two litres of milk, bread and six eggs" becomes three entries: 2 litres of milk, one
 * bread, six eggs. It is deliberately a pure function over a string — no speech API, no i18n
 * instance, no network — so the grammar can be tested on its own, which is the only way to be
 * confident about a feature whose input is a microphone.
 *
 * What it does NOT do is decide what the names mean. Resolving "milk" to a product, a category
 * or a free-text item is `useVoiceItemMatching`'s job: the parser stops at quantity, unit and
 * the words in between, because those are the parts that are grammar rather than data.
 *
 * Three locales, one grammar: `[quantity] [unit] [filler] name`, with a trailing-quantity
 * fallback ("milk 2 litres") for the way people actually read a list out loud.
 */
import { Unit } from '~/types/enums'
import { normalizeForSearch } from './stringUtils'

export type VoiceLocale = 'en' | 'hu' | 'de'

export interface ParsedVoiceItem {
  /** How many. Defaults to 1 when the utterance gave no number. */
  quantity: number
  /** The unit that was actually spoken, or `undefined` when none was. */
  unit?: Unit
  /** What is left after the quantity and the unit — the thing to look up. */
  name: string
  /** The fragment this came from, so the review row can show what was heard. */
  raw: string
}

/**
 * Words that split one utterance into several items. Commas and semicolons split too, and are
 * handled separately because a speech engine punctuates unpredictably.
 */
const SEPARATORS: Record<VoiceLocale, string[]> = {
  en: ['and', 'plus', 'then'],
  hu: ['es', 'meg', 'plusz'],
  de: ['und', 'plus', 'sowie']
}

/**
 * Words to drop wherever they appear in a fragment: the verb people start with, and the
 * particle between a unit and what it measures.
 */
const NOISE_WORDS: Record<VoiceLocale, string[]> = {
  en: ['add', 'put', 'buy', 'of', 'a', 'an', 'the', 'some', 'please'],
  hu: ['adj', 'add', 'hozza', 'hozzaad', 'vegyel', 'kell', 'legyen'],
  de: ['fuge', 'hinzu', 'kaufe', 'bitte', 'noch', 'ein', 'eine', 'einen']
}

/** Spoken numbers, normalized (accents already stripped). Covers what a shopping list needs. */
const NUMBER_WORDS: Record<VoiceLocale, Record<string, number>> = {
  en: {
    half: 0.5, one: 1, two: 2, three: 3, four: 4, five: 5, six: 6, seven: 7, eight: 8,
    nine: 9, ten: 10, eleven: 11, twelve: 12, dozen: 12, thirteen: 13, fourteen: 14,
    fifteen: 15, sixteen: 16, seventeen: 17, eighteen: 18, nineteen: 19, twenty: 20
  },
  hu: {
    fel: 0.5, egy: 1, ket: 2,ketto: 2, harom: 3, negy: 4, ot: 5, hat: 6, het: 7,
    nyolc: 8, kilenc: 9, tiz: 10, tizenegy: 11, tizenketto: 12, tizenket: 12, tucat: 12,
    tizenharom: 13, tizennegy: 14, tizenot: 15, tizenhat: 16, tizenhet: 17,
    tizennyolc: 18, tizenkilenc: 19, husz: 20
  },
  de: {
    halb: 0.5, ein: 1, eine: 1, eins: 1, zwei: 2, drei: 3, vier: 4, funf: 5, sechs: 6,
    sieben: 7, acht: 8, neun: 9, zehn: 10, elf: 11, zwolf: 12, dutzend: 12,
    dreizehn: 13, vierzehn: 14, funfzehn: 15, sechzehn: 16, siebzehn: 17,
    achtzehn: 18, neunzehn: 19, zwanzig: 20
  }
}

/**
 * Spoken units, normalized. Every spelling a speech engine is likely to produce for the same
 * unit maps to the same enum member; the symbol forms (`kg`, `ml`) are shared across locales.
 */
const UNIT_WORDS: Record<VoiceLocale, Record<string, Unit>> = {
  en: {
    pc: Unit.Piece, pcs: Unit.Piece, piece: Unit.Piece, pieces: Unit.Piece,
    g: Unit.Gram, gram: Unit.Gram, grams: Unit.Gram, gramme: Unit.Gram, grammes: Unit.Gram,
    kg: Unit.Kilogram, kilo: Unit.Kilogram, kilos: Unit.Kilogram, kilogram: Unit.Kilogram, kilograms: Unit.Kilogram,
    mg: Unit.Milligram, milligram: Unit.Milligram, milligrams: Unit.Milligram,
    ml: Unit.Milliliter, milliliter: Unit.Milliliter, milliliters: Unit.Milliliter, millilitre: Unit.Milliliter, millilitres: Unit.Milliliter,
    cl: Unit.Centiliter, centiliter: Unit.Centiliter, centilitre: Unit.Centiliter,
    dl: Unit.Deciliter, deciliter: Unit.Deciliter, decilitre: Unit.Deciliter,
    l: Unit.Liter, liter: Unit.Liter, liters: Unit.Liter, litre: Unit.Liter, litres: Unit.Liter,
    m: Unit.Meter, meter: Unit.Meter, meters: Unit.Meter, metre: Unit.Meter, metres: Unit.Meter,
    cm: Unit.Centimeter, centimeter: Unit.Centimeter, centimetre: Unit.Centimeter,
    mm: Unit.Millimeter, millimeter: Unit.Millimeter, millimetre: Unit.Millimeter,
    tsp: Unit.Teaspoon, teaspoon: Unit.Teaspoon, teaspoons: Unit.Teaspoon,
    tbsp: Unit.Tablespoon, tablespoon: Unit.Tablespoon, tablespoons: Unit.Tablespoon,
    cup: Unit.Cup, cups: Unit.Cup,
    pack: Unit.Pack, packs: Unit.Pack, packet: Unit.Pack, packets: Unit.Pack,
    box: Unit.Box, boxes: Unit.Box,
    bottle: Unit.Bottle, bottles: Unit.Bottle,
    can: Unit.Can, cans: Unit.Can, tin: Unit.Can, tins: Unit.Can,
    jar: Unit.Jar, jars: Unit.Jar,
    bag: Unit.Bag, bags: Unit.Bag
  },
  hu: {
    db: Unit.Piece, darab: Unit.Piece,
    g: Unit.Gram, gramm: Unit.Gram,
    kg: Unit.Kilogram, kilo: Unit.Kilogram, kilogramm: Unit.Kilogram,
    mg: Unit.Milligram, milligramm: Unit.Milligram,
    ml: Unit.Milliliter, milliliter: Unit.Milliliter,
    cl: Unit.Centiliter, centiliter: Unit.Centiliter,
    dl: Unit.Deciliter, deci: Unit.Deciliter, deciliter: Unit.Deciliter,
    l: Unit.Liter, liter: Unit.Liter, litert: Unit.Liter,
    m: Unit.Meter, meter: Unit.Meter,
    cm: Unit.Centimeter, centimeter: Unit.Centimeter,
    mm: Unit.Millimeter, millimeter: Unit.Millimeter,
    teaskanal: Unit.Teaspoon, kavaskanal: Unit.Teaspoon,
    evokanal: Unit.Tablespoon,
    csesze: Unit.Cup, bogre: Unit.Cup,
    csomag: Unit.Pack,
    doboz: Unit.Box,
    uveg: Unit.Bottle, palack: Unit.Bottle,
    konzerv: Unit.Can,
    befottesuveg: Unit.Jar,
    zacsko: Unit.Bag, zsak: Unit.Bag
  },
  de: {
    stk: Unit.Piece, stuck: Unit.Piece, stucke: Unit.Piece,
    g: Unit.Gram, gramm: Unit.Gram,
    kg: Unit.Kilogram, kilo: Unit.Kilogram, kilogramm: Unit.Kilogram,
    mg: Unit.Milligram, milligramm: Unit.Milligram,
    ml: Unit.Milliliter, milliliter: Unit.Milliliter,
    cl: Unit.Centiliter, zentiliter: Unit.Centiliter,
    dl: Unit.Deciliter, deziliter: Unit.Deciliter,
    l: Unit.Liter, liter: Unit.Liter,
    m: Unit.Meter, meter: Unit.Meter,
    cm: Unit.Centimeter, zentimeter: Unit.Centimeter,
    mm: Unit.Millimeter, millimeter: Unit.Millimeter,
    tl: Unit.Teaspoon, teeloffel: Unit.Teaspoon,
    el: Unit.Tablespoon, essloffel: Unit.Tablespoon,
    tasse: Unit.Cup, tassen: Unit.Cup,
    packung: Unit.Pack, paket: Unit.Pack, packungen: Unit.Pack,
    schachtel: Unit.Box, karton: Unit.Box,
    flasche: Unit.Bottle, flaschen: Unit.Bottle,
    dose: Unit.Can, dosen: Unit.Can,
    glas: Unit.Jar, glaser: Unit.Jar,
    beutel: Unit.Bag, tute: Unit.Bag
  }
}

/** Falls back to English for a locale the app does not ship — the grammar is the same shape. */
const forLocale = <T>(table: Record<VoiceLocale, T>, locale: string): T =>
  table[locale as VoiceLocale] ?? table.en

/** A number, spoken or written. `1,5` and `1.5` are the same number in every locale here. */
const readQuantity = (token: string, locale: string): number | null => {
  const numeric = token.replace(',', '.')
  if (/^\d+(\.\d+)?$/.test(numeric)) {
    const value = Number.parseFloat(numeric)
    return value > 0 ? value : null
  }

  return forLocale(NUMBER_WORDS, locale)[token] ?? null
}

const readUnit = (token: string, locale: string): Unit | undefined =>
  forLocale(UNIT_WORDS, locale)[token]

interface Token {
  /** As spoken, accents and capitals intact — this is what is shown back. */
  raw: string
  /** Accent-stripped and lowercased, for looking up in the tables above. */
  key: string
}

/** Punctuation a speech engine sprinkles around words, stripped from both forms. */
const EDGE_PUNCTUATION = /^[^\p{L}\p{N}]+|[^\p{L}\p{N}]+$/gu

const tokenize = (fragment: string): Token[] =>
  fragment
    .split(/\s+/)
    .map((word) => {
      const raw = word.replace(EDGE_PUNCTUATION, '')
      return { raw, key: normalizeForSearch(raw) }
    })
    .filter(token => token.key.length > 0)

/**
 * Splits an utterance into one token list per item, on punctuation and on the locale's own list
 * conjunctions.
 *
 * It works on tokens rather than on string offsets on purpose: stripping accents changes a
 * string's length, so an index found in the normalized text does not point at the same place in
 * the original — and the original is what is shown back to the reader, accents and capitals
 * intact.
 */
const splitFragments = (transcript: string, locale: string): Token[][] => {
  const separators = new Set(forLocale(SEPARATORS, locale))
  const fragments: Token[][] = []

  // A comma between two digits is a decimal separator, not a list separator — "1,5 kg" is one
  // quantity in two of the three locales, and splitting it would silently halve the amount.
  for (const chunk of transcript.split(/;+|,(?![0-9])|(?<![0-9]),/)) {
    let current: Token[] = []

    for (const token of tokenize(chunk)) {
      if (separators.has(token.key)) {
        if (current.length) fragments.push(current)
        current = []
        continue
      }
      current.push(token)
    }

    if (current.length) fragments.push(current)
  }

  return fragments
}

/**
 * Reads one fragment as `[quantity] [unit] name`, falling back to `name [quantity] [unit]`.
 *
 * The trailing form matters more than it looks: reading a written list aloud produces "milk two
 * litres" at least as often as "two litres of milk".
 */
const parseFragment = (fragment: Token[], locale: string): ParsedVoiceItem | null => {
  const noise = new Set(forLocale(NOISE_WORDS, locale))
  const raw = fragment.map(token => token.raw).join(' ')

  // A fragment that is nothing but noise — "add", "please" — is not an item.
  const tokens = fragment.filter(token => !noise.has(token.key))
  if (tokens.length === 0) return null

  let quantity: number | null = null
  let unit: Unit | undefined
  let nameTokens = tokens

  const leadingQuantity = readQuantity(tokens[0]!.key, locale)
  if (leadingQuantity !== null) {
    quantity = leadingQuantity
    nameTokens = tokens.slice(1)

    const leadingUnit = nameTokens[0] ? readUnit(nameTokens[0].key, locale) : undefined
    if (leadingUnit !== undefined && nameTokens.length > 1) {
      unit = leadingUnit
      nameTokens = nameTokens.slice(1)
    }
  } else {
    // Trailing form. The unit may or may not be there: "milk 2" and "milk 2 litres" both work.
    const last = tokens[tokens.length - 1]!
    const secondLast = tokens[tokens.length - 2]
    const trailingUnit = readUnit(last.key, locale)

    if (trailingUnit !== undefined && secondLast && tokens.length > 2) {
      const value = readQuantity(secondLast.key, locale)
      if (value !== null) {
        quantity = value
        unit = trailingUnit
        nameTokens = tokens.slice(0, -2)
      }
    } else if (tokens.length > 1) {
      const value = readQuantity(last.key, locale)
      if (value !== null) {
        quantity = value
        nameTokens = tokens.slice(0, -1)
      }
    }
  }

  // Everything was a number and a unit and nothing else — "two litres" is not an item.
  const name = nameTokens.map(token => token.raw).join(' ').trim()
  if (!name) return null

  return {
    quantity: quantity ?? 1,
    unit,
    name,
    raw
  }
}

/**
 * Parse a whole utterance.
 *
 * @param transcript What the speech engine heard.
 * @param locale     The app's current locale — `en`, `hu` or `de`; anything else reads as `en`.
 */
export const parseVoiceItems = (transcript: string, locale: string): ParsedVoiceItem[] => {
  if (!transcript?.trim()) return []

  return splitFragments(transcript, locale)
    .map(fragment => parseFragment(fragment, locale))
    .filter((item): item is ParsedVoiceItem => item !== null)
}
