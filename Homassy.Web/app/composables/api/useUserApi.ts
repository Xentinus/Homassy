/**
 * User API composable
 * Provides user profile and settings-related API calls
 */
import type {
  UserProfileResponse,
  UpdateUserSettingsRequest,
  UploadUserProfileImageRequest
} from '~/types/api'

export const useUserApi = () => {
  const client = useApiClient()

  /**
   * Get current user profile
   */
  const getUserProfile = async () => {
    return await client.get<UserProfileResponse>('/api/v1/User/profile')
  }

  /**
   * Update user settings
   */
  const updateUserSettings = async (settings: UpdateUserSettingsRequest) => {
    return await client.put<UserProfileResponse>(
      '/api/v1/User/settings',
      settings,
      {
        showSuccessToast: true,
        successMessage: 'Settings updated successfully'
      }
    )
  }

  /**
   * Upload user profile picture
   */
  const uploadProfilePicture = async (request: UploadUserProfileImageRequest) => {
    return await client.post(
      '/api/v1/User/profile-picture',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Profile picture uploaded successfully'
      }
    )
  }

  /**
   * Delete user profile picture
   */
  const deleteProfilePicture = async () => {
    return await client.delete(
      '/api/v1/User/profile-picture',
      {
        showSuccessToast: true,
        successMessage: 'Profile picture deleted successfully'
      }
    )
  }

  return {
    getUserProfile,
    updateUserSettings,
    uploadProfilePicture,
    deleteProfilePicture
  }
}
