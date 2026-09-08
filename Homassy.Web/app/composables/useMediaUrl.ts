/**
 * Turns the server-relative media paths the API hands out (`/api/v1.0/User/{id}/profile-picture?…`)
 * into URLs an `<img>` can load.
 *
 * The API returns paths rather than absolute URLs because only the client knows where the API is:
 * a separate origin in development, the same origin behind the reverse proxy in production. That
 * also means an image request is cross-origin in development, which is why every consumer sets
 * `crossorigin="use-credentials"` — without it the browser omits the Kratos session cookie and the
 * endpoint answers 401.
 */
export const useMediaUrl = () => {
  const config = useRuntimeConfig()

  const mediaUrl = (path?: string | null): string | undefined => {
    if (!path) return undefined
    // Absolute or inline URLs pass through, so a caller can hand this an external image.
    if (/^(https?:)?\/\//.test(path) || path.startsWith('data:') || path.startsWith('blob:')) {
      return path
    }

    const base = String(config.public.apiBase ?? '').replace(/\/+$/, '')
    return `${base}${path.startsWith('/') ? path : `/${path}`}`
  }

  return { mediaUrl }
}
