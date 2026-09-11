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
  ActivityTimelineResult,
  GetActivitiesRequest,
  PagedActivitiesResponse
} from '~/types/activity'

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
      settings
    )
  }

  /**
   * Upload user profile picture (synchronous - legacy)
   */
  const uploadProfilePicture = async (request: UploadUserProfileImageRequest) => {
    return await client.post(
      '/api/v1/User/profile-picture',
      request
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
      '/api/v1/User/profile-picture'
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
    // No generic: the endpoint answers with an envelope carrying no data, and
    // `void` is not valid as a value type here.
    return await client.put(
      '/api/v1/User/notification',
      preferences
    )
  }

  /**
   * Record that the first-run tour has been shown to this user (#98) — written when the
   * tour starts, not when it ends, so abandoning it halfway does not leave it ambushing
   * the user on every launch. `useOnboardingTour` explains why.
   *
   * `false` clears the flag and arms auto-start again. Nothing in the app sends it today:
   * "Replay the tour" runs the tour without re-arming it, which is what the user asked
   * for by tapping the row. The endpoint keeps the capability.
   *
   * The flag is *read* back on `GET /auth/me`, not here: the client needs it at boot,
   * which is a payload it already fetches.
   *
   * No error toast. The tour is on screen either way by the time this is called, and the
   * local echo in `useOnboardingTour` keeps it from reopening on this device, so a failed
   * write is not something the user can act on.
   */
  const updateOnboarding = async (completed: boolean) => {
    return await client.put('/api/v1/User/onboarding', { completed }, { showErrorToast: false })
  }

  /**
   * Send test push notification
   */
  const sendTestPushNotification = async () => {
    return await client.post('/api/v1/User/push/test', {}, { showErrorToast: false })
  }

  /**
   * Send test email summary
   */
  const sendTestEmailSummary = async () => {
    return await client.post('/api/v1/User/email/test', {}, { showErrorToast: false })
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

  /**
   * Get one page of the cursor-paged, server-aggregated activity timeline: same-actor,
   * same-type activities recorded close together collapse into a single entry (see
   * `ActivityTimelineEntry`), and paging follows an opaque cursor rather than a page number.
   * @param params - Cursor from the previous page (omit for the first page), filters and page size
   * @returns One page of timeline entries, newest first, plus the next page's cursor
   */
  const getActivityTimeline = async (params?: {
    cursor?: string
    pageSize?: number
    activityType?: number
    userPublicId?: string
    /**
     * Optional window (#127), both ISO-8601: `since` exclusive, `until` inclusive. The away-delta
     * card links here with the exact window its count was computed over, so the list cannot
     * disagree with the number that was tapped.
     */
    since?: string
    until?: string
  }) => {
    const queryParams = new URLSearchParams()

    if (params?.cursor !== undefined) {
      queryParams.append('cursor', params.cursor)
    }
    if (params?.pageSize !== undefined) {
      queryParams.append('pageSize', params.pageSize.toString())
    }
    if (params?.activityType !== undefined) {
      queryParams.append('activityType', params.activityType.toString())
    }
    if (params?.userPublicId !== undefined) {
      queryParams.append('userPublicId', params.userPublicId)
    }
    if (params?.since !== undefined) {
      queryParams.append('since', params.since)
    }
    if (params?.until !== undefined) {
      queryParams.append('until', params.until)
    }

    const queryString = queryParams.toString()
    const url = queryString ? `/api/v1/User/activities/timeline?${queryString}` : '/api/v1/User/activities/timeline'

    return await client.get<ActivityTimelineResult>(url)
  }

  return {
    getUserProfile,
    updateUserSettings,
    uploadProfilePicture,
    uploadProfilePictureWithProgress,
    deleteProfilePicture,
    getNotificationPreferences,
    updateNotificationPreferences,
    updateOnboarding,
    sendTestPushNotification,
    sendTestEmailSummary,
    getUsersByPublicIds,
    getActivities,
    getActivityTimeline
  }
}
