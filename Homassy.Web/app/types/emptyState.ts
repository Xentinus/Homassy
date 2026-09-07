/**
 * Types for the empty-state illustration set (EmptyStateIllustration.vue,
 * rendered through EmptyState.vue and app/error.vue).
 */

/**
 * One drawing per thing that can be empty, plus the three the error page needs.
 *
 * `search` is deliberately separate from the per-entity names: "no results for
 * this filter" is a different situation from "nothing here yet" and gets its own
 * illustration and its own call to action.
 */
export type EmptyStateIllustrationName
  = | 'products'
    | 'shoppingList'
    | 'shoppingLocation'
    | 'storageLocation'
    | 'automation'
    | 'calendar'
    | 'notifications'
    | 'search'
    | 'notFound'
    | 'serverError'
    | 'offline'
