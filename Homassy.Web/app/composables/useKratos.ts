/**
 * Kratos Client Composable
 * Provides Ory Kratos integration for authentication flows
 */
import {
  FrontendApi,
  Configuration,
  type LoginFlow,
  type RegistrationFlow,
  type RecoveryFlow,
  type VerificationFlow,
  type SettingsFlow,
  type Session,
  type Identity,
  type UiNodeInputAttributes,
  type UpdateLoginFlowBody,
  type UpdateRegistrationFlowBody,
  type UpdateRecoveryFlowBody,
  type UpdateVerificationFlowBody,
  type UpdateSettingsFlowBody,
  type UiNode
} from '@ory/client'

export interface WebAuthnCredential {
  id: string
  displayName: string
  createdAt: string
  publicKey?: string
  canDelete?: boolean // false when it's the last credential
}

export interface KratosConfig {
  publicUrl: string
}

export interface FlowError {
  id: string
  error: {
    code: number
    status: string
    reason: string
    message: string
  }
}

/**
 * The body Kratos answers a failed flow call with. It is one of several things —
 * the flow itself (so the caller can re-render the UI it came back with), a
 * `FlowError`, or a 422 telling the browser where to go next — and callers probe
 * for the field they care about, so every field is optional.
 */
export type KratosErrorBody = Partial<
  LoginFlow & RegistrationFlow & RecoveryFlow & VerificationFlow & SettingsFlow & FlowError
> & {
  /** Present on the 422 that hands a flow off to another provider or step. */
  redirect_browser_to?: string
}

/** The axios response on a Kratos rejection, as far as this app reads it. */
export interface KratosErrorResponse {
  status?: number
  data?: KratosErrorBody
}

export interface KratosError {
  code: string
  message: string
  details?: Record<string, unknown>
  /** Kept so the caller can re-render the flow Kratos sent back with the error. */
  response?: KratosErrorResponse
}

/**
 * @ory/client rejects with an axios error, but a call that never reached Kratos
 * rejects with a bare `TypeError`, so a caught value is only ever `unknown`.
 * This reads the fields the app needs off it without claiming the rest is there.
 */
const asKratosRejection = (error: unknown): { response?: KratosErrorResponse, message?: string } =>
  (error ?? {}) as { response?: KratosErrorResponse, message?: string }

/** Status Kratos answered with, or `undefined` when nothing answered. */
const kratosStatus = (error: unknown): number | undefined => asKratosRejection(error).response?.status

/**
 * Kratos composable for managing authentication flows
 */
