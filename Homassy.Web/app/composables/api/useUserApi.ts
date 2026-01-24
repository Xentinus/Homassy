/**
 * User API composable
 * Provides user profile and settings-related API calls
 */
import type {
  UserProfileResponse,
  UpdateUserSettingsRequest,
  UploadUserProfileImageRequest,
  NotificationPreferencesResponse,
  UpdateNotificationPreferencesRequest,
  UserInfo
} from '~/types/user'
import type {
  ActivityInfo,
  GetActivitiesRequest,
  PagedActivitiesResponse
} from '~/types/activity'

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
   * Upload user profile picture (synchronous - legacy)
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
   * Upload user profile picture with progress tracking
   */
  const uploadProfilePictureWithProgress = async (request: UploadUserProfileImageRequest) => {
    return await client.post<{ jobId: string }>(
      '/api/v1/User/profile-picture/upload-async',
      request,
      {
        showSuccessToast: false,
        showErrorToast: false
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

  /**
   * Send test push notification
   */
  const sendTestPushNotification = async () => {
    return await client.post('/api/v1/User/push/test', {}, { showErrorToast: false })
  }

  /**
   * Get multiple users by their public IDs
   * @param publicIds - Array of user public IDs (GUIDs)
   * @returns List of UserInfo objects
   */
  const getUsersByPublicIds = async (publicIds: string[]) => {
    if (!publicIds || publicIds.length === 0) {
      return []
    }

    if (publicIds.length > 100) {
      throw new Error('Maximum 100 user IDs allowed per request')
    }

    // Join publicIds with comma
    const publicIdsParam = publicIds.join(',')

    return await client.get<UserInfo[]>(`/api/v1/User/bulk?publicIds=${publicIdsParam}`)
  }

  /**
   * Get paginated activity history with optional filtering
   * @param params - Activity filter parameters
   * @returns Paginated list of activities
   */
  const getActivities = async (params?: GetActivitiesRequest) => {
    const queryParams = new URLSearchParams()

    if (params?.activityType !== undefined) {
      queryParams.append('activityType', params.activityType.toString())
    }
    if (params?.startDate) {
      queryParams.append('startDate', params.startDate)
    }
    if (params?.endDate) {
      queryParams.append('endDate', params.endDate)
    }
    if (params?.userPublicId !== undefined) {
      queryParams.append('userPublicId', params.userPublicId)
    }
    if (params?.pageNumber !== undefined) {
      queryParams.append('pageNumber', params.pageNumber.toString())
    }
    if (params?.pageSize !== undefined) {
      queryParams.append('pageSize', params.pageSize.toString())
    }
    if (params?.returnAll !== undefined) {
      queryParams.append('returnAll', params.returnAll.toString())
    }

    const queryString = queryParams.toString()
    const url = queryString ? `/api/v1/User/activities?${queryString}` : '/api/v1/User/activities'

    return await client.get<PagedActivitiesResponse>(url)
  }

  return {
    getUserProfile,
    updateUserSettings,
    uploadProfilePicture,
    uploadProfilePictureWithProgress,
    deleteProfilePicture,
    getNotificationPreferences,
    updateNotificationPreferences,
    sendTestPushNotification,
    getUsersByPublicIds,
    getActivities
  }
}
