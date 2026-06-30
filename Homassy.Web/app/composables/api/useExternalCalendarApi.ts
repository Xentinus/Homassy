import type {
  ExternalCalendarResponse,
  CreateExternalCalendarRequest,
  UpdateExternalCalendarRequest
} from '~/types/externalCalendar'

export const useExternalCalendarApi = () => {
  const client = useApiClient()

  const getExternalCalendars = async () => {
    return await client.get<ExternalCalendarResponse[]>('/api/v1/ExternalCalendar', {
      showErrorToast: false
    })
  }

  const createExternalCalendar = async (request: CreateExternalCalendarRequest) => {
    return await client.post<ExternalCalendarResponse>('/api/v1/ExternalCalendar', request)
  }

  const updateExternalCalendar = async (publicId: string, request: UpdateExternalCalendarRequest) => {
    return await client.put<ExternalCalendarResponse>(`/api/v1/ExternalCalendar/${publicId}`, request)
  }

  const deleteExternalCalendar = async (publicId: string) => {
    return await client.delete(`/api/v1/ExternalCalendar/${publicId}`)
  }

  const syncExternalCalendar = async (publicId: string) => {
    return await client.post<ExternalCalendarResponse>(
      `/api/v1/ExternalCalendar/${publicId}/sync`,
      undefined
    )
  }

  return {
    getExternalCalendars,
    createExternalCalendar,
    updateExternalCalendar,
    deleteExternalCalendar,
    syncExternalCalendar
  }
}
