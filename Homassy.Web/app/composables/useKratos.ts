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
}

export interface KratosConfig {
  publicUrl: string
}

export interface KratosError {
  code: string
  message: string
  details?: Record<string, unknown>
  response?: any  // Preserve original axios response for flow state updates
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
    } catch (error: any) {
      // 401 means not authenticated - this is expected
      if (error.response?.status === 401) {
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
    } catch (error: any) {
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
    } catch (error: any) {
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
    } catch (error: any) {
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
    } catch (error: any) {
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
    } catch (error: any) {
      console.error('[Kratos] Error getting registration flow:', error)
      throw parseKratosError(error)
    }
  }

  /**
   * Submit a registration flow
   */
  const submitRegistrationFlow = async (flowId: string, body: UpdateRegistrationFlowBody): Promise<{ identity: any }> => {
    try {
      const response = await kratos.updateRegistrationFlow({
        flow: flowId,
        updateRegistrationFlowBody: body
      })
      return { identity: response.data.identity }
    } catch (error: any) {
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
    } catch (error: any) {
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
    } catch (error: any) {
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
    } catch (error: any) {
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
    } catch (error: any) {
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
    } catch (error: any) {
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
    } catch (error: any) {
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
    } catch (error: any) {
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
    } catch (error: any) {
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
    } catch (error: any) {
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
        credentials.push({
          id: attrs.value as string,
          displayName: node.meta?.label?.text || `Passkey ${credentials.length + 1}`,
          createdAt: '' // Not available in flow
        })
      }
      // Also check passkey_remove for newer Kratos versions
      if (node.group === 'passkey' && 
          node.attributes.node_type === 'input' &&
          (node.attributes as UiNodeInputAttributes).name === 'passkey_remove') {
        const attrs = node.attributes as UiNodeInputAttributes
        credentials.push({
          id: attrs.value as string,
          displayName: node.meta?.label?.text || `Passkey ${credentials.length + 1}`,
          createdAt: '' // Not available in flow
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
   * Logout from all sessions
   */
  const logout = async (): Promise<void> => {
    try {
      // First, create a logout flow to get the logout token
      const response = await kratos.createBrowserLogoutFlow()
      const logoutUrl = response.data.logout_url
      
      // Perform the logout by navigating to the logout URL
      if (typeof window !== 'undefined' && logoutUrl) {
        // Extract the logout_token from the URL
        const url = new URL(logoutUrl)
        const token = url.searchParams.get('logout_token')
        
        if (token) {
          await kratos.updateLogoutFlow({ token })
        }
      }
    } catch (error: any) {
      // 401 means already logged out
      if (error.response?.status === 401) {
        return
      }
      console.error('[Kratos] Error during logout:', error)
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
   * Check if WebAuthn/Passkey is available in the login flow
   */
  const hasWebAuthn = (flow: LoginFlow | RegistrationFlow): boolean => {
    return flow.ui.nodes.some(
      (node) =>
        node.group === 'webauthn' &&
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
  const parseKratosError = (error: any): KratosError => {
    // Preserve the original response for flow state updates
    const response = error.response
    
    // Handle flow errors (e.g., expired flow)
    if (error.response?.data?.error) {
      const flowError = error.response.data as FlowError
      return {
        code: flowError.error.code.toString(),
        message: flowError.error.message || flowError.error.reason,
        details: { status: flowError.error.status },
        response
      }
    }

    // Handle validation errors from flow UI
    if (error.response?.data?.ui?.messages) {
      const messages = error.response.data.ui.messages
      const firstError = messages.find((m: any) => m.type === 'error')
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
    if (error.response?.data?.ui?.nodes) {
      const nodes = error.response.data.ui.nodes
      for (const node of nodes) {
        if (node.messages?.length > 0) {
          const errorMsg = node.messages.find((m: any) => m.type === 'error')
          if (errorMsg) {
            return {
              code: errorMsg.id?.toString() || 'field_error',
              message: errorMsg.text,
              details: { field: (node.attributes as any)?.name },
              response
            }
          }
        }
      }
    }

    // Generic error
    return {
      code: error.response?.status?.toString() || 'unknown',
      message: error.message || 'An unexpected error occurred',
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
    
    // Helpers
    getCsrfToken,
    hasWebAuthn,
    hasCode,
    getAvailableMethods,
    getFlowErrors,
    getFlowMessages
  }
}
