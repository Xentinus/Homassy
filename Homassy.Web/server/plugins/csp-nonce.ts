/**
 * Stamps the per-request CSP nonce onto every inline `<script>` Nuxt renders.
 *
 * The Content-Security-Policy itself is set by the reverse proxy (Homassy.Proxy/Caddyfile),
 * which generates one nonce per request, puts it in the `script-src` directive and forwards the
 * same value upstream as `X-CSP-Nonce`. Hashes are not an option for these tags: the runtime
 * config block embeds the deployment's NUXT_PUBLIC_* values and the import map embeds the entry
 * chunk's content hash, so both change outside this repository's control.
 *
 * No header means no CSP in front of us — `npm run dev`, or the app reached directly on :3000 —
 * and then nothing is stamped, which is exactly right: an unused nonce attribute is inert.
 *
 * `/offline` is prerendered, so this hook runs for it at build time with no header, and the
 * Caddyfile gives that one route a policy that does not rely on a nonce.
 */

// A nonce is a base64 value. Anything else is either a misconfigured proxy or a client trying to
// choose the nonce itself, and a nonce the attacker picked is no better than 'unsafe-inline'.
const NONCE_PATTERN = /^[A-Za-z0-9+/\-_]{8,128}={0,2}$/

// Only tags that do not already carry a nonce, so re-running is a no-op.
const SCRIPT_TAG_PATTERN = /<script(?![^>]*\snonce=)/g

export default defineNitroPlugin((nitroApp) => {
  nitroApp.hooks.hook('render:html', (html, { event }) => {
    const nonce = getRequestHeader(event, 'x-csp-nonce')
    if (!nonce || !NONCE_PATTERN.test(nonce)) return

    const stamp = (chunk: string) => chunk.replace(SCRIPT_TAG_PATTERN, `<script nonce="${nonce}"`)

    // Head and the appended body chunks only. `html.body` is the rendered application, where a
    // `<script` sequence can only be escaped user content — rewriting that would corrupt it.
    html.head = html.head.map(stamp)
    html.bodyPrepend = html.bodyPrepend.map(stamp)
    html.bodyAppend = html.bodyAppend.map(stamp)
  })
})
