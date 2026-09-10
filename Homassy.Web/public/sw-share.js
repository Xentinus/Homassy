/**
 * Web Share Target handler (#118).
 *
 * `share_target` in the manifest is declared `method: "POST"` because a shared image
 * can only arrive as `multipart/form-data`. A POST navigation cannot be answered by
 * the page itself — the browser posts it straight at the service worker — so this
 * script intercepts it, stashes the payload where the app can read it, and answers
 * with a redirect to the ordinary GET route `/share`.
 *
 * Why the Cache API and not the URL: a shared file has no query-string
 * representation, and shared text can be arbitrarily long (and is the user's own
 * content — it has no business sitting in a history entry). The payload lands under
 * synthetic same-origin URLs in a cache of its own, which the `/share` page reads and
 * then deletes.
 *
 * Loaded through `workbox.importScripts`, so this `fetch` listener is registered
 * before Workbox registers its own routing listener and therefore sees the event
 * first. Workbox's routes are GET-only in any case, so a POST navigation would never
 * reach them.
 */

/** Cache holding at most one pending share payload. */
const SHARE_CACHE = 'share-target-v1';

/** The manifest's `share_target.action`. Kept in step with nuxt.config.ts. */
const SHARE_ACTION = '/share';

/** Where the payload's metadata lives inside the cache. */
const SHARE_PAYLOAD_URL = '/__share-target/payload.json';

/** Prefix for one shared file's bytes inside the cache. */
const SHARE_FILE_PREFIX = '/__share-target/file-';

/**
 * A shared image larger than this is dropped rather than cached: the product image
 * flow compresses to well under 1 MB before upload anyway, and caching a 50 MB video
 * someone shared by mistake would evict the app's own precache.
 */
const MAX_SHARED_FILE_BYTES = 12 * 1024 * 1024;

self.addEventListener('fetch', (event) => {
  const request = event.request;
  if (request.method !== 'POST') return;

  let url;
  try {
    url = new URL(request.url);
  } catch {
    return;
  }

  if (url.origin !== self.location.origin || url.pathname !== SHARE_ACTION) return;

  event.respondWith(handleShare(request));
});

async function handleShare(request) {
  try {
    const formData = await request.formData();
    const cache = await caches.open(SHARE_CACHE);

    // One payload at a time. A second share replaces the first rather than queueing:
    // the user is looking at the app they just shared into, not at a backlog.
    await clearShareCache(cache);

    const files = [];
    let index = 0;

    for (const value of formData.getAll('image')) {
      if (typeof value === 'string' || !value) continue;
      if (value.size > MAX_SHARED_FILE_BYTES) continue;

      const fileUrl = `${SHARE_FILE_PREFIX}${index++}`;
      await cache.put(
        new Request(fileUrl),
        new Response(value, {
          headers: { 'Content-Type': value.type || 'application/octet-stream' }
        })
      );
      files.push({ url: fileUrl, name: value.name || '', type: value.type || '', size: value.size });
    }

    const payload = {
      title: asText(formData.get('title')),
      text: asText(formData.get('text')),
      url: asText(formData.get('url')),
      files,
      receivedAt: Date.now()
    };

    await cache.put(
      new Request(SHARE_PAYLOAD_URL),
      new Response(JSON.stringify(payload), {
        headers: { 'Content-Type': 'application/json' }
      })
    );
  } catch (error) {
    // Nothing was stashed, so the page will simply show its empty state. Swallowing
    // is deliberate: a thrown error here would leave the user staring at the
    // browser's own network-error screen with no way back into an installed app.
    console.error('[ShareTarget] failed to stash payload', error);
  }

  // 303 so the browser re-issues the navigation as a GET, which is what makes the
  // SPA route load normally (and keeps the POST out of the history entry).
  return Response.redirect(SHARE_ACTION, 303);
}

function asText(value) {
  return typeof value === 'string' && value.trim() ? value.trim() : null;
}

async function clearShareCache(cache) {
  const keys = await cache.keys();
  await Promise.all(keys.map((key) => cache.delete(key)));
}
