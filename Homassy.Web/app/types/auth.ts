/**
 * Authentication related types
 */

// Legacy types (kept for backward compatibility during migration)
export interface LoginRequest {
  email: string
}

export interface VerifyLoginRequest {
  verificationCode: string
  email: string
}

export interface RefreshTokenRequest {
  accessToken: string
  refreshToken: string
}

export interface CreateUserRequest {
  email: string
  name: string
  displayName?: string
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  refreshTokenExpiresAt: string
  user: UserInfo
}

export interface RefreshTokenResponse {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  refreshTokenExpiresAt: string
}

export interface UserInfo {
  name: string
  displayName: string
  profilePictureBase64?: string
  timeZone: string
  language: string
  currency: string
}

// Kratos-specific types
export interface KratosUserTraits {
  email: string
  name: string
  display_name?: string
  profile_picture_base64?: string
  date_of_birth?: string
  gender?: string
  default_currency?: string
  default_timezone?: string
  default_language?: string
  family_id?: number
}

export interface KratosSession {
  id: string
  active: boolean
  expires_at: string
  authenticated_at: string
  identity: KratosIdentity
  authentication_methods?: KratosAuthMethod[]
}

export interface KratosIdentity {
  id: string
  traits: KratosUserTraits
  schema_id: string
  schema_url: string
  state: string
  created_at: string
  updated_at: string
}

export interface KratosAuthMethod {
  method: string
  aal: string
  completed_at: string
}

export interface KratosFlowState {
  flowId: string | null
  flowType: 'login' | 'registration' | 'recovery' | 'verification' | null
  email: string | null
  codeSent: boolean
  codeExpired: boolean
  errors: string[]
}

