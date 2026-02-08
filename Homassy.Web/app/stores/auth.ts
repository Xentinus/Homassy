/**
 * Authentication Store - Pinia (Kratos Version)
 * Manages user authentication state using Ory Kratos sessions
 */
import { defineStore } from 'pinia'
import type { Session } from '@ory/client'
import type { UserInfo, KratosUserTraits } from '~/types/auth'

interface AuthState {
  session: Session | null
  user: UserInfo | null
  isLoading: boolean
  isLoggingOut: boolean
  logoutPromise: Promise<void> | null
  initialized: boolean
}

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    session: null,
    user: null,
    isLoading: false,
    isLoggingOut: false,
    logoutPromise: null,
    initialized: false
  }),

  getters: {
    isAuthenticated: (state) => state.session?.active === true && state.user !== null,
    currentUser: (state) => state.user,
    kratosIdentityId: (state) => state.session?.identity?.id ?? null,
    sessionExpiresAt: (state) => state.session?.expires_at ?? null
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

    /**
     * Convert Kratos traits to UserInfo format
     */
    traitsToUserInfo(traits: KratosUserTraits): UserInfo {
      return {
        name: traits.name || '',
        displayName: traits.display_name || traits.name || '',
        profilePictureBase64: traits.profile_picture_base64,
        timeZone: traits.default_timezone || 'UTC',
        language: traits.default_language || 'en',
        currency: traits.default_currency || 'EUR'
      }
    },

    /**
     * Initialize auth state by checking Kratos session
     * Call this on app mount
     */
    async initialize() {
      if (this.initialized) {
        return
      }

      console.debug('[Auth] Initializing auth state...')
      this.isLoading = true

      try {
        const kratos = useKratos()
        const session = await kratos.getSession()

        if (session && session.active) {
          this.session = session
          const traits = session.identity?.traits as KratosUserTraits
          
          if (traits) {
            this.user = this.traitsToUserInfo(traits)
            
            // Sync language locale
            if (traits.default_language) {
              this.syncLanguageLocale(traits.default_language)
            }
          }
          
          console.debug('[Auth] Session restored, user:', this.user?.name)
        } else {
          console.debug('[Auth] No active session found')
          this.session = null
          this.user = null
        }
      } catch (error) {
        console.error('[Auth] Failed to initialize:', error)
        this.session = null
        this.user = null
      } finally {
        this.isLoading = false
        this.initialized = true
      }
    },

    /**
     * Refresh session from Kratos
     * Call this after successful login/registration
     */
    async refreshSession() {
      console.debug('[Auth] Refreshing session...')
      this.isLoading = true

      try {
        const kratos = useKratos()
        const session = await kratos.getSession()

        if (session && session.active) {
          this.session = session
          const traits = session.identity?.traits as KratosUserTraits
          
          if (traits) {
            this.user = this.traitsToUserInfo(traits)
            
            // Sync language locale
            if (traits.default_language) {
              this.syncLanguageLocale(traits.default_language)
            }
          }
          
          // Sync user to backend API
          await this.syncUserToBackend()
          
          console.debug('[Auth] Session refreshed, user:', this.user?.name)
          return true
        } else {
          console.debug('[Auth] No active session after refresh')
          this.session = null
          this.user = null
          return false
        }
      } catch (error) {
        console.error('[Auth] Failed to refresh session:', error)
        this.session = null
        this.user = null
        return false
      } finally {
        this.isLoading = false
      }
    },

    /**
     * Sync user data to backend API after Kratos login
     * This ensures the backend has the latest user data
     */
    async syncUserToBackend() {
      try {
        const { $api } = useNuxtApp()
        
        // Call backend sync endpoint
        await $api('/api/v1/Auth/sync', {
          method: 'POST'
        })
        
        console.debug('[Auth] User synced to backend')
      } catch (error) {
        // Non-fatal error - backend sync is optional
        console.warn('[Auth] Failed to sync user to backend:', error)
      }
    },

    /**
     * Get fresh user info from backend API
     */
    async fetchUserFromBackend(): Promise<UserInfo | null> {
      try {
        const { $api } = useNuxtApp()
        
        const response = await $api<{ data: UserInfo }>('/api/v1/Auth/me', {
          method: 'GET'
        })
        
        if (response.data) {
          this.user = response.data
          
          // Sync language locale
          if (response.data.language) {
            this.syncLanguageLocale(response.data.language)
          }
        }
        
        return response.data
      } catch (error) {
        console.error('[Auth] Failed to fetch user from backend:', error)
        return null
      }
    },

    /**
     * Logout user
     * Ends the Kratos session via browser navigation
     */
    async logout() {
      // Guard: If already logging out, return the existing promise
      if (this.isLoggingOut && this.logoutPromise) {
        console.debug('[Auth] Logout already in progress, returning existing promise')
        return this.logoutPromise
      }

      // Guard: If already logged out
      if (!this.session) {
        console.debug('[Auth] Already logged out, redirecting to home')
        if (typeof window !== 'undefined') {
          navigateTo('/')
        }
        return
      }

      this.isLoggingOut = true

      this.logoutPromise = (async () => {
        try {
          const kratos = useKratos()
          
          // Clear local state first (before browser redirect)
          this.clearAuthData()
          
          // Set flag to show toast on landing page after redirect
          if (typeof window !== 'undefined') {
            localStorage.setItem('homassy_logout_success', 'true')
          }

          // Perform logout via browser navigation
          // This will redirect to Kratos which clears httpOnly cookies
          // then redirects back to /?logout=success
          const returnUrl = typeof window !== 'undefined' 
            ? `${window.location.origin}/?logout=success`
            : '/?logout=success'
          
          await kratos.logout(returnUrl)
          
          // Note: Code below this won't execute because browser redirects
        } catch (composableError) {
          console.warn('[Auth] Logout failed, forcing redirect', composableError)
          this.clearAuthData()
          if (typeof window !== 'undefined') {
            localStorage.setItem('homassy_logout_success', 'true')
            window.location.href = '/'
          }
        }
      })()

      try {
        await this.logoutPromise
      } finally {
        this.isLoggingOut = false
        this.logoutPromise = null
      }
    },

    /**
     * Clear authentication data
     */
    clearAuthData() {
      console.debug('[Auth] Clearing auth data')
      
      this.session = null
      this.user = null
      this.isLoggingOut = false
      this.logoutPromise = null
      this.initialized = false
    },

    /**
     * Check if session is still valid
     * Returns true if session exists and is not expired
     */
    isSessionValid(): boolean {
      if (!this.session || !this.session.active) {
        return false
      }

      if (this.session.expires_at) {
        const expiresAt = new Date(this.session.expires_at).getTime()
        const now = Date.now()
        return expiresAt > now
      }

      return true
    },

    /**
     * Setup visibility listener to refresh session when user returns to tab
     * This handles long-running background tabs
     */
    setupVisibilityListener() {
      // Only run on client
      if (import.meta.server) {
        return
      }

      document.addEventListener('visibilitychange', async () => {
        if (document.visibilityState === 'visible' && this.isAuthenticated) {
          console.debug('[Auth] Tab became visible, checking session validity...')
          
          // Only refresh if session might be stale (older than 5 minutes since last check)
          if (!this.isSessionValid()) {
            console.debug('[Auth] Session may be stale, refreshing...')
            await this.refreshSession()
          }
        }
      })
    },

    // =====================================================
    // Legacy methods for backward compatibility
    // These are no-ops or redirects to new Kratos-based methods
    // =====================================================

    /**
     * @deprecated Use initialize() instead
     */
    async loadFromCookies() {
      console.debug('[Auth] loadFromCookies called - delegating to initialize()')
      await this.initialize()
    },

    /**
     * @deprecated Kratos handles tokens via session cookies
     */
    getTokensFromCookies() {
      // Return empty tokens - Kratos uses session cookies
      return {
        accessToken: null,
        refreshToken: null
      }
    },

    /**
     * @deprecated Use refreshSession() instead  
     */
    async refreshAccessToken() {
      console.debug('[Auth] refreshAccessToken called - delegating to refreshSession()')
      return await this.refreshSession()
    },

    /**
     * @deprecated Get user from session instead
     */
    async fetchCurrentUser(_syncLocale: boolean = true): Promise<UserInfo | null> {
      return this.user
    },

    // Legacy cookie properties (for code that still references them)
    get accessToken() {
      return null
    },
    get refreshToken() {
      return null
    }
  }
})
