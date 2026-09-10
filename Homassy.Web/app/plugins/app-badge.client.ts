/**
 * Installs the document-title prefix side of `useAppBadge` (#130).
 *
 * A plugin rather than a component `useHead`: the prefix belongs to the whole app,
 * and a `useHead` registered inside a page or layout is torn down with it — which
 * on a page-to-page navigation leaves a window where the count is not in the title,
 * and on an unmount leaves whichever value was last patched behind.
 *
 * Client-only. During SSR the count is always 0 (the expiring-items fetch is an
 * authenticated client-side call), so a server-rendered prefix could only ever be
 * wrong, and a wrong one would be a hydration mismatch on the `<title>`.
 *
 * The icon-badge side needs no installation — `useAppBadge` watches its own state
 * and calls `navigator.setAppBadge` directly.
 */
export default defineNuxtPlugin(() => {
  const { titleTemplate } = useAppBadge()

  // The whole input is a computed, so unhead re-patches the title when the count
  // changes. `%s` is substituted with the title app.vue's useSeoMeta sets.
  useHead(computed(() => ({ titleTemplate: titleTemplate.value })))
})
