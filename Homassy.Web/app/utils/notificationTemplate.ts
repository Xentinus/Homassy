/**
 * Maps a stored notification onto the i18n keys and icon that render it (#116).
 *
 * The rows carry a type and parameters, never prose, so this is where a row becomes something a
 * reader can read — in whatever language they are using now, not the one they were using when it
 * was delivered. Pure and framework-free so it can be unit-tested without a DOM; the drawer feeds
 * the result straight to `t()`.
 *
 * The type names are the API's `NotificationType` member names verbatim (`'ShoppingListItemsAdded'`),
 * which is what the locale files key on. PascalCase keys are unusual for this codebase, and
 * deliberate: the alternative — the enum's numeric value — would give locale files full of
 * `notifications.types.13.title`, unreviewable and silently wrong the first time a number moved.
 */

/** i18n keys and interpolation values for one notification row. */
export interface NotificationTemplate {
  titleKey: string
  bodyKey: string
  /** Passed to `t(key, params)`. */
  params: Record<string, string>
  /** Lucide icon name for the row's leading glyph. */
  icon: string
}

const BASE = 'notifications.types'

/**
 * Icon per type. Grouped by area rather than one glyph per type: nineteen distinguishable icons
 * would be nineteen things to learn, where "this is about your shopping list" is the useful
 * distinction in a mixed list.
 */
const ICONS: Record<string, string> = {
  WeeklySummary: 'i-lucide-calendar-clock',

  ShoppingListItemsAdded: 'i-lucide-shopping-cart',
  ShoppingListItemsEdited: 'i-lucide-shopping-cart',
  ShoppingListItemsDeleted: 'i-lucide-shopping-cart',
  ShoppingListItemsPurchased: 'i-lucide-shopping-basket',
  ShoppingListCreated: 'i-lucide-list-plus',
  ShoppingListDeleted: 'i-lucide-list-x',

  InventoryItemsCreated: 'i-lucide-package-plus',
  InventoryItemsUpdated: 'i-lucide-package',
  InventoryItemsDeleted: 'i-lucide-package-minus',
  InventoryItemsConsumed: 'i-lucide-utensils',

  AutomationExecuted: 'i-lucide-workflow',
  AutomationReminder: 'i-lucide-bell-ring',
  AutomationAddedToShoppingList: 'i-lucide-workflow',
  LowStock: 'i-lucide-triangle-alert',

  FamilyJoinRequest: 'i-lucide-user-plus',
  FamilyJoinApproved: 'i-lucide-user-check',
  FamilyJoinDeclined: 'i-lucide-user-x',

  CalendarEventReminder: 'i-lucide-calendar-clock'
}

const FALLBACK_ICON = 'i-lucide-bell'

/**
 * The keys and params for `notification`.
 *
 * An unrecognised type falls back to `notifications.types.unknown`, which says that something
 * happened and when, without claiming to know what. That is reachable in normal operation, not
 * only in theory: an installed PWA keeps running the bundle it was installed with, so a client can
 * be older than the deploy that added a notification type — and the row it cannot name is still
 * one the user should be able to see the existence of, and mark read.
 */
export function notificationTemplate(
  notification: { type: string, parameters?: Record<string, string> | null }
): NotificationTemplate {
  const params = notification.parameters ?? {}
  const known = Object.prototype.hasOwnProperty.call(ICONS, notification.type)

  if (!known) {
    return {
      titleKey: `${BASE}.unknown.title`,
      bodyKey: `${BASE}.unknown.body`,
      params: {},
      icon: FALLBACK_ICON
    }
  }

  return {
    titleKey: `${BASE}.${notification.type}.title`,
    bodyKey: bodyKeyFor(notification.type, params),
    params,
    icon: ICONS[notification.type] ?? FALLBACK_ICON
  }
}

/**
 * Most types have one body template. The calendar reminder has four, because its wording branches
 * on whether the event is all-day and on whether the reminder is "at the start" or ahead of it —
 * the same branch the server makes when it words the push. Mirroring it here keeps the inbox row
 * and the push saying the same thing, and keeps all four phrasings in the locale files where a
 * translator can see them, instead of assembled from fragments in code.
 */
function bodyKeyFor(type: string, params: Record<string, string>): string {
  if (type !== 'CalendarEventReminder') return `${BASE}.${type}.body`

  const allDay = params.isAllDay === 'true'
  const atStart = (params.leadMinutes ?? '0') === '0'

  if (allDay) return atStart ? `${BASE}.CalendarEventReminder.bodyAllDayToday` : `${BASE}.CalendarEventReminder.bodyAllDayLead`
  return atStart ? `${BASE}.CalendarEventReminder.bodyNow` : `${BASE}.CalendarEventReminder.bodyLead`
}
