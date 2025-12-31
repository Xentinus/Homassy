/**
 * String utility functions
 */

/**
 * Normalizes a string for accent-insensitive search
 * Mirrors the backend NormalizeForSearch() method
 * @param text The text to normalize
 * @returns Normalized lowercase string without accents
 */
export function normalizeForSearch(text: string | null | undefined): string {
  if (!text) return ''

  return text
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
}
