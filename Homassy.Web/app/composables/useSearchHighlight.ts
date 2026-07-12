/**
 * Wraps occurrences of a search query in a text with a highlighted <span> (mocha tint).
 * Case-insensitive; returns the original text when the query is empty or has no match.
 * The result is intended to be rendered with `v-html`.
 */
export const useSearchHighlight = () => {
  const escapeRegex = (str: string): string => str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

  const highlightText = (text: string, query: string): string => {
    if (!query || !text) return text

    const normalizedQuery = query.toLowerCase().trim()
    const normalizedText = text.toLowerCase()
    if (!normalizedText.includes(normalizedQuery)) return text

    const regex = new RegExp(`(${escapeRegex(normalizedQuery)})`, 'gi')
    return text.replace(regex, '<span class="font-bold text-primary-600 dark:text-primary-400 bg-primary-100 dark:bg-primary-900/30 px-1 py-0.5 rounded">$1</span>')
  }

  return { highlightText }
}
