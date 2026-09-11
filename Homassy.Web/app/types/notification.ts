/**
 * Notification centre types (#116).
 *
 * A row carries its *type* and its parameters, never rendered text — the wording is composed at
 * read time from the `notifications.types.*` templates, so a user who switches language does not
 * find a month of another language's sentences in their inbox. See the API's
 * `Models/Notification/NotificationEnvelope` for the parameter names each type carries.
 */

/**
 * The `NotificationType` member's name, as the API sends it (e.g. `'ShoppingListItemsAdded'`).
 *
 * Deliberately a plain `string` rather than a union of the nineteen members: this value comes off
 * the wire, and an installed client can outlive a deploy that adds a type. A union would make an
 * unknown value a type error at the one place that has to handle it gracefully instead — see
 * `notificationTemplate`.
 */
export type NotificationTypeName = string

export interface NotificationInfo {
  publicId: string
  type: NotificationTypeName
  /** Values the localized template interpolates. Always present, possibly empty. */
  parameters: Record<string, string>
  /** App-relative path to open when the row is tapped, or null. */
  targetUrl: string | null
  /** When the notification was emitted, ISO-8601 (UTC). */
  createdAt: string
  isRead: boolean
}

export interface NotificationPage {
  items: NotificationInfo[]
  /** Cursor for the next page, or null at the end of the list. */
  nextCursor: string | null
  /** The whole inbox's unread count, not just this page's. */
  unreadCount: number
}

/** What every mutating notification endpoint answers with: the new badge number. */
export interface UnreadCountResponse {
  unreadCount: number
}
