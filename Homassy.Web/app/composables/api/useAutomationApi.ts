/**
 * Automation API composable
 * Provides automation rule management API calls
 */
import type {
  AutomationResponse,
  AutomationExecutionResponse,
  CreateAutomationRequest,
  UpdateAutomationRequest,
  ExecuteAutomationRequest
} from '~/types/automation'

export const useAutomationApi = () => {
  const client = useApiClient()

  /**
   * Get all automation rules for the current user/family
   */
  const getAutomations = async () => {
    return await client.get<AutomationResponse[]>('/api/v1/Automation')
  }

  /**
   * Get a single automation rule by publicId
   */
  const getAutomation = async (publicId: string) => {
    return await client.get<AutomationResponse>(`/api/v1/Automation/${publicId}`)
  }

  /**
   * Create a new automation rule
   */
  const createAutomation = async (request: CreateAutomationRequest) => {
    return await client.post<AutomationResponse>('/api/v1/Automation', request)
  }

  /**
   * Update an existing automation rule
   */
  const updateAutomation = async (publicId: string, request: UpdateAutomationRequest) => {
    return await client.put<AutomationResponse>(`/api/v1/Automation/${publicId}`, request)
  }

  /**
   * Delete an automation rule (soft delete)
   */
  const deleteAutomation = async (publicId: string) => {
    return await client.delete(`/api/v1/Automation/${publicId}`)
  }

  /**
   * Manually execute an automation rule
   */
  const executeAutomation = async (publicId: string, request: ExecuteAutomationRequest) => {
    return await client.post<AutomationExecutionResponse>(`/api/v1/Automation/${publicId}/execute`, request, { showErrorToast: false })
  }

  /**
   * Get execution history for an automation rule
   */
  const getExecutionHistory = async (publicId: string) => {
    return await client.get<AutomationExecutionResponse[]>(`/api/v1/Automation/${publicId}/history`)
  }

  return {
    getAutomations,
    getAutomation,
    createAutomation,
    updateAutomation,
    deleteAutomation,
    executeAutomation,
    getExecutionHistory
  }
}
