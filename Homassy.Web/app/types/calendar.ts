export enum CalendarEventType {
  InventoryExpiration = 0,
  AutomationExecution = 1,
  ShoppingListDeadline = 2,
  ExternalCalendar = 3,
  /** A family day note (#60). */
  DayNote = 4
}

export interface CalendarEventInfo {
  publicId: string
  title: string
  eventType: CalendarEventType
  start: string
  end: string | null
  detail: string | null
  relatedEntityPublicId: string | null
  color: string | null
  isAllDay: boolean
}

/**
 * A day note on the family calendar (#60).
 *
 * Notes also arrive inside the aggregated event list as `CalendarEventType.DayNote`; this is the
 * full shape the note panel edits, which the calendar projection deliberately does not carry.
 */
export interface CalendarNoteInfo {
  publicId: string
  /** The day the note is about, `YYYY-MM-DD`. A calendar day, never an instant. */
  date: string
  title: string
  content: string | null
  /** When the family is reminded, ISO UTC, or null for a note with no reminder. */
  reminderAt: string | null
  /** True once the reminder has gone out. */
  reminderSent: boolean
  createdByPublicId: string
  createdByName: string
  lastEditedByPublicId: string | null
  lastEditedByName: string | null
  createdAt: string
}

export interface CreateCalendarNoteRequest {
  date: string
  title: string
  content?: string | null
  reminderAt?: string | null
}

export interface UpdateCalendarNoteRequest {
  date: string
  title: string
  content?: string | null
  reminderAt?: string | null
}
