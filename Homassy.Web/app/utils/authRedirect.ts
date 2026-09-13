/**
 * Where an unauthenticated caller gets sent, and what it carries.
 *
 * There are two places that bounce a user to the login page — `middleware/auth.ts` for a
 * navigation into a protected route, and `plugins/api.ts` for a 401 from the API — and they
 * have to agree. They did not: the middleware carried the destination while the 401 path
 * navigated to a bare `/auth/login`, so a session that lapsed while the app was open threw
 * away whatever the user was doing and dropped them on the calendar (#87). One helper, so the
 * next place that needs to do this cannot get it wrong either.
 *
 * The value this produces is validated again on the way out by `safeReturnTo`: it becomes a
 * query parameter, so the login page never trusts the shape of what ends up in the URL.
 */
export const LOGIN_PATH = '/auth/login'

export interface LoginRedirect {
  path: string
  query?: { return_to: string }
}

/**
 * The login navigation for a user currently at `currentFullPath`.
 *
 * Returns `null` when the caller is already somewhere under `/auth/`: bouncing from there is
 * the redirect loop this and `safeReturnTo` both exist to refuse.
 */
export function loginRedirectFor(currentFullPath: unknown): LoginRedirect | null {
  if (typeof currentFullPath !== 'string' || !currentFullPath) return { path: LOGIN_PATH }

  if (currentFullPath.startsWith('/auth/') || currentFullPath === '/auth') return null

  // Same rule as safeReturnTo: exactly one leading slash, so `//evil.example` — a
  // protocol-relative URL — is never carried even if something hands us one.
  if (!currentFullPath.startsWith('/') || currentFullPath.startsWith('//')) return { path: LOGIN_PATH }

  return { path: LOGIN_PATH, query: { return_to: currentFullPath } }
}
