import { ProductCategory } from '~/types/enums'
import type { ProductInfo } from '~/types/product'
import { normalizeForSearch } from '~/utils/stringUtils'

/**
 * Decides what a dictated name refers to (#132).
 *
 * Three rungs, in the order the issue asks for: an existing product first, because a household
 * buys the same things over and over; then the product-category vocabulary, which is 947
 * localized words and by far the best guess for a thing this family has never bought; and
 * finally the words themselves, as a free-text item.
 *
 * Nothing here decides anything on the reader's behalf — the match is a suggestion that lands
 * in an editable row they confirm. Its only job is to make the common case need no edit.
 */

export type VoiceMatchKind = 'product' | 'category' | 'custom'

export interface VoiceItemMatch {
  kind: VoiceMatchKind
  /** Set when `kind` is `product`. */
  product?: ProductInfo
  /** Set when `kind` is `category` — the enum's number, which is what the API stores. */
  category?: ProductCategory
  /** What to show on the row, and what a custom item is called. */
  label: string
}

/**
 * Hungarian object and plural endings, longest first, so "tejet" can find "tej".
 *
 * Only Hungarian gets this: a dictated English or German name barely inflects, while Hungarian
 * puts the sentence's grammar on the end of the noun — "két liter tejet" hands the matcher
 * "tejet", which matches nothing at all on its own.
 */
const HU_SUFFIXES = ['okat', 'eket', 'öket', 'akat', 'at', 'et', 'ot', 'öt', 'ök', 'ak', 'ek', 'ok', 'k', 't']

export const useVoiceItemMatching = () => {
  const { locale, getLocaleMessage } = useI18n()

  // One pass over 947 labels per locale, kept for the life of the page: a review drawer looks
  // up several names at once, and rebuilding the table for each of them would be the only
  // expensive thing this feature does.
  const categoryIndexCache = new Map<string, Map<string, ProductCategory>>()

  const categoryIndex = (): Map<string, ProductCategory> => {
    const cached = categoryIndexCache.get(locale.value)
    if (cached) return cached

    const index = new Map<string, ProductCategory>()
    const messages = getLocaleMessage(locale.value) as Record<string, unknown>
    const labels = (messages?.enums as Record<string, unknown> | undefined)?.productCategory

    if (labels && typeof labels === 'object') {
      for (const [value, label] of Object.entries(labels as Record<string, unknown>)) {
        if (typeof label !== 'string') continue
        const key = normalizeForSearch(label)
        // `Other` is the catch-all and matches nothing useful; a duplicate label would only
        // shadow an earlier, lower-numbered category, so the first one wins.
        if (!key || Number(value) === ProductCategory.Other || index.has(key)) continue
        index.set(key, Number(value) as ProductCategory)
      }
    }

    categoryIndexCache.set(locale.value, index)
    return index
  }

  /** The spellings to try for one dictated name, best first. */
  const candidateKeys = (name: string): string[] => {
    const key = normalizeForSearch(name)
    if (!key) return []

    const keys = [key]
    if (locale.value === 'hu') {
      for (const suffix of HU_SUFFIXES) {
        if (key.length > suffix.length + 2 && key.endsWith(suffix)) {
          keys.push(key.slice(0, -suffix.length))
        }
      }
    }

    return keys
  }

  const matchProduct = (keys: string[], products: ProductInfo[]): ProductInfo | undefined => {
    // Exact first, across every candidate spelling, before anything is allowed to match loosely
    // — a prefix hit on one spelling must not beat an exact hit on another.
    for (const key of keys) {
      const exact = products.find(product => normalizeForSearch(product.name) === key)
      if (exact) return exact
    }

    for (const key of keys) {
      const prefixed = products.find((product) => {
        const productKey = normalizeForSearch(product.name)
        return productKey.startsWith(key) || key.startsWith(productKey)
      })
      if (prefixed) return prefixed
    }

    for (const key of keys) {
      if (key.length < 4) continue
      const contained = products.find((product) => {
        const haystack = normalizeForSearch(`${product.name} ${product.brand}`)
        return haystack.includes(key)
      })
      if (contained) return contained
    }

    return undefined
  }

  const matchCategory = (keys: string[]): ProductCategory | undefined => {
    const index = categoryIndex()

    for (const key of keys) {
      const exact = index.get(key)
      if (exact !== undefined) return exact
    }

    // A category label is one or two words, so a prefix match is safe here in a way a substring
    // match would not be ("rice" must not match "Rice cooker" ahead of "Rice").
    for (const key of keys) {
      if (key.length < 4) continue
      for (const [label, value] of index) {
        if (label.startsWith(key)) return value
      }
    }

    return undefined
  }

  /**
   * @param name     The dictated name, as `parseVoiceItems` left it.
   * @param products What this family already has in the catalogue, to match against first.
   */
  const matchName = (name: string, products: ProductInfo[]): VoiceItemMatch => {
    const keys = candidateKeys(name)

    const product = matchProduct(keys, products)
    if (product) return { kind: 'product', product, label: product.name }

    const category = matchCategory(keys)
    if (category !== undefined) return { kind: 'category', category, label: name }

    return { kind: 'custom', label: name }
  }

  return { matchName }
}
