/**
 * User API composable
 * Provides user profile and settings-related API calls
 */
import type {
  UserProfileResponse,
  UpdateUserSettingsRequest,
  UploadUserProfileImageRequest,
  NotificationPreferencesResponse,
  UpdateNotificationPreferencesRequest
} from '~/types/user'

export const useUserApi = () => {
  const client = useApiClient()
  const { $i18n } = useNuxtApp()

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
        successMessage: $i18n.t('toast.settingsUpdatedSuccess')
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
        successMessage: $i18n.t('toast.profilePictureUploaded')
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
        successMessage: $i18n.t('toast.profilePictureDeleted')
      }
    )
  }

  /**
   * Get notification preferences
   */
  const getNotificationPreferences = async () => {
    return await client.get<NotificationPreferencesResponse>('/api/v1/User/notification')
  }

  /**
   * Update notification preferences
   */
  const updateNotificationPreferences = async (preferences: UpdateNotificationPreferencesRequest) => {
    return await client.put<void>(
      '/api/v1/User/notification',
      preferences,
      {
        showSuccessToast: true,
        successMessage: $i18n.t('toast.notificationPreferencesUpdated')
      }
    )
  }

  return {
    getUserProfile,
    updateUserSettings,
    uploadProfilePicture,
    deleteProfilePicture,
    getNotificationPreferences,
    updateNotificationPreferences
  }
}
