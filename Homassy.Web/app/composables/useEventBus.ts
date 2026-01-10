/**
 * Event bus composable using mitt for global event management
 * Provides type-safe event emission and subscription for cross-component communication
 */
import mitt from 'mitt'

// Define all event types for inventory and product mutations
type EventBusEvents = {
  'inventory:created': void
  'inventory:updated': void
  'inventory:deleted': void
  'inventory:consumed': void
  'inventory:split': void
  'inventory:moved': void
  'product:deleted': void
  'shopping-list-item:created': void
  'shopping-list-item:updated': void
  'shopping-list-item:deleted': void
  'shopping-list-item:purchased': void
  'shopping-list-item:restored': void
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