export const useKratos = () => {
  const config = useRuntimeConfig()
  const kratosUrl = config.public.kratosPublicUrl || 'http://localhost:4433'

  // Create Kratos frontend API client
  const kratos = new FrontendApi(
    new Configuration({
      basePath: kratosUrl,
      baseOptions: {
        withCredentials: true // Important for session cookies
      }
    })
  )

  /**
   * Get current session if authenticated
   */
  const getSession = async (): Promise<Session | null> => {
    try {
      const response = await kratos.toSession()
      return response.data
    } catch (error) {
      // 401 means not authenticated - this is expected
      if (kratosStatus(error) === 401) {
        return null
      }
      console.error('[Kratos] Error getting session:', error)
      return null
    }
  }

  /**
   * Check if user is authenticated
   */
  const isAuthenticated = async (): Promise<boolean> => {
    const session = await getSession()
    return session !== null && session.active === true
  }

  /**
   * Initialize a login flow
   */
  const createLoginFlow = async (refresh?: boolean, aal?: string): Promise<LoginFlow> => {
    try {
      const response = await kratos.createBrowserLoginFlow({
        refresh: refresh,
        aal: aal
      })
      return response.data
    } catch (error) {
      console.error('[Kratos] Error creating login flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Get an existing login flow by ID
   */
  const getLoginFlow = async (flowId: string): Promise<LoginFlow> => {
    try {
      const response = await kratos.getLoginFlow({ id: flowId })
      return response.data
    } catch (error) {
      console.error('[Kratos] Error getting login flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Submit a login flow
   */
  const submitLoginFlow = async (flowId: string, body: UpdateLoginFlowBody): Promise<{ session: Session }> => {
    try {
      const response = await kratos.updateLoginFlow({
        flow: flowId,
        updateLoginFlowBody: body
      })
      return { session: response.data.session! }
    } catch (error) {
      console.error('[Kratos] Error submitting login flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Initialize a registration flow
   */
  const createRegistrationFlow = async (): Promise<RegistrationFlow> => {
    try {
      const response = await kratos.createBrowserRegistrationFlow()
      return response.data
    } catch (error) {
      console.error('[Kratos] Error creating registration flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Get an existing registration flow by ID
   */
  const getRegistrationFlow = async (flowId: string): Promise<RegistrationFlow> => {
    try {
      const response = await kratos.getRegistrationFlow({ id: flowId })
      return response.data
    } catch (error) {
      console.error('[Kratos] Error getting registration flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Submit a registration flow
   */
  const submitRegistrationFlow = async (flowId: string, body: UpdateRegistrationFlowBody): Promise<{ identity?: Identity }> => {
    try {
      const response = await kratos.updateRegistrationFlow({
        flow: flowId,
        updateRegistrationFlowBody: body
      })
      return { identity: response.data.identity }
    } catch (error) {
      console.error('[Kratos] Error submitting registration flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Initialize a recovery flow
   */
  const createRecoveryFlow = async (): Promise<RecoveryFlow> => {
    try {
      const response = await kratos.createBrowserRecoveryFlow()
      return response.data
    } catch (error) {
      console.error('[Kratos] Error creating recovery flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Get an existing recovery flow by ID
   */
  const getRecoveryFlow = async (flowId: string): Promise<RecoveryFlow> => {
    try {
      const response = await kratos.getRecoveryFlow({ id: flowId })
      return response.data
    } catch (error) {
      console.error('[Kratos] Error getting recovery flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Submit a recovery flow
   */
  const submitRecoveryFlow = async (flowId: string, body: UpdateRecoveryFlowBody): Promise<void> => {
    try {
      await kratos.updateRecoveryFlow({
        flow: flowId,
        updateRecoveryFlowBody: body
      })
    } catch (error) {
      console.error('[Kratos] Error submitting recovery flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Initialize a verification flow
   */
  const createVerificationFlow = async (): Promise<VerificationFlow> => {
    try {
      const response = await kratos.createBrowserVerificationFlow()
      return response.data
    } catch (error) {
      console.error('[Kratos] Error creating verification flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Get an existing verification flow by ID
   */
  const getVerificationFlow = async (flowId: string): Promise<VerificationFlow> => {
    try {
      const response = await kratos.getVerificationFlow({ id: flowId })
      return response.data
    } catch (error) {
      console.error('[Kratos] Error getting verification flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Submit a verification flow
   */
  const submitVerificationFlow = async (flowId: string, body: UpdateVerificationFlowBody): Promise<void> => {
    try {
      await kratos.updateVerificationFlow({
        flow: flowId,
        updateVerificationFlowBody: body
      })
    } catch (error) {
      console.error('[Kratos] Error submitting verification flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Initialize a settings flow
   */
  const createSettingsFlow = async (): Promise<SettingsFlow> => {
    try {
      const response = await kratos.createBrowserSettingsFlow()
      return response.data
    } catch (error) {
      console.error('[Kratos] Error creating settings flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Get an existing settings flow by ID
   */
  const getSettingsFlow = async (flowId: string): Promise<SettingsFlow> => {
    try {
      const response = await kratos.getSettingsFlow({ id: flowId })
      return response.data
    } catch (error) {
      console.error('[Kratos] Error getting settings flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Submit a settings flow (e.g., to register/remove WebAuthn credentials)
   */
  const submitSettingsFlow = async (flowId: string, body: UpdateSettingsFlowBody): Promise<SettingsFlow> => {
    try {
      const response = await kratos.updateSettingsFlow({
        flow: flowId,
        updateSettingsFlowBody: body
      })
      return response.data
    } catch (error) {
      console.error('[Kratos] Error submitting settings flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Extract WebAuthn credentials from an identity
   */
  const getWebAuthnCredentials = (identity: Identity): WebAuthnCredential[] => {
    const credentials: WebAuthnCredential[] = []
    
    if (!identity.credentials) {
      return credentials
    }

    // Check for webauthn credentials
    const webauthnCred = identity.credentials['webauthn']
    if (webauthnCred?.identifiers) {
      webauthnCred.identifiers.forEach((id, index) => {
        credentials.push({
          id: id,
          displayName: `Passkey ${index + 1}`,
          createdAt: webauthnCred.created_at || new Date().toISOString()
        })
      })
    }

    // Also check for passkey credentials (newer Kratos versions)
    const passkeyCred = identity.credentials['passkey']
    if (passkeyCred?.identifiers) {
      passkeyCred.identifiers.forEach((id, index) => {
        credentials.push({
          id: id,
          displayName: `Passkey ${credentials.length + index + 1}`,
          createdAt: passkeyCred.created_at || new Date().toISOString()
        })
      })
    }

    return credentials
  }

  /**
   * Extract WebAuthn credentials from settings flow UI nodes
   * This is more reliable as it includes display names
   */
  const getWebAuthnCredentialsFromFlow = (flow: SettingsFlow): WebAuthnCredential[] => {
    const credentials: WebAuthnCredential[] = []
    
    // Find webauthn_remove nodes - each represents a registered credential
    flow.ui.nodes.forEach((node) => {
      if (node.group === 'webauthn' && 
          node.attributes.node_type === 'input' &&
          (node.attributes as UiNodeInputAttributes).name === 'webauthn_remove') {
        const attrs = node.attributes as UiNodeInputAttributes
        // Extract display_name and added_at from label context (not from label.text which contains "Remove security key...")
        const context = node.meta?.label?.context as { display_name?: string; added_at?: string } | undefined
        credentials.push({
          id: attrs.value as string,
          displayName: context?.display_name || `Passkey ${credentials.length + 1}`,
          createdAt: context?.added_at || '',
          canDelete: !attrs.disabled // Kratos sets disabled=true when it would lock out user
        })
      }
      // Also check passkey_remove for newer Kratos versions
      if (node.group === 'passkey' && 
          node.attributes.node_type === 'input' &&
          (node.attributes as UiNodeInputAttributes).name === 'passkey_remove') {
        const attrs = node.attributes as UiNodeInputAttributes
        // Extract display_name and added_at from label context (not from label.text which contains "Remove security key...")
        const context = node.meta?.label?.context as { display_name?: string; added_at?: string } | undefined
        credentials.push({
          id: attrs.value as string,
          displayName: context?.display_name || `Passkey ${credentials.length + 1}`,
          createdAt: context?.added_at || '',
          canDelete: !attrs.disabled // Kratos sets disabled=true when it would lock out user
        })
      }
    })

    return credentials
  }

  /**
   * Check if WebAuthn registration is available in settings flow
   */
  const hasWebAuthnRegistration = (flow: SettingsFlow): boolean => {
    return flow.ui.nodes.some(
      (node) =>
        (node.group === 'webauthn' || node.group === 'passkey') &&
        (node.attributes.node_type === 'input' || node.attributes.node_type === 'button') &&
        ((node.attributes as UiNodeInputAttributes).name === 'webauthn_register_trigger' ||
         (node.attributes as UiNodeInputAttributes).name === 'passkey_register_trigger' ||
         (node.attributes as UiNodeInputAttributes).name === 'passkey_settings_register')
    )
  }

  /**
   * Get logout URL for browser navigation
   * Returns the Kratos logout URL with return_to parameter
   */
  const getLogoutUrl = async (returnTo?: string): Promise<string | null> => {
    try {
      const response = await kratos.createBrowserLogoutFlow({
        returnTo: returnTo
      })
      return response.data.logout_url || null
    } catch (error) {
      // 401 means already logged out
      if (kratosStatus(error) === 401) {
        return null
      }
      console.error('[Kratos] Error creating logout flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Logout from all sessions using browser navigation
   * This ensures httpOnly cookies are properly cleared
   */
  const logout = async (returnTo?: string): Promise<void> => {
    try {
      const logoutUrl = await getLogoutUrl(returnTo)
      
      if (typeof window !== 'undefined' && logoutUrl) {
        // Use browser navigation to properly clear session cookies
        window.location.href = logoutUrl
      }
    } catch (error) {
      // 401 means already logged out - just redirect
      if (kratosStatus(error) === 401) {
        if (typeof window !== 'undefined') {
          window.location.href = returnTo || '/'
        }
        return
      }
      console.error('[Kratos] Error during logout:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Logout current session and create a fresh flow
   * Used when user wants to switch accounts while already logged in
   * @param flowType - Type of flow to create after logout ('login' or 'registration')
   * @returns The newly created flow, or null if logout failed
   */
  const logoutAndCreateFlow = async (
    flowType: 'login' | 'registration'
  ): Promise<LoginFlow | RegistrationFlow | null> => {
    try {
      console.debug(`[Kratos] Logging out to create fresh ${flowType} flow`)
      
      // 1. Get logout URL
      const logoutUrl = await getLogoutUrl()
      
      // 2. Execute logout by calling the logout URL
      if (logoutUrl) {
        await $fetch(logoutUrl, {
          method: 'GET',
          credentials: 'include'
        })
        console.debug('[Kratos] Logout successful')
      }
      
      // 3. Clear auth store
      const authStore = useAuthStore()
      authStore.logout()
      
      // 4. Create new flow
      if (flowType === 'login') {
        console.debug('[Kratos] Creating fresh login flow')
        return await createLoginFlow(false)
      } else {
        console.debug('[Kratos] Creating fresh registration flow')
        return await createRegistrationFlow()
      }
    } catch (error) {
      console.error(`[Kratos] Failed to logout and create ${flowType} flow:`, error)
      throw parseKratosError(error)
    }
  }

  /**
   * Extract CSRF token from a flow's UI nodes
   */
  const getCsrfToken = (nodes: UiNode[]): string | undefined => {
    const csrfNode = nodes.find(
      (node) =>
        node.attributes.node_type === 'input' &&
        (node.attributes as UiNodeInputAttributes).name === 'csrf_token'
    )
    return csrfNode
      ? (csrfNode.attributes as UiNodeInputAttributes).value as string
      : undefined
  }

  /**
   * Value of a named input node — how Kratos hands back the trait values a
   * half-finished flow already holds (a registration resumed at the code step,
   * say). Same shape as getCsrfToken above, which is one of these.
   */
  const getNodeValue = (nodes: UiNode[], name: string): string | undefined => {
    const node = nodes.find(
      (candidate) =>
        candidate.attributes.node_type === 'input' &&
        (candidate.attributes as UiNodeInputAttributes).name === name
    )
    return node
      ? (node.attributes as UiNodeInputAttributes).value as string | undefined
      : undefined
  }

  /**
   * Check if WebAuthn/Passkey is available in the login flow
   * Checks for both 'webauthn' and 'passkey' groups
   */
  const hasWebAuthn = (flow: LoginFlow | RegistrationFlow): boolean => {
    return flow.ui.nodes.some(
      (node) =>
        (node.group === 'webauthn' || node.group === 'passkey') &&
        node.attributes.node_type === 'input'
    )
  }

  /**
   * Check if code (magic link) is available in the flow
   */
  const hasCode = (flow: LoginFlow | RegistrationFlow): boolean => {
    return flow.ui.nodes.some(
      (node) =>
        node.group === 'code' &&
        node.attributes.node_type === 'input'
    )
  }

  /**
   * Get the current authentication methods available
   */
  const getAvailableMethods = (flow: LoginFlow | RegistrationFlow): string[] => {
    const methods: string[] = []
    
    if (hasWebAuthn(flow)) {
      methods.push('webauthn')
    }
    if (hasCode(flow)) {
      methods.push('code')
    }
    
    return methods
  }

  /**
   * Parse Kratos error response into a more usable format
   */
  const parseKratosError = (error: unknown): KratosError => {
    const rejection = asKratosRejection(error)
    // Preserve the original response for flow state updates
    const response = rejection.response
    const data = response?.data

    // Handle flow errors (e.g., expired flow)
    if (data?.error) {
      const flowError = data as FlowError
      return {
        code: flowError.error.code.toString(),
        message: flowError.error.message || flowError.error.reason,
        details: { status: flowError.error.status },
        response
      }
    }

    // Handle validation errors from flow UI
    const messages = data?.ui?.messages
    if (messages) {
      const firstError = messages.find(message => message.type === 'error')
      if (firstError) {
        return {
          code: firstError.id?.toString() || 'validation_error',
          message: firstError.text,
          details: { messages },
          response
        }
      }
    }

    // Handle node-level errors
    const nodes = data?.ui?.nodes
    if (nodes) {
      for (const node of nodes) {
        const errorMsg = node.messages?.find(message => message.type === 'error')
        if (errorMsg) {
          return {
            code: errorMsg.id?.toString() || 'field_error',
            message: errorMsg.text,
            // `name` only exists on input nodes; the others carry no field.
            details: { field: 'name' in node.attributes ? node.attributes.name : undefined },
            response
          }
        }
      }
    }

    // Generic error
    return {
      code: response?.status?.toString() || 'unknown',
      message: rejection.message || 'An unexpected error occurred',
      response
    }
  }

  /**
   * Get error messages from a flow's UI nodes
   */
  const getFlowErrors = (flow: LoginFlow | RegistrationFlow | RecoveryFlow | VerificationFlow): string[] => {
    const errors: string[] = []

    // Global messages
    if (flow.ui.messages) {
      errors.push(
        ...flow.ui.messages
          .filter((m) => m.type === 'error')
          .map((m) => m.text)
      )
    }

    // Node-level messages
    for (const node of flow.ui.nodes) {
      if (node.messages) {
        errors.push(
          ...node.messages
            .filter((m) => m.type === 'error')
            .map((m) => m.text)
        )
      }
    }

    return errors
  }

  /**
   * Get success messages from a flow's UI nodes
   */
  const getFlowMessages = (flow: LoginFlow | RegistrationFlow | RecoveryFlow | VerificationFlow): string[] => {
    const messages: string[] = []

    // Global messages
    if (flow.ui.messages) {
      messages.push(
        ...flow.ui.messages
          .filter((m) => m.type === 'info' || m.type === 'success')
          .map((m) => m.text)
      )
    }

    return messages
  }

  return {
    // Session
    getSession,
    isAuthenticated,
    
    // Login
    createLoginFlow,
    getLoginFlow,
    submitLoginFlow,
    
    // Registration
    createRegistrationFlow,
    getRegistrationFlow,
    submitRegistrationFlow,
    
    // Recovery
    createRecoveryFlow,
    getRecoveryFlow,
    submitRecoveryFlow,
    
    // Verification
    createVerificationFlow,
    getVerificationFlow,
    submitVerificationFlow,
    
    // Settings
    createSettingsFlow,
    getSettingsFlow,
    submitSettingsFlow,
    getWebAuthnCredentials,
    getWebAuthnCredentialsFromFlow,
    hasWebAuthnRegistration,
    
    // Logout
    logout,
    getLogoutUrl,
    logoutAndCreateFlow,
    
    // Helpers
    getCsrfToken,
    getNodeValue,
    hasWebAuthn,
    hasCode,
    getAvailableMethods,
    getFlowErrors,
    getFlowMessages
  }
}
