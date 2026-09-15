/**
 * The one gate between a stored URL and an `href`.
 *
 * A row's URL is validated by the API when it is saved, but that is not the same as trusting it
 * at render time: rows predate this validation, and the value reaches the page through the list
 * snapshot, a realtime broadcast and the edit form alike. `javascript:` in an `href` runs on
 * click, so the scheme is checked where the link is built, every time.
 */
export const toSafeHttpUrl = (value: string | null | undefined): string | null => {
  const trimmed = value?.trim()
  if (!trimmed) return null

  try {
    const url = new URL(trimmed)
    return url.protocol === 'http:' || url.protocol === 'https:' ? trimmed : null
  } catch {
    // Not a URL at all — a bare host included. Expanding "example.com" into a link would mean
    // guessing a scheme on the user's behalf, which is how a wrong destination gets opened.
    return null
  }
}
