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
  FamilyMemberResponse
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
   * Join existing family
   */
  const joinFamily = async (request: JoinFamilyRequest) => {
    return await client.post<FamilyDetailsResponse>(
      '/api/v1/Family/join',
      request
    )
  }

  /**
   * Leave current family
   */
  const leaveFamily = async () => {
    return await client.post(
      '/api/v1/Family/leave',
      undefined
    )
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
    joinFamily,
    leaveFamily,
    uploadFamilyPicture,
    deleteFamilyPicture
  }
}
