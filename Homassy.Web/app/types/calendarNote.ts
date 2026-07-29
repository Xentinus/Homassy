export interface CalendarNoteResponse {
  publicId: string
  /** `YYYY-MM-DD` — the day the note is about. Never round-trip this through `Date`/`toISOString()`. */
  date: string
  title: string
  content: string | null
  /** Absolute instant the reminder fires, derived server-side. Read-only. */
  reminderAt: string | null
  /** `HH:mm` the author picked — what the edit form and the card should show. Null = no reminder. */
  reminderTime: string | null
  /** The `UserTimeZone` enum value `reminderTime` is anchored in. */
  reminderTimeZone: number | null
  reminderSentAt: string | null
  authorPublicId: string
  authorName: string
  lastEditedByPublicId: string
  lastEditedByName: string
  createdAt: string
  lastEditedAt: string | null
  /** Opaque concurrency token; echo it back on update or the save is rejected with 409. */
  version: string
}

export interface CreateCalendarNoteRequest {
  date: string
  title: string
  content?: string | null
  reminderTime?: string | null
}

export interface UpdateCalendarNoteRequest {
  date?: string
  title?: string
  /** `''` erases the body; omitting the key leaves it unchanged. */
  content?: string | null
  /** Leaves the reminder unchanged when omitted — use `clearReminder` to remove it. */
  reminderTime?: string | null
  clearReminder?: boolean
  version: string
}
