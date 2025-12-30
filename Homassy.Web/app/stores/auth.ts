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
}

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    user: null,
    accessToken: null,
    refreshToken: null,
    accessTokenExpiresAt: null,
    refreshTokenExpiresAt: null,
    refreshTimer: null
  }),

  getters: {
    isAuthenticated: (state) => !!state.accessToken && !!state.user,
    currentUser: (state) => state.user
  },

  actions: {
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
    },

    /**
     * Verify login code and authenticate user
     */
    async verifyCode(email: string, code: string) {
      const { $api, $i18n } = useNuxtApp()
      const toast = useToast()

      try {
        const response = await $api<{ data: AuthResponse }>('/api/v1/Auth/verify-code', {
          method: 'POST',
          body: { email, verificationCode: code } as VerifyLoginRequest
        })

        if (response.data) {
          this.setAuthData(response.data)

          toast.add({
            title: $i18n.t('toast.loggedIn'),
            description: $i18n.t('toast.welcome', { name: response.data.user.name }),
            color: 'success',
            icon: 'i-heroicons-check-circle'
          })
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
    },

    /**
     * Register new user
     */
    async register(userData: CreateUserRequest) {
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
    },

    /**
     * Refresh access token
     */
    async refreshAccessToken() {
      const { accessToken, refreshToken } = this.getTokensFromCookies()
      if (!accessToken || !refreshToken) {
        throw new Error('No tokens available for refresh')
      }

      // Keep state in sync with cookie source of truth
      this.accessToken = accessToken
      this.refreshToken = refreshToken

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
        // Refresh failed - logout user
        await this.logout()
        throw error
      }
    },

    /**
     * Logout user
     */
    async logout() {
      const { $api, $i18n } = useNuxtApp()
      const toast = useToast()

      try {
        await $api('/api/v1/Auth/logout', {
          method: 'POST'
        })
      } catch (error) {
        // Continue with logout even if API call fails
        console.error('Logout API call failed:', error)
      }

      // Clear state
      this.clearAuthData()

      toast.add({
        title: $i18n.t('toast.loggedOut'),
        description: $i18n.t('toast.signedOut'),
        color: 'success',
        icon: 'i-heroicons-arrow-left-on-rectangle'
      })

      // Redirect to home
      navigateTo('/')
    },

    /**
     * Get current user info
     */
    async fetchCurrentUser() {
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

      // Save to cookies
      this.saveToCookies()

      // Schedule automatic token refresh
      this.scheduleTokenRefresh()
    },

    /**
     * Clear authentication data
     */
    clearAuthData() {
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

      // Clear cookies
      this.clearCookies()
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

        accessTokenCookie.value = this.accessToken
        refreshTokenCookie.value = this.refreshToken
        userCookie.value = this.user ? JSON.stringify(this.user) : null
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

          // If tokens exist but user is missing, attempt recovery once
          if (!this.user) {
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

        accessTokenCookie.value = null
        refreshTokenCookie.value = null
        userCookie.value = null
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

      // Refresh 1 minute before expiration
      const refreshTime = Math.max(timeUntilExpiry - 60000, 0)

      this.refreshTimer = setTimeout(async () => {
        try {
          await this.refreshAccessToken()
        } catch (error) {
          console.error('Automatic token refresh failed:', error)
        }
      }, refreshTime)
    }
  }
})
