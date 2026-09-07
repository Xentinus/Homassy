/**
 * Event bus composable using mitt for global event management
 * Provides type-safe event emission and subscription for cross-component communication
 */
import mitt from 'mitt'

/**
 * All event types for inventory and product mutations. Every one of them is a
 * bare signal with no payload — subscribers refetch or invalidate rather than
 * reading anything off the event.
 *
 * `undefined` rather than `void` for the payload: mitt has an `emit` overload
 * keyed on `undefined extends Events[Key]`, so `emit('inventory:created')` with
 * no second argument stays legal, and `undefined` is a value type where `void`
 * is only valid as a return type.
 */
type EventBusEvents = {
  'inventory:created': undefined
  'inventory:updated': undefined
  'inventory:deleted': undefined
  'inventory:consumed': undefined
  'inventory:split': undefined
  'inventory:moved': undefined
  'product:deleted': undefined
  'shopping-list-item:created': undefined
  'shopping-list-item:updated': undefined
  'shopping-list-item:deleted': undefined
  'shopping-list-item:purchased': undefined
  'shopping-list-item:restored': undefined
}

// Create a singleton event bus instance
const emitter = mitt<EventBusEvents>()

/**
 * Composable that provides access to the global event bus
 * @returns {Object} Event bus methods (on, off, emit)
 */
export const useEventBus = () => {
  return {
    /**
     * Subscribe to an event
     * @param event - Event name to listen to
     * @param handler - Callback function when event is emitted
     */
    on: emitter.on,

    /**
     * Unsubscribe from an event
     * @param event - Event name to stop listening to
     * @param handler - Callback function to remove
     */
    off: emitter.off,

    /**
     * Emit an event
     * @param event - Event name to emit
     */
    emit: emitter.emit
  }
}
