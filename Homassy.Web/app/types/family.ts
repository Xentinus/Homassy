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

export type FamilyJoinRequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled'

/** The current user's own pending request to join a family. */
export interface MyJoinRequestResponse {
  publicId: string
  familyName: string
  status: FamilyJoinRequestStatus
  requestedAt: string
}

/** An incoming request to join the current user's family, shown to existing members. */
export interface FamilyJoinRequestResponse {
  publicId: string
  name: string
  displayName: string
  profilePictureBase64?: string
  requestedAt: string
}
