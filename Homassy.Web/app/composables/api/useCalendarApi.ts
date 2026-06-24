import type { CalendarEventInfo } from '~/types/calendar'

export const useCalendarApi = () => {
  const client = useApiClient()

  const getCalendarEvents = async (startDate: string, endDate: string) => {
    return client.post<CalendarEventInfo[]>('/api/v1/Calendar', { startDate, endDate })
  }

  return { getCalendarEvents }
}
