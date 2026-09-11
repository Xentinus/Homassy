/**
 * Turning URLs inside a chat message into safe links (#147).
 *
 * A link is the **only** markup this app derives from user input. The message body itself is
 * rendered as text nodes — never `v-html` — so this module does not produce HTML: it splits a
 * string into typed segments and lets the template decide what an anchor looks like. That is the
 * whole safety property, and it is why this is a pure module rather than a formatter.
 *
 * The label is deliberately not the URL. A raw URL in a bubble is unreadable at any length worth
 * having, and a *rewritten* label is how a spoofed link works, so the rule is: the host is always
 * shown, in full, and only the path after it is ever shortened.
 */

/**
 * The scheme is optional in the text; a bare `www.foo.com` is a link people expect to work.
 *
 * Parentheses are *allowed* in the match and balanced afterwards by `trimTrailing`, because both
 * cases are real: a wiki URL ends in `)` legitimately, and a URL written inside brackets does not.
 * Excluding them outright would break the first; keeping them blindly would break the second.
 */
const URL_PATTERN = /\b(?:https?:\/\/|www\.)[^\s<>[\]{}"']+/gi

/** Trailing punctuation that belongs to the sentence rather than to the URL. */
const TRAILING_PUNCTUATION = /[.,;:!?]+$/

/** How much of a link label may be path before it is elided. The host is never shortened. */
const MAX_PATH_LENGTH = 24

export type ChatTextSegment =
  | { type: 'text', value: string }
  | { type: 'link', href: string, label: string, host: string }

/**
 * Trims the punctuation and unbalanced brackets a URL picks up from the sentence around it.
 *
 * "see https://example.com/a." ends in a full stop that is not part of the address, and
 * "(https://example.com/a)" has a closing paren the pattern already refused to swallow — but a
 * URL that legitimately ends in `)` (a wiki article, say) keeps it when the parens balance.
 */
const trimTrailing = (raw: string): string => {
  let value = raw.replace(TRAILING_PUNCTUATION, '')

  while (value.endsWith(')') && countOf(value, ')') > countOf(value, '(')) {
    value = value.slice(0, -1)
  }

  return value
}

const countOf = (value: string, char: string): number =>
  value.split(char).length - 1

/**
 * A readable label for a link: the host, then as much of the path as fits.
 *
 * The host is never truncated and never moved — a reader deciding whether to follow a link is
 * deciding whether they trust the site, and a label that hides or shortens the host is the one
 * thing that must not happen here.
 */
export const linkLabel = (href: string): { label: string, host: string } => {
  let url: URL
  try {
    url = new URL(href)
  } catch {
    return { label: href, host: href }
  }

  const host = url.host
  const rest = `${url.pathname === '/' ? '' : url.pathname}${url.search}${url.hash}`

  if (rest.length === 0) return { label: host, host }
  if (rest.length <= MAX_PATH_LENGTH) return { label: `${host}${rest}`, host }

  return { label: `${host}${rest.slice(0, MAX_PATH_LENGTH)}…`, host }
}

/**
 * Splits message text into plain and link segments, in order.
 *
 * Never returns HTML and never rewrites the text around a link: the concatenation of every
 * segment's source is the original message, character for character, which is what makes it safe
 * to render each segment as a text node.
 */
export const linkifySegments = (text: string): ChatTextSegment[] => {
  const segments: ChatTextSegment[] = []
  let lastIndex = 0

  for (const match of text.matchAll(URL_PATTERN)) {
    const raw = match[0]
    const start = match.index ?? 0
    const trimmed = trimTrailing(raw)

    if (trimmed.length === 0) continue

    if (start > lastIndex) {
      segments.push({ type: 'text', value: text.slice(lastIndex, start) })
    }

    // A bare `www.` link needs a scheme to be followable at all, and https rather than http: an
    // address typed without one in 2026 means the secure one.
    const href = trimmed.toLowerCase().startsWith('www.') ? `https://${trimmed}` : trimmed
    const { label, host } = linkLabel(href)

    segments.push({ type: 'link', href, label, host })
    lastIndex = start + trimmed.length
  }

  if (lastIndex < text.length) {
    segments.push({ type: 'text', value: text.slice(lastIndex) })
  }

  return segments
}
