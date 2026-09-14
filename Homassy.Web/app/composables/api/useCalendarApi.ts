import type { CalendarEventInfo, CalendarNoteInfo, CreateCalendarNoteRequest, UpdateCalendarNoteRequest } from '~/types/calendar'
import type { ApiCallOptions } from '../useApiClient'

export const useCalendarApi = () => {
  const client = useApiClient()

  const getCalendarEvents = async (startDate: string, endDate: string) => {
    return client.post<CalendarEventInfo[]>('/api/v1/Calendar', { startDate, endDate })
  }

  /**
   * The family's day notes in a date range (#60).
   *
   * Notes also arrive inside `getCalendarEvents`, which is what paints the day cells. This call is
   * what the day panel reads, because editing a note needs its text, its reminder and its author —
   * none of which fit the calendar projection.
   */
  const getCalendarNotes = async (startDate: string, endDate: string) => {
    return client.post<CalendarNoteInfo[]>('/api/v1/Calendar/notes/range', { startDate, endDate })
  }

  const createCalendarNote = async (request: CreateCalendarNoteRequest, options?: ApiCallOptions) => {
    return client.post<CalendarNoteInfo>('/api/v1/Calendar/notes', request, options)
  }

  const updateCalendarNote = async (publicId: string, request: UpdateCalendarNoteRequest, options?: ApiCallOptions) => {
    return client.put<CalendarNoteInfo>(`/api/v1/Calendar/notes/${publicId}`, request, options)
  }

  const deleteCalendarNote = async (publicId: string, options?: ApiCallOptions) => {
    return client.delete(`/api/v1/Calendar/notes/${publicId}`, undefined, options)
  }

  return {
    getCalendarEvents,
    getCalendarNotes,
    createCalendarNote,
    updateCalendarNote,
    deleteCalendarNote
  }
}
