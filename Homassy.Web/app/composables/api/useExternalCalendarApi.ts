import type {
  ExternalCalendarResponse,
  CreateExternalCalendarRequest,
  UpdateExternalCalendarRequest
} from '~/types/externalCalendar'
import type { ApiCallOptions } from '../useApiClient'

export const useExternalCalendarApi = () => {
  const client = useApiClient()

  const getExternalCalendars = async () => {
    return await client.get<ExternalCalendarResponse[]>('/api/v1/ExternalCalendar', {
      showErrorToast: false
    })
  }

  const createExternalCalendar = async (request: CreateExternalCalendarRequest, options?: ApiCallOptions) => {
    return await client.post<ExternalCalendarResponse>('/api/v1/ExternalCalendar', request, options)
  }

  const updateExternalCalendar = async (publicId: string, request: UpdateExternalCalendarRequest, options?: ApiCallOptions) => {
    return await client.put<ExternalCalendarResponse>(`/api/v1/ExternalCalendar/${publicId}`, request, options)
  }

  const deleteExternalCalendar = async (publicId: string) => {
    return await client.delete(`/api/v1/ExternalCalendar/${publicId}`)
  }

  const syncExternalCalendar = async (publicId: string, options?: ApiCallOptions) => {
    return await client.post<ExternalCalendarResponse>(
      `/api/v1/ExternalCalendar/${publicId}/sync`,
      undefined,
      options
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
