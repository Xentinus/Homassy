/**
 * Authentication Store - Pinia
 * Manages user authentication state, tokens, and automatic refresh
 */
import { defineStore } from 'pinia'
import type {
  LoginRequest,
  VerifyLoginRequest,
  CreateUserRequest,
  AuthResponse,
  RefreshTokenRequest,
  UserInfo
} from '~/types/auth'

interface AuthState {
  user: UserInfo | null
  accessToken: string | null
  refreshToken: string | null
  accessTokenExpiresAt: string | null
  refreshTokenExpiresAt: string | null
  refreshTimer: ReturnType<typeof setTimeout> | null
  isLoggingOut: boolean
  logoutPromise: Promise<void> | null
  isRefreshing: boolean
  refreshPromise: Promise<any> | null
}

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    user: null,
    accessToken: null,
    refreshToken: null,
    accessTokenExpiresAt: null,
    refreshTokenExpiresAt: null,
    refreshTimer: null,
    isLoggingOut: false,
    logoutPromise: null,
    isRefreshing: false,
    refreshPromise: null
  }),

  getters: {
    isAuthenticated: (state) => !!state.accessToken && !!state.user,
    currentUser: (state) => state.user
  },

  actions: {
    /**
     * Sync language locale and cookie from user data
     * Returns the locale code for caller to use with setLocale
     */
    syncLanguageLocale(language: string): string {
      try {
        const localeCookie = useCookie('homassy_locale', {
          maxAge: 10 * 365 * 24 * 60 * 60, // 10 years
          path: '/',
          sameSite: 'lax'
        })
        
        // Map language name/code to locale code
        // Support both full names (English, German) and codes (en, de)
        const localeMap: Record<string, string> = {
          'English': 'en',
          'Hungarian': 'hu',
          'German': 'de',
          'en': 'en',
          'hu': 'hu',
          'de': 'de'
        }
        
        const localeCode = localeMap[language] || 'en'
        localeCookie.value = localeCode
        
        return localeCode
      } catch (error) {
        console.warn('[Auth] Could not sync language locale:', error)
        return 'en'
      }
    },

    getTokensFromCookies() {
      try {
        const accessTokenCookie = useCookie('homassy_access_token')
        const refreshTokenCookie = useCookie('homassy_refresh_token')

        return {
          accessToken: accessTokenCookie.value as unknown as string | null,
          refreshToken: refreshTokenCookie.value as unknown as string | null
        }
      } catch (error) {
        console.warn('[Auth] Could not read tokens from cookies - not in valid Nuxt context')
        return {
          accessToken: null,
          refreshToken: null
        }
      }
    },

    /**
     * Request verification code for login
     */
    async requestCode(email: string) {
      try {
        const { $api, $i18n } = useNuxtApp()
        const toast = useToast()

        try {
          const response = await $api('/api/v1/Auth/request-code', {
            method: 'POST',
            body: { email } as LoginRequest
          })

          toast.add({
            title: $i18n.t('toast.verificationCodeSent'),
            description: $i18n.t('toast.checkEmail'),
            color: 'success',
            icon: 'i-heroicons-envelope'
          })

          return response
        } catch (error) {
          toast.add({
            title: $i18n.t('toast.error'),
            description: $i18n.t('toast.failedToSendCode'),
            color: 'error',
            icon: 'i-heroicons-x-circle'
          })
          throw error
        }
      } catch (composableError) {
        console.error('[Auth] Composables not available in requestCode', composableError)
        throw composableError
      }
    },

    /**
     * Verify login code and authenticate user
     */
    async verifyCode(email: string, code: string) {
      try {
        const { $api, $i18n } = useNuxtApp()
        const toast = useToast()

        try {
          const response = await $api<{ data: AuthResponse }>('/api/v1/Auth/verify-code', {
            method: 'POST',
            body: { email, verificationCode: code } as VerifyLoginRequest
          })

          if (response.data) {
            this.setAuthData(response.data)
          }

          return response
        } catch (error) {
          toast.add({
            title: $i18n.t('toast.loginError'),
            description: $i18n.t('toast.invalidCode'),
            color: 'error',
            icon: 'i-heroicons-x-circle'
          })
          throw error
        }
      } catch (composableError) {
        console.error('[Auth] Composables not available in verifyCode', composableError)
        throw composableError
      }
    },

    /**
     * Register new user
     */
    async register(userData: CreateUserRequest) {
      try {
        const { $api, $i18n } = useNuxtApp()
        const toast = useToast()

        try {
          const response = await $api<{ data: AuthResponse }>('/api/v1/Auth/register', {
            method: 'POST',
            body: userData
          })

          if (response.data) {
            this.setAuthData(response.data)

            toast.add({
              title: $i18n.t('toast.registered'),
              description: $i18n.t('toast.accountCreated'),
              color: 'success',
              icon: 'i-heroicons-check-circle'
            })
          }

          return response
        } catch (error) {
          toast.add({
            title: $i18n.t('toast.registrationError'),
            description: $i18n.t('toast.failedToCreateAccount'),
            color: 'error',
            icon: 'i-heroicons-x-circle'
          })
          throw error
        }
      } catch (composableError) {
        console.error('[Auth] Composables not available in register', composableError)
        throw composableError
      }
    },

    /**
     * Refresh access token
     * Guarded to prevent multiple concurrent refresh attempts
     */
    async refreshAccessToken() {
      // Guard: If already refreshing, return the existing promise
      if (this.isRefreshing && this.refreshPromise) {
        console.debug('[Auth] Token refresh already in progress, returning existing promise')
        return this.refreshPromise
      }

      const { accessToken, refreshToken } = this.getTokensFromCookies()
      if (!accessToken || !refreshToken) {
        throw new Error('No tokens available for refresh')
      }

      // Keep state in sync with cookie source of truth
      this.accessToken = accessToken
      this.refreshToken = refreshToken

      // Set guard flag and create refresh promise
      this.isRefreshing = true

      this.refreshPromise = (async () => {
        const { $api } = useNuxtApp()

        try {
          const response = await $api<{ data: { accessToken: string; refreshToken: string; accessTokenExpiresAt: string; refreshTokenExpiresAt: string } }>('/api/v1/Auth/refresh', {
            method: 'POST',
            body: {
              accessToken,
              refreshToken
            } as RefreshTokenRequest
          })

          if (response.data) {
            this.accessToken = response.data.accessToken
            this.refreshToken = response.data.refreshToken
            this.accessTokenExpiresAt = response.data.accessTokenExpiresAt
            this.refreshTokenExpiresAt = response.data.refreshTokenExpiresAt

            // Save tokens + current user snapshot to cookies
            this.saveToCookies()

            // Schedule next refresh
            this.scheduleTokenRefresh()

            // Refresh user data only when token is refreshed
            try {
              const user = await this.fetchCurrentUser()
              if (user) {
                this.user = user
                this.saveToCookies()
              }
            } catch (error) {
              console.error('Failed to refresh user after token refresh', error)
            }
          }

          return response
        } catch (error) {
          console.error('[Auth] Token refresh failed:', error)
          
          // Check if this is a 401 (invalid refresh token) vs network error
          const is401 = error?.response?.status === 401 || error?.statusCode === 401
          
          if (is401) {
            console.error('[Auth] Refresh token is invalid (401), clearing immediately')
            // Invalid refresh token - clear auth data immediately
            this.clearAuthData()
          } else {
            console.error('[Auth] Token refresh network error, will retry later')
            // Network error - don't clear, let retry logic handle it
          }
          
          throw error
        } finally {
          // Reset guard state
          this.isRefreshing = false
          this.refreshPromise = null
        }
      })()

      return this.refreshPromise
    },

    /**
     * Logout user
     * Guarded to prevent multiple concurrent logout calls
     */
    async logout() {
      // Guard: If already logging out, return the existing promise
      if (this.isLoggingOut && this.logoutPromise) {
        console.debug('[Auth] Logout already in progress, returning existing promise')
        return this.logoutPromise
      }

      // Guard: If already logged out (no tokens), just redirect
      if (!this.accessToken && !this.refreshToken) {
        console.debug('[Auth] Already logged out, redirecting to home')
        if (typeof window !== 'undefined') {
          navigateTo('/')
        }
        return
      }

      // Set guard flag and create logout promise
      this.isLoggingOut = true

      this.logoutPromise = (async () => {
        try {
          const { $api, $i18n } = useNuxtApp()
          const toast = useToast()

          try {
            // Try to call logout API endpoint
            await $api('/api/v1/Auth/logout', {
              method: 'POST'
            })
          } catch (error) {
            // Continue with logout even if API call fails
            console.error('Logout API call failed:', error)
          }

          // Clear state (this will clear tokens and auth data)
          this.clearAuthData()

          // Show toast notification ONCE
          toast.add({
            title: $i18n.t('toast.loggedOut'),
            description: $i18n.t('toast.signedOut'),
            color: 'success',
            icon: 'i-heroicons-arrow-left-on-rectangle'
          })

          // Redirect to home ONCE
          if (typeof window !== 'undefined') {
            await navigateTo('/')
          }
        } catch (composableError) {
          // If composables are not available (e.g., called from background process)
          // Just clear auth data and redirect manually
          console.warn('[Auth] Composables not available during logout, performing manual cleanup', composableError)
          this.clearAuthData()
          if (typeof window !== 'undefined') {
            window.location.href = '/'
          }
        }
      })()

      try {
        await this.logoutPromise
      } finally {
        // Reset guard state after logout completes
        this.isLoggingOut = false
        this.logoutPromise = null
      }
    },

    /**
     * Get current user info
     * @param syncLocale - If false, skips locale synchronization (useful when locale was just set manually)
     */
    async fetchCurrentUser(syncLocale: boolean = true) {
      const { accessToken } = this.getTokensFromCookies()
      if (!accessToken) return null

      // Sync state from cookie so downstream checks stay consistent
      if (!this.accessToken) {
        this.accessToken = accessToken
      }

      const { $api } = useNuxtApp()

      try {
        const response = await $api<{ data: UserInfo }>('/api/v1/Auth/me', {
          method: 'GET'
        })

        if (response.data) {
          this.user = response.data
          // Sync language locale and cookie only if requested
          if (syncLocale && response.data.language) {
            this.syncLanguageLocale(response.data.language)
          }
        }

        return response.data
      } catch (error) {
        console.error('Failed to fetch current user:', error)
        return null
      }
    },

    /**
     * Set authentication data from API response
     */
    setAuthData(authData: AuthResponse) {
      this.user = authData.user
      this.accessToken = authData.accessToken
      this.refreshToken = authData.refreshToken
      this.accessTokenExpiresAt = authData.accessTokenExpiresAt
      this.refreshTokenExpiresAt = authData.refreshTokenExpiresAt

      // Sync language locale and cookie
      if (authData.user.language) {
        this.syncLanguageLocale(authData.user.language)
      }

      // Save to cookies
      this.saveToCookies()

      // Schedule automatic token refresh
      this.scheduleTokenRefresh()
    },

    /**
     * Clear authentication data
     */
    clearAuthData() {
      console.debug('[Auth] Clearing auth data and cookies')
      
      this.user = null
      this.accessToken = null
      this.refreshToken = null
      this.accessTokenExpiresAt = null
      this.refreshTokenExpiresAt = null

      // Clear refresh timer
      if (this.refreshTimer) {
        clearTimeout(this.refreshTimer)
        this.refreshTimer = null
      }

      // Clear guard states
      this.isLoggingOut = false
      this.logoutPromise = null
      this.isRefreshing = false
      this.refreshPromise = null

      // Clear cookies SYNCHRONOUSLY
      this.clearCookies()
      
      // Force a second clear to ensure cookies are gone
      // This handles edge cases where first clear might not work
      if (typeof window !== 'undefined') {
        setTimeout(() => {
          this.clearCookies()
        }, 0)
      }
    },

    /**
     * Save tokens to cookies
     */
    saveToCookies() {
      try {
        // Use secure cookies only in production (HTTPS). In dev (HTTP),
        // cookies must be sent to SSR on refresh, so set secure=false.
        const isSecure = import.meta.env.PROD

        const accessTokenCookie = useCookie('homassy_access_token', {
          maxAge: 60 * 60 * 24 * 7, // 7 days
          sameSite: 'lax',
          secure: isSecure,
          path: '/'
        })
        const refreshTokenCookie = useCookie('homassy_refresh_token', {
          maxAge: 60 * 60 * 24 * 30, // 30 days
          sameSite: 'lax',
          secure: isSecure,
          path: '/'
        })
        const userCookie = useCookie('homassy_user', {
          maxAge: 60 * 60 * 24 * 7,
          sameSite: 'lax',
          secure: isSecure,
          path: '/'
        })
        const accessTokenExpiresCookie = useCookie('homassy_access_token_expires', {
          maxAge: 60 * 60 * 24 * 7,
          sameSite: 'lax',
          secure: isSecure,
          path: '/'
        })
        const refreshTokenExpiresCookie = useCookie('homassy_refresh_token_expires', {
          maxAge: 60 * 60 * 24 * 30,
          sameSite: 'lax',
          secure: isSecure,
          path: '/'
        })

        accessTokenCookie.value = this.accessToken
        refreshTokenCookie.value = this.refreshToken
        userCookie.value = this.user ? JSON.stringify(this.user) : null
        accessTokenExpiresCookie.value = this.accessTokenExpiresAt
        refreshTokenExpiresCookie.value = this.refreshTokenExpiresAt
      } catch (error) {
        console.warn('[Auth] Could not save to cookies - not in valid Nuxt context')
      }
    },

    /**
     * Load tokens from cookies
     */
    async loadFromCookies() {
      try {
        const accessTokenCookie = useCookie('homassy_access_token')
        const refreshTokenCookie = useCookie('homassy_refresh_token')
        const userCookie = useCookie('homassy_user')
        const accessTokenExpiresCookie = useCookie('homassy_access_token_expires')
        const refreshTokenExpiresCookie = useCookie('homassy_refresh_token_expires')

        const preview = (v: unknown, len = 20): string => {
          if (!v) return 'null'
          if (typeof v === 'string') return v.slice(0, len) + ((v as string).length > len ? '...' : '')
          if (typeof v === 'object') return '[object]'
          return String(v)
        }

        console.debug('[Auth] loadFromCookies: checking tokens...')
        console.debug(`[Auth] accessToken: ${preview(accessTokenCookie.value)}`)
        console.debug(`[Auth] refreshToken: ${preview(refreshTokenCookie.value)}`)
        console.debug(`[Auth] userCookie type: ${typeof userCookie.value}`)

        if (accessTokenCookie.value && refreshTokenCookie.value) {
          this.accessToken = accessTokenCookie.value as unknown as string
          this.refreshToken = refreshTokenCookie.value as unknown as string
          this.accessTokenExpiresAt = accessTokenExpiresCookie.value as unknown as string | null
          this.refreshTokenExpiresAt = refreshTokenExpiresCookie.value as unknown as string | null

          let parsedUser: UserInfo | null = null
          if (userCookie.value) {
            try {
              if (typeof userCookie.value === 'object') {
                parsedUser = userCookie.value as unknown as UserInfo
              } else if (typeof userCookie.value === 'string') {
                const raw = userCookie.value as string
                const looksJson = raw.trim().startsWith('{')
                parsedUser = looksJson ? JSON.parse(raw) : null
              }
            } catch (error) {
              console.error('Failed to parse user cookie', error)
              parsedUser = null
            }
          }
          this.user = parsedUser

          console.debug(`[Auth] Loaded tokens, isAuthenticated: ${!!this.accessToken && !!this.user}`)

          // If tokens exist but user is missing, attempt recovery once (only on client side)
          if (!this.user && import.meta.client) {
            // Clear corrupted user cookie to avoid future parse errors
            userCookie.value = null
            try {
              console.debug('[Auth] Attempting user recovery...')
              const fetched = await this.fetchCurrentUser()
              if (fetched) {
                this.user = fetched
                this.saveToCookies()
                console.debug('[Auth] User recovered and saved')
              }
            } catch (e) {
              console.error('User recovery after cookie parse failed', e)
            }
          }

          // Schedule token refresh (client only)
          this.scheduleTokenRefresh()
        } else {
          console.debug('[Auth] No tokens found in cookies')
        }
      } catch (error) {
        console.warn('[Auth] Could not load from cookies - not in valid Nuxt context', error)
      }
    },

    /**
     * Clear cookies
     */
    clearCookies() {
      // Only clear cookies if we're in a valid Nuxt context
      // This prevents errors when called from error handlers
      try {
        const accessTokenCookie = useCookie('homassy_access_token')
        const refreshTokenCookie = useCookie('homassy_refresh_token')
        const userCookie = useCookie('homassy_user')
        const accessTokenExpiresCookie = useCookie('homassy_access_token_expires')
        const refreshTokenExpiresCookie = useCookie('homassy_refresh_token_expires')

        accessTokenCookie.value = null
        refreshTokenCookie.value = null
        userCookie.value = null
        accessTokenExpiresCookie.value = null
        refreshTokenExpiresCookie.value = null
      } catch (error) {
        // If we can't access cookies (e.g., outside Nuxt context), skip clearing
        // The cookies will be cleared on next page load when context is available
        console.warn('[Auth] Could not clear cookies - not in valid Nuxt context')
      }
    },

    /**
     * Schedule automatic token refresh before expiration
     */
    scheduleTokenRefresh() {
      // Only schedule timers on the client
      if (!import.meta.client) return

      // Clear existing timer
      if (this.refreshTimer) {
        clearTimeout(this.refreshTimer)
      }

      if (!this.accessTokenExpiresAt) return

      // Calculate time until token expires
      const expiresAt = new Date(this.accessTokenExpiresAt).getTime()
      const now = Date.now()
      const timeUntilExpiry = expiresAt - now

      // Refresh 5 minutes before expiration
      const refreshTime = Math.max(timeUntilExpiry - 300000, 0) // 5 minutes = 300000ms

      console.debug(`[Auth] Scheduling token refresh in ${Math.round(refreshTime / 1000)} seconds`)

      this.refreshTimer = setTimeout(async () => {
        try {
          console.debug('[Auth] Automatic token refresh triggered')
          await this.refreshAccessToken()
        } catch (error) {
          console.error('[Auth] Automatic token refresh failed:', error)

          // Retry every minute until expiration if refresh fails
          if (this.accessTokenExpiresAt) {
            const expiresAt = new Date(this.accessTokenExpiresAt).getTime()
            const now = Date.now()
            const timeUntilExpiry = expiresAt - now

            if (timeUntilExpiry > 0) {
              console.debug('[Auth] Scheduling retry in 1 minute...')
              this.refreshTimer = setTimeout(async () => {
                // Recursively call scheduleTokenRefresh to retry
                this.scheduleTokenRefresh()
              }, 60000) // Retry after 1 minute
            } else {
              console.error('[Auth] Token expired, cannot retry. Logging out...')
              await this.logout()
            }
          }
        }
      }, refreshTime)
    },

    /**
     * Setup visibility change listener to refresh token when tab becomes visible
     * This handles cases where the browser was backgrounded for a long time
     */
    setupVisibilityListener() {
      // Only run on client
      if (!import.meta.client) return

      const handleVisibilityChange = async () => {
        // Only act when page becomes visible
        if (document.hidden) return

        console.debug('[Auth] Page became visible, checking token expiration')

        // Check if we have an access token
        if (!this.accessTokenExpiresAt || !this.accessToken) {
          console.debug('[Auth] No token to refresh')
          return
        }

        // Calculate time until expiration
        const expiresAt = new Date(this.accessTokenExpiresAt).getTime()
        const now = Date.now()
        const timeUntilExpiry = expiresAt - now

        // Refresh if token expires in less than 5 minutes or already expired
        const fiveMinutes = 300000 // 5 minutes in ms
        if (timeUntilExpiry < fiveMinutes) {
          console.debug('[Auth] Token expiring soon or expired, refreshing...', {
            expiresIn: Math.round(timeUntilExpiry / 1000) + 's'
          })

          try {
            await this.refreshAccessToken()
          } catch (error) {
            console.error('[Auth] Failed to refresh token on visibility change:', error)
          }
        } else {
          console.debug('[Auth] Token still valid', {
            expiresIn: Math.round(timeUntilExpiry / 1000) + 's'
          })
        }
      }

      // Add event listener
      document.addEventListener('visibilitychange', handleVisibilityChange)

      // Cleanup function (will be called when store is disposed)
      if (typeof window !== 'undefined') {
        window.addEventListener('beforeunload', () => {
          document.removeEventListener('visibilitychange', handleVisibilityChange)
        })
      }

      console.debug('[Auth] Visibility listener setup complete')
    }
  }
})
