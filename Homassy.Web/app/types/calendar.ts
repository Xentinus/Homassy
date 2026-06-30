export enum CalendarEventType {
  InventoryExpiration = 0,
  AutomationExecution = 1,
  ShoppingListDeadline = 2,
  ExternalCalendar = 3
}

export interface CalendarEventInfo {
  publicId: string
  title: string
  eventType: CalendarEventType
  start: string
  detail: string | null
  relatedEntityPublicId: string | null
  color: string | null
}
