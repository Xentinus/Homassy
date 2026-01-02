/**
 * User related types
 */
import type { Language, Currency, UserTimeZone, ImageFormat } from './enums'

export interface UserProfileResponse {
  publicId: string
  email: string
  name: string
  displayName?: string
  profilePictureBase64?: string
  language: string
  currency: string
  timeZone: string
  emailVerifiedAt?: string
  createdAt: string
  updatedAt: string
}

export interface UpdateUserSettingsRequest {
  email?: string
  name?: string
  displayName?: string
  defaultLanguage?: Language
  defaultCurrency?: Currency
  defaultTimeZone?: UserTimeZone
}

export interface UploadUserProfileImageRequest {
  imageBase64: string
}

export interface UserProfileImageInfo {
  imageBase64: string
  format: ImageFormat
  width: number
  height: number
  fileSizeBytes: number
}

export interface NotificationPreferencesResponse {
  emailNotificationsEnabled: boolean
  emailWeeklySummaryEnabled: boolean
  pushNotificationsEnabled: boolean
  pushWeeklySummaryEnabled: boolean
  inAppNotificationsEnabled: boolean
}

export interface UpdateNotificationPreferencesRequest {
  emailNotificationsEnabled?: boolean
  emailWeeklySummaryEnabled?: boolean
  pushNotificationsEnabled?: boolean
  pushWeeklySummaryEnabled?: boolean
  inAppNotificationsEnabled?: boolean
}

/**
 * Simple user info for bulk retrieval
 */
export interface UserInfo {
  name: string
  displayName: string
  profilePictureBase64?: string
  timeZone: string
  language: string
  currency: string
}
