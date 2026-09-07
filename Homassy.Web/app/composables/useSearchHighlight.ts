/**
 * Wraps occurrences of a search query in a text with a highlighted <span> (mocha tint).
 * Case-insensitive; returns the text unchanged when the query is empty or has no match.
 * The result is rendered with `v-html`.
 *
 * Because it is rendered with `v-html`, everything that is not the highlight
 * span this file generates has to be escaped first. The text is a product name,
 * a brand, a note, a location description — all of it typed in by a user, so a
 * name like `<img src=x onerror=...>` would otherwise execute on every card it
 * appears on. Matches are located in the *raw* text and each piece is escaped as
 * it is emitted, so escaping cannot shift the offsets the search found.
 */
const HTML_ESCAPES: Record<string, string> = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  '\'': '&#39;'
}

const HIGHLIGHT_CLASS = 'font-bold text-primary-600 dark:text-primary-400 bg-primary-100 dark:bg-primary-900/30 px-1 py-0.5 rounded'

export const useSearchHighlight = () => {
  const escapeRegex = (str: string): string => str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

  const escapeHtml = (value: string): string => value.replace(/[&<>"']/g, char => HTML_ESCAPES[char] ?? char)

  const highlightText = (text: string, query: string): string => {
    if (!query || !text) return escapeHtml(text ?? '')

    const normalizedQuery = query.toLowerCase().trim()
    const normalizedText = text.toLowerCase()
    if (!normalizedText.includes(normalizedQuery)) return escapeHtml(text)

    const regex = new RegExp(`(${escapeRegex(normalizedQuery)})`, 'gi')

    // `split` on a regex with one capture group interleaves the pieces with the
    // matches, so every odd index is a hit.
    return text
      .split(regex)
      .map((part, index) => (index % 2 === 1
        ? `<span class="${HIGHLIGHT_CLASS}">${escapeHtml(part)}</span>`
        : escapeHtml(part)))
      .join('')
  }

  return { highlightText }
}
