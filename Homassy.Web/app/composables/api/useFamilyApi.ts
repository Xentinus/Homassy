/**
 * Family API composable
 * Provides family-related API calls
 */
import type {
  FamilyDetailsResponse,
  CreateFamilyRequest,
  UpdateFamilyRequest,
  JoinFamilyRequest,
  UploadFamilyPictureRequest
} from '~/types/api'

export const useFamilyApi = () => {
  const client = useApiClient()

  /**
   * Get current family information
   */
  const getFamily = async () => {
    return await client.get<FamilyDetailsResponse>('/api/v1/Family')
  }

  /**
   * Create new family
   */
  const createFamily = async (family: CreateFamilyRequest) => {
    return await client.post<FamilyDetailsResponse>(
      '/api/v1/Family/create',
      family,
      {
        showSuccessToast: true,
        successMessage: 'Family created successfully'
      }
    )
  }

  /**
   * Update family information
   */
  const updateFamily = async (family: UpdateFamilyRequest) => {
    return await client.put<FamilyDetailsResponse>(
      '/api/v1/Family',
      family,
      {
        showSuccessToast: true,
        successMessage: 'Family updated successfully'
      }
    )
  }

  /**
   * Join existing family
   */
  const joinFamily = async (request: JoinFamilyRequest) => {
    return await client.post<FamilyDetailsResponse>(
      '/api/v1/Family/join',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Joined family successfully'
      }
    )
  }

  /**
   * Leave current family
   */
  const leaveFamily = async () => {
    return await client.post(
      '/api/v1/Family/leave',
      undefined,
      {
        showSuccessToast: true,
        successMessage: 'Left family successfully'
      }
    )
  }

  /**
   * Upload family picture
   */
  const uploadFamilyPicture = async (request: UploadFamilyPictureRequest) => {
    return await client.post(
      '/api/v1/Family/picture',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Family picture uploaded successfully'
      }
    )
  }

  /**
   * Delete family picture
   */
  const deleteFamilyPicture = async () => {
    return await client.delete(
      '/api/v1/Family/picture',
      {
        showSuccessToast: true,
        successMessage: 'Family picture deleted successfully'
      }
    )
  }

  return {
    getFamily,
    createFamily,
    updateFamily,
    joinFamily,
    leaveFamily,
    uploadFamilyPicture,
    deleteFamilyPicture
  }
}
