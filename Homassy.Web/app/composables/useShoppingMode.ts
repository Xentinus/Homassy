/**
 * Which lists are in shopping mode (#131).
 *
 * Module-scoped rather than page state, because the mode has to survive leaving the page: someone
 * who opens a product, answers a notification, or checks the calendar mid-shop comes back to the
 * list they were shopping, not to the planning view. It is deliberately *not* persisted to storage —
 * "while the app is open" is the whole scope, and a mode that outlived a reload would put a user who
 * got home to a cold start straight back into a shop screen.
 *
 * Per list, not a single flag: two lists can each be mid-shop, and the mode belongs to the one
 * being looked at.
 */
const activeLists = ref<Set<string>>(new Set())

export const useShoppingMode = () => {
  const isActiveFor = (listPublicId: string | null | undefined): boolean =>
    !!listPublicId && activeLists.value.has(listPublicId)

  const enter = (listPublicId: string) => {
    activeLists.value = new Set(activeLists.value).add(listPublicId)
  }

  const exit = (listPublicId: string | null | undefined) => {
    if (!listPublicId || !activeLists.value.has(listPublicId)) return
    const next = new Set(activeLists.value)
    next.delete(listPublicId)
    activeLists.value = next
  }

  const toggle = (listPublicId: string) => {
    if (isActiveFor(listPublicId)) exit(listPublicId)
    else enter(listPublicId)
  }

  return { isActiveFor, enter, exit, toggle }
}
