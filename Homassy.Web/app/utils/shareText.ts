/**
 * Pure helpers for the two decisions the share target and the app shortcuts have to
 * make about untrusted strings (#118). Plain functions in their own module so they can
 * be unit-tested without a Nuxt runtime — see `tests/unit/shareText.spec.ts`.
 */

/**
 * The longest name either target field accepts: `CreateProductRequest.Name` and the
 * custom shopping-list item's schema both cap at 128 characters.
 */
export const SHARED_NAME_MAX_LENGTH = 128

/**
 * Reduce a share down to something that can seed a name field: the sharing app's title
 * if it gave one, otherwise the first line of the shared text.
 *
 * The first line, not the whole text: apps share paragraphs, and a name field wants a
 * name. Trimmed to `SHARED_NAME_MAX_LENGTH`, so a shared essay seeds a field that
 * validates rather than one that fails on sight.
 *
 * The first line with something on it, specifically — plenty of apps put a blank line
 * or a leading newline before the content, and taking `split('\n')[0]` verbatim would
 * turn the whole share into "nothing to suggest".
 */
export function shareNameFrom(content: { title?: string | null, text?: string | null }): string | null {
  const source = content.title?.trim()
    || content.text?.split('\n').map(line => line.trim()).find(line => line.length > 0)
  if (!source) return null
  return source.slice(0, SHARED_NAME_MAX_LENGTH)
}

/**
 * Validate a `return_to` value from the login page's query string, falling back to the
 * calendar.
 *
 * The parameter used to be dead — nothing wrote it — and is now written on every gated
 * navigation so a deep link survives the auth gate. That makes it reachable by anyone
 * who can hand the user a link to the login page, so only an in-app path is accepted:
 *
 * - it must start with exactly one `/` (never two, which is a protocol-relative URL);
 * - it must not point back into `/auth/`, which would be a login loop.
 *
 * `router.push` resolves a string as a route path rather than a URL, so an absolute
 * target would already have failed harmlessly — but `//evil.example` is the shape worth
 * refusing outright rather than reasoning about.
 */
export function safeReturnTo(value: unknown, fallback = '/calendar'): string {
  const target = Array.isArray(value) ? value[0] : value
  if (typeof target !== 'string' || !target) return fallback
  if (!target.startsWith('/') || target.startsWith('//')) return fallback
  if (target.startsWith('/auth/')) return fallback
  return target
}
