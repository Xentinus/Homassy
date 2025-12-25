/**
 * Authentication API composable
 * Provides authentication-related API calls
 */
import type {
  CreateUserRequest,
  UserInfo
} from '~/types/api'

export const useAuthApi = () => {
  const authStore = useAuthStore()

  /**
   * Request verification code for login
   */
  const requestCode = async (email: string) => {
    return await authStore.requestCode(email)
  }

  /**
   * Verify login code
   */
  const verifyCode = async (email: string, code: string) => {
    return await authStore.verifyCode(email, code)
  }

  /**
   * Register new user
   */
  const register = async (userData: CreateUserRequest) => {
    return await authStore.register(userData)
  }

  /**
   * Logout
   */
  const logout = async () => {
    return await authStore.logout()
  }

  /**
   * Get current user info
   */
  const getCurrentUser = async (): Promise<UserInfo | null> => {
    return await authStore.fetchCurrentUser()
  }

  /**
   * Refresh access token
   */
  const refreshToken = async () => {
    return await authStore.refreshAccessToken()
  }

  return {
    requestCode,
    verifyCode,
    register,
    logout,
    getCurrentUser,
    refreshToken
  }
}
