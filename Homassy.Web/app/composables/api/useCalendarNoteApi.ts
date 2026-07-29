import type {
  CalendarNoteResponse,
  CreateCalendarNoteRequest,
  UpdateCalendarNoteRequest
} from '~/types/calendarNote'

export const useCalendarNoteApi = () => {
  const client = useApiClient()

  /**
   * Notes for a date range. Error toasts are suppressed because this is one of several parallel week loads on
   * the calendar page — a failure must not toast on every week navigation.
   */
  const getCalendarNotes = async (startDate: string, endDate: string) => {
    return await client.get<CalendarNoteResponse[]>(
      `/api/v1/CalendarNote?startDate=${startDate}&endDate=${endDate}`,
      { showErrorToast: false }
    )
  }

  const createCalendarNote = async (request: CreateCalendarNoteRequest) => {
    return await client.post<CalendarNoteResponse>('/api/v1/CalendarNote', request)
  }

  const updateCalendarNote = async (publicId: string, request: UpdateCalendarNoteRequest) => {
    return await client.put<CalendarNoteResponse>(`/api/v1/CalendarNote/${publicId}`, request)
  }

  const deleteCalendarNote = async (publicId: string) => {
    return await client.delete(`/api/v1/CalendarNote/${publicId}`)
  }

  return {
    getCalendarNotes,
    createCalendarNote,
    updateCalendarNote,
    deleteCalendarNote
  }
}
