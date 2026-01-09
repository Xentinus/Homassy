/**
 * Family related types
 */

export interface FamilyInfo {
  name: string
  shareCode: string
}

export interface FamilyDetailsResponse {
  name: string
  description?: string
  shareCode: string
  familyPictureBase64?: string
}

export interface CreateFamilyRequest {
  name: string
  description?: string
  familyPictureBase64?: string
}

export interface UpdateFamilyRequest {
  name?: string
  description?: string
}

export interface JoinFamilyRequest {
  shareCode: string
}

export interface UploadFamilyPictureRequest {
  familyPictureBase64: string
}

export interface FamilyMemberResponse {
  publicId: string
  name: string
  displayName: string
  lastLoginAt: string
  profilePictureBase64?: string
  isCurrentUser: boolean
}
