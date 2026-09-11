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
  /** Versioned path to the family picture, or null/absent when it has none. Never bytes. */
  familyPictureUrl?: string | null
}

export interface CreateFamilyRequest {
  name: string
  description?: string
}

export interface UpdateFamilyRequest {
  name?: string
  description?: string
}

export interface JoinFamilyRequest {
  shareCode: string
}

export interface UploadFamilyPictureRequest {
  imageBase64: string
}

/** What the upload answers with: the new picture already addressed by its own version. */
export interface FamilyImageInfo {
  familyPictureUrl: string
  format: number
  width: number
  height: number
  fileSizeBytes: number
}

export interface FamilyMemberResponse {
  publicId: string
  name: string
  displayName: string
  lastLoginAt: string
  profilePictureUrl?: string
  isCurrentUser: boolean
  /** Chosen identity-colour palette key, or null/absent for the deterministic pick. */
  identityColor?: string | null
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
  profilePictureUrl?: string
  requestedAt: string
}
