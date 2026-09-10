/**
 * Reads (and clears) the payload `public/sw-share.js` stashed when another app shared
 * something into Homassy (#118).
 *
 * The service worker cannot hand a POST body to the page, so it puts the shared
 * fields — and each shared file's bytes — into a cache of their own and 303s to
 * `/share`. This composable is the page's side of that handoff: it reads the cache,
 * materialises the files as `File` objects, and then empties the cache.
 *
 * **The read is destructive on purpose.** A shared payload is a one-time intent; if it
 * survived, refreshing `/share` (or navigating back to it days later) would replay
 * someone's shared photo into a fresh product form. The caller is handed the only copy
 * and owns it from then on.
 */

/** Must match `SHARE_CACHE` in `public/sw-share.js`. */
const SHARE_CACHE = 'share-target-v1'

/** Must match `SHARE_PAYLOAD_URL` in `public/sw-share.js`. */
const SHARE_PAYLOAD_URL = '/__share-target/payload.json'

/**
 * A payload older than this is discarded unread. The window exists for the case where
 * the share arrived, the browser killed the tab before `/share` rendered, and the user
 * opened the app again much later — resurrecting a forgotten share as a half-filled
 * form is confusing, and the cache is not a durable inbox.
 */
const MAX_PAYLOAD_AGE_MS = 10 * 60 * 1000

/**
 * The `/share` page's handoff slot for the shopping-list route.
 *
 * `/share` keeps the *product* case for itself (a shared image is a `File`, which
 * cannot travel in a URL), but hands the shopping-list case to
 * `/shopping-lists?action=add-custom` — that page already owns list selection, the
 * add-item wizard and the realtime group, none of which is worth a second copy.
 *
 * The name rides in this module-scoped ref rather than in the query string. Both
 * pages live in the same SPA session, so the ref survives the navigation, and shared
 * text is the user's own content — it has no business in a history entry, however
 * briefly.
 */
const handoffItemName = ref<string | null>(null)

interface StashedFile {
  url: string
  name: string
  type: string
  size: number
}

interface StashedPayload {
  title: string | null
  text: string | null
  url: string | null
  files: StashedFile[]
  receivedAt: number
}

export interface SharedContent {
  /** The subject line, where the sharing app supplied one. */
  title: string | null
  /** The shared text. Often the whole of a share from a notes or messaging app. */
  text: string | null
  /** The shared link, where the sharing app separated it from the text. */
  url: string | null
  /** Shared images, in the order the sharing app listed them. */
  files: File[]
}

export const useShareTarget = () => {
  const isSupported = computed(() => import.meta.client && 'caches' in globalThis)

  /**
   * Take the pending shared payload, or `null` when there is none (or it is too old,
   * or the platform has no Cache API). Either way the cache is left empty.
   */
  async function takeSharedContent(): Promise<SharedContent | null> {
    if (!isSupported.value) return null

    let cache: Cache
    try {
      cache = await caches.open(SHARE_CACHE)
    } catch {
      return null
    }

    try {
      const response = await cache.match(SHARE_PAYLOAD_URL)
      if (!response) return null

      const payload = (await response.json()) as StashedPayload
      if (!payload || typeof payload.receivedAt !== 'number') return null

      if (Date.now() - payload.receivedAt > MAX_PAYLOAD_AGE_MS) return null

      const files: File[] = []
      for (const entry of payload.files ?? []) {
        const fileResponse = await cache.match(entry.url)
        if (!fileResponse) continue
        const blob = await fileResponse.blob()
        files.push(new File([blob], entry.name || 'shared-image', {
          type: entry.type || blob.type || 'application/octet-stream'
        }))
      }

      return {
        title: payload.title ?? null,
        text: payload.text ?? null,
        url: payload.url ?? null,
        files
      }
    } catch (error) {
      console.error('[ShareTarget] failed to read the shared payload', error)
      return null
    } finally {
      // Always, including on the error and too-old paths: whatever is in there is
      // either consumed or unusable, and leaving it would replay it on the next visit.
      try {
        const keys = await cache.keys()
        await Promise.all(keys.map(key => cache.delete(key)))
      } catch {
        // Nothing to do — the payload read already happened (or already failed).
      }
    }
  }

  /** Park a name for the shopping-list page to pick up. See `handoffItemName`. */
  function setHandoffItemName(name: string | null) {
    handoffItemName.value = name?.trim() || null
  }

  /**
   * Read and clear the parked name. Destructive for the same reason
   * `takeSharedContent` is: it is a one-time intent, not page state.
   */
  function takeHandoffItemName(): string | null {
    const name = handoffItemName.value
    handoffItemName.value = null
    return name
  }

  return { isSupported, takeSharedContent, setHandoffItemName, takeHandoffItemName }
}
