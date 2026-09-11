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
  /**
   * Stable public identifier for this user. The API's `UserInfo` DTO always sends it, but this
   * interface also backs the brief Kratos-trait-seeded object `authStore.traitsToUserInfo()`
   * returns between session restore and the first `fetchUserFromBackend()` round-trip — Kratos
   * traits carry no such id, so it is absent (not empty-string) until then, same as
   * `profilePictureUrl` below. Used to tell "this is me" apart from other members in
   * shopping-list presence and change attribution.
   */
  publicId?: string
  name: string
  displayName: string
  /** Server-relative, version-stamped path to the avatar thumbnail. See `useMediaUrl`. */
  profilePictureUrl?: string
  timeZone: string
  language: string
  currency: string
  /**
   * Chosen identity-colour palette key, or null/absent for the deterministic pick. Backs
   * `authStore.user`, which is what `useUserPreferences().buildPayload()` reads to keep resending
   * the current value on every settings save (see that file).
   */
  identityColor?: string | null
  /**
   * When the server last saw this user, ISO-8601 - or absent/null when it has never been written
   * (and always absent on the Kratos-trait-seeded object, which knows nothing about it).
   *
   * The away-delta feature (#127) uses it only when this device has no last-seen of its own: it
   * lags by up to a flush interval and counts every device, so a device-local value is always the
   * better answer. See `~/utils/awayGap`.
   */
  lastSeenAt?: string | null
  /**
   * When this user finished or skipped the first-run spotlight tour, ISO-8601 — or null
   * when they have not, which is what makes the tour start (#98). Absent on the
   * Kratos-trait-seeded object, which knows nothing about it; `useOnboardingTour` treats
   * absent the same as null and pairs it with a same-device `localStorage` echo, so a
   * momentarily-unknown value cannot flash the tour at a returning user.
   */
  onboardingCompletedAt?: string | null
}

// Kratos-specific types
export interface KratosUserTraits {
  email: string
  name: string
  display_name?: string
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

