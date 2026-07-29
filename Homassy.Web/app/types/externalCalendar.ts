export interface ExternalCalendarResponse {
  publicId: string
  name: string
  iCalUrl: string
  color: string
  isEnabled: boolean
  lastSyncedAt: string | null
  lastSyncError: string | null
  eventCount: number
  /** Minutes before an event starts; 0 means "at start". Empty = reminders off. */
  reminderLeadTimes: number[]
  /** `HH:mm` an all-day event's reminder is anchored to, in each member's own timezone. */
  allDayNotifyTime: string
}

export interface CreateExternalCalendarRequest {
  name: string
  iCalUrl: string
  color: string
  reminderLeadTimes?: number[]
  allDayNotifyTime?: string
}

export interface UpdateExternalCalendarRequest {
  name?: string
  iCalUrl?: string
  color?: string
  isEnabled?: boolean
  /** Omit to leave reminders unchanged; send an empty array to turn them off. */
  reminderLeadTimes?: number[]
  allDayNotifyTime?: string
}
