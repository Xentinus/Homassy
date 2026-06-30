export interface ExternalCalendarResponse {
  publicId: string
  name: string
  iCalUrl: string
  color: string
  isEnabled: boolean
  lastSyncedAt: string | null
  lastSyncError: string | null
  eventCount: number
}

export interface CreateExternalCalendarRequest {
  name: string
  iCalUrl: string
  color: string
}

export interface UpdateExternalCalendarRequest {
  name?: string
  iCalUrl?: string
  color?: string
  isEnabled?: boolean
}
