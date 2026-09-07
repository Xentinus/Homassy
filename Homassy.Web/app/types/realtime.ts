/**
 * Shared types for the SignalR hub clients (useInventorySocket,
 * useShoppingListSocket, useMasterDataSocket).
 */
import type { HubConnection } from '@microsoft/signalr'

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
