/**
 * Authentication related types
 */

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
