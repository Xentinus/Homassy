/**
 * Keep the `theme-color` meta in sync with the active colour mode.
 *
 * Why it matters: on iOS standalone the status-bar strip follows the page/theme
 * colour, and nothing used to rewrite the meta on a theme switch, so the top
 * strip stayed light after switching to the dark theme.
 *
 * The value is *read back* from the root element's computed background
 * (`html { background-color: var(--ui-bg) }`, see assets/css/main.css) rather
 * than being a second copy of the palette — so it can never drift from the app's
 * own background, whatever `--ui-bg` resolves to.
 *
 * It goes through `useHead` (not a direct DOM write): both nuxt.config's static
 * tag and @vite-pwa/nuxt's manifest-derived one are unhead-managed, so a manual
 * `meta.content = …` gets reverted on unhead's next flush. `tagPriority: 'high'`
 * makes this entry win the `meta[name]` dedupe.
 *
 * Client-only: reading computed styles needs a DOM, and the colour mode is
 * `unknown` during SSR — branching on it there would be a hydration mismatch.
 */

// Normalise to `#rrggbb`. Tailwind v4 palettes are oklch, and that is what
// getComputedStyle hands back; a hex triplet is what every `theme-color`
// implementation is guaranteed to parse. Canvas does the conversion, using the
// same colour parser as CSS, so it round-trips whatever the engine understood.
function toHex(color: string): string {
  const canvas = document.createElement('canvas')
  canvas.width = canvas.height = 1
  const ctx = canvas.getContext('2d')
  if (!ctx) return color
  ctx.fillStyle = color
  ctx.fillRect(0, 0, 1, 1)
  const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data
  if (r === undefined || g === undefined || b === undefined) return color
  return `#${[r, g, b].map(c => c.toString(16).padStart(2, '0')).join('')}`
}

export default defineNuxtPlugin((nuxtApp) => {
  const colorMode = useColorMode()
  // Passed to useHead as a ref (not a getter) — that is what unhead tracks, so
  // every write here re-patches the tag.
  const themeColor = ref<string | undefined>()

  const read = () => {
    const bg = getComputedStyle(document.documentElement).backgroundColor
    // Transparent means the stylesheet has not applied yet — leave the SSR value
    // in place rather than committing black.
    if (!bg || bg === 'transparent' || bg.startsWith('rgba(0, 0, 0, 0')) return
    themeColor.value = toHex(bg)
  }

  useHead({
    meta: [
      {
        name: 'theme-color',
        content: themeColor,
        tagPriority: 'high'
      }
    ]
  })

  // The `light`/`dark` class is already on <html> at this point (color-mode sets
  // it from a blocking head script), so read straight away…
  read()
  // …and again once mounted, because in dev the stylesheet is injected by Vite and
  // may not have applied yet, in which case the first read bails on transparent.
  nuxtApp.hook('app:mounted', read)

  // Synchronous on purpose. This watcher is registered after color-mode's own
  // (module plugins run before app plugins), so by the time it fires the class
  // swap is already committed and getComputedStyle resolves the new theme.
  // Deliberately not deferred to requestAnimationFrame: a backgrounded or
  // non-compositing webview does not run animation frames.
  watch(() => colorMode.value, read)

  // A suspended standalone webview can miss an OS-level light/dark switch while
  // backgrounded, so re-read on resume. Only for 'system' — an explicit
  // preference cannot go stale. Mirrors plugins/splash-resume.client.ts.
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible' && colorMode.preference === 'system') {
      read()
    }
  })
})
