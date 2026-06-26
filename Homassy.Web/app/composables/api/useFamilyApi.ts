/**
 * Family API composable
 * Provides family-related API calls
 */
import type {
  FamilyDetailsResponse,
  CreateFamilyRequest,
  UpdateFamilyRequest,
  JoinFamilyRequest,
  UploadFamilyPictureRequest,
  FamilyMemberResponse,
  MyJoinRequestResponse,
  FamilyJoinRequestResponse
} from '~/types/family'

export const useFamilyApi = () => {
  const client = useApiClient()
  const $i18n = useI18n()

  /**
   * Get current family information
   */
  const getFamily = async () => {
    return await client.get<FamilyDetailsResponse>('/api/v1/Family', {
      showErrorToast: false
    })
  }

  /**
   * Get all family members
   */
  const getFamilyMembers = async () => {
    return await client.get<FamilyMemberResponse[]>('/api/v1/Family/members', {
      showErrorToast: false
    })
  }

  /**
   * Create new family
   */
  const createFamily = async (family: CreateFamilyRequest) => {
    return await client.post<FamilyDetailsResponse>(
      '/api/v1/Family/create',
      family
    )
  }

  /**
   * Update family information
   */
  const updateFamily = async (family: UpdateFamilyRequest) => {
    return await client.put<FamilyDetailsResponse>(
      '/api/v1/Family',
      family
    )
  }

  /**
   * Request to join an existing family (requires approval from a member)
   */
  const requestJoin = async (request: JoinFamilyRequest) => {
    return await client.post<MyJoinRequestResponse>(
      '/api/v1/Family/join-requests',
      request
    )
  }

  /**
   * Get the current user's pending join request (null if none)
   */
  const getMyJoinRequest = async () => {
    return await client.get<MyJoinRequestResponse | null>(
      '/api/v1/Family/join-requests/mine',
      { showErrorToast: false }
    )
  }

  /**
   * Withdraw the current user's pending join request
   */
  const cancelMyJoinRequest = async () => {
    return await client.delete('/api/v1/Family/join-requests/mine')
  }

  /**
   * List pending join requests for the current user's family
   */
  const getJoinRequests = async () => {
    return await client.get<FamilyJoinRequestResponse[]>(
      '/api/v1/Family/join-requests',
      { showErrorToast: false }
    )
  }

  /**
   * Approve a pending join request
   */
  const approveJoinRequest = async (publicId: string) => {
    return await client.post(
      `/api/v1/Family/join-requests/${publicId}/approve`,
      undefined
    )
  }

  /**
   * Decline a pending join request
   */
  const rejectJoinRequest = async (publicId: string) => {
    return await client.post(
      `/api/v1/Family/join-requests/${publicId}/reject`,
      undefined
    )
  }

  /**
   * Leave current family
   */
  const leaveFamily = async () => {
    const response = await client.post(
      '/api/v1/Family/leave',
      undefined
    )
    if (!response.success || response.errorCodes?.length) {
      throw new Error(response.errorCodes?.[0] || 'Failed to leave family')
    }
    return response
  }

  /**
   * Upload family picture
   */
  const uploadFamilyPicture = async (request: UploadFamilyPictureRequest) => {
    return await client.post(
      '/api/v1/Family/picture',
      request
    )
  }

  /**
   * Delete family picture
   */
  const deleteFamilyPicture = async () => {
    return await client.delete(
      '/api/v1/Family/picture'
    )
  }

  return {
    getFamily,
    getFamilyMembers,
    createFamily,
    updateFamily,
    requestJoin,
    getMyJoinRequest,
    cancelMyJoinRequest,
    getJoinRequests,
    approveJoinRequest,
    rejectJoinRequest,
    leaveFamily,
    uploadFamilyPicture,
    deleteFamilyPicture
  }
}
