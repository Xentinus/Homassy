/**
 * Shared types for the SignalR hub clients (useInventorySocket,
 * useShoppingListSocket, useMasterDataSocket).
 */
import type { HubConnection } from '@microsoft/signalr'
import type { ShoppingListItemInfo } from './shoppingList'

/**
 * A hub event handler, where the payload shape is the caller's to declare.
 *
 * `never[]` rather than `unknown[]` on purpose. Parameters are contravariant, so
 * `(payload: InventoryUpsertedEvent) => void` is assignable to a `never[]` rest
 * signature but *not* to an `unknown[]` one — and the whole point of these
 * subscriptions is that each caller passes a handler typed for the event it
 * subscribed to. It also means the hub clients need no `any`.
 */
export type HubEventHandler = (...args: never[]) => void

/**
 * What `HubConnection.on` / `.off` accept: `(...args: any[]) => void`. The two
 * variances meet here and nowhere else, so the hub clients cast once at this
 * boundary rather than typing their own callers loosely.
 */
export type SignalRHandler = Parameters<HubConnection['on']>[1]

/**
 * One member currently present on a shared shopping list — the `PresenceChanged` event's payload
 * is the list's full member snapshot (see `Homassy.API.Models.ShoppingList.PresenceMemberInfo`,
 * serialised camelCase). `useShoppingListSocket`'s `presentMembers` filters this down to exclude
 * the caller, so consumers only ever see who *else* is here.
 */
export interface PresenceMember {
  publicId: string
  displayName: string
  profilePictureUrl?: string | null
  identityColor?: string | null
  /** How many of this member's connections currently have the list open. */
  deviceCount: number
}

/**
 * `ItemUpserted` payload (`Homassy.API.Hubs.ShoppingListRealtime.ItemUpsertedAsync`). Replaces the
 * bare `ShoppingListItemInfo` the client used to receive: `actorPublicId` is who made the change,
 * null for the rare paths with no acting session to attribute it to, so a viewer can flash
 * "changed by" without a separate fetch.
 */
export interface ItemUpsertedEvent {
  item: ShoppingListItemInfo
  actorPublicId?: string | null
}

/** `ItemDeleted` payload (`Homassy.API.Hubs.ShoppingListRealtime.ItemDeletedAsync`). */
export interface ItemDeletedEvent {
  publicId: string
  shoppingListPublicId: string
  actorPublicId?: string | null
}
