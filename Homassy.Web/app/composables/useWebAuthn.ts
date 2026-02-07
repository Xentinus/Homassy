/**
 * WebAuthn Composable
 * Provides WebAuthn/Passkey utilities for Kratos authentication
 */
import {
  startRegistration,
  startAuthentication,
  browserSupportsWebAuthn,
  browserSupportsWebAuthnAutofill,
  platformAuthenticatorIsAvailable
} from '@simplewebauthn/browser'
import type {
  PublicKeyCredentialCreationOptionsJSON,
  PublicKeyCredentialRequestOptionsJSON,
  RegistrationResponseJSON,
  AuthenticationResponseJSON
} from '@simplewebauthn/browser'
import type { UiNode, UiNodeInputAttributes } from '@ory/client'

export interface WebAuthnOptions {
  publicKey: PublicKeyCredentialCreationOptionsJSON | PublicKeyCredentialRequestOptionsJSON
}

export interface WebAuthnResult {
  success: boolean
  response?: RegistrationResponseJSON | AuthenticationResponseJSON
  error?: string
}

/**
 * WebAuthn composable for Passkey authentication
 */
export const useWebAuthn = () => {
  /**
   * Check if WebAuthn is supported by the browser
   */
  const isSupported = (): boolean => {
    return browserSupportsWebAuthn()
  }

  /**
   * Check if WebAuthn autofill (conditional mediation) is supported
   * This allows passkey suggestions in the browser's autofill dropdown
   */
  const supportsAutofill = async (): Promise<boolean> => {
    try {
      return await browserSupportsWebAuthnAutofill()
    } catch {
      return false
    }
  }

  /**
   * Check if a platform authenticator (like Touch ID, Face ID, Windows Hello) is available
   */
  const hasPlatformAuthenticator = async (): Promise<boolean> => {
    try {
      return await platformAuthenticatorIsAvailable()
    } catch {
      return false
    }
  }

  /**
   * Extract WebAuthn options from Kratos flow UI nodes
   */
  const extractWebAuthnOptions = (nodes: UiNode[]): WebAuthnOptions | null => {
    // Find the webauthn_register or webauthn_login node
    const webAuthnNode = nodes.find(
      (node) =>
        node.group === 'webauthn' &&
        node.attributes.node_type === 'input' &&
        ((node.attributes as UiNodeInputAttributes).name === 'webauthn_register' ||
          (node.attributes as UiNodeInputAttributes).name === 'webauthn_login')
    )

    if (!webAuthnNode) {
      return null
    }

    const attrs = webAuthnNode.attributes as UiNodeInputAttributes
    
    // The value contains the WebAuthn options as a JSON string
    if (attrs.value && typeof attrs.value === 'string') {
      try {
        const options = JSON.parse(attrs.value)
        return { publicKey: options.publicKey }
      } catch {
        console.error('[WebAuthn] Failed to parse WebAuthn options')
        return null
      }
    }

    return null
  }

  /**
   * Extract WebAuthn challenge from Kratos flow
   * The challenge is typically embedded in a script or hidden input
   */
  const extractWebAuthnChallenge = (nodes: UiNode[]): string | null => {
    // Look for webauthn_register_trigger or webauthn_login_trigger
    const triggerNode = nodes.find(
      (node) =>
        node.group === 'webauthn' &&
        node.attributes.node_type === 'input' &&
        ((node.attributes as UiNodeInputAttributes).name === 'webauthn_register_trigger' ||
          (node.attributes as UiNodeInputAttributes).name === 'webauthn_login_trigger')
    )

    if (triggerNode) {
      const attrs = triggerNode.attributes as UiNodeInputAttributes
      // The onclick attribute contains the WebAuthn options
      if (attrs.onclick) {
        try {
          // Parse the onclick which contains: __ory_webauthn_...({...}) or __oryWebAuthn...({...})
          const match = attrs.onclick.match(/(?:__ory_webauthn_\w+|__oryWebAuthn\w+)\((.*)\)/s)
          if (match && match[1]) {
            const options = JSON.parse(match[1])
            return JSON.stringify(options)
          }
        } catch {
          console.error('[WebAuthn] Failed to parse trigger onclick')
        }
      }
    }

    return null
  }

  /**
   * Get the WebAuthn script from Kratos flow nodes
   * This script contains the __ory_webauthn functions
   */
  const getWebAuthnScript = (nodes: UiNode[]): string | null => {
    const scriptNode = nodes.find(
      (node) =>
        node.group === 'webauthn' &&
        node.attributes.node_type === 'script'
    )

    if (scriptNode && 'text' in scriptNode.attributes) {
      return (scriptNode.attributes as any).text
    }

    return null
  }

  /**
   * Start WebAuthn registration ceremony
   * Call this when the user wants to register a new passkey
   */
  const register = async (options: PublicKeyCredentialCreationOptionsJSON): Promise<WebAuthnResult> => {
    if (!isSupported()) {
      return {
        success: false,
        error: 'WebAuthn is not supported by this browser'
      }
    }

    try {
      const response = await startRegistration({ optionsJSON: options })
      return {
        success: true,
        response
      }
    } catch (error: any) {
      console.error('[WebAuthn] Registration failed:', error)
      
      // Handle specific error types
      if (error.name === 'InvalidStateError') {
        return {
          success: false,
          error: 'A passkey for this account already exists on this device'
        }
      }
      if (error.name === 'NotAllowedError') {
        return {
          success: false,
          error: 'Passkey registration was cancelled or timed out'
        }
      }
      if (error.name === 'AbortError') {
        return {
          success: false,
          error: 'Passkey registration was cancelled'
        }
      }
      
      return {
        success: false,
        error: error.message || 'Passkey registration failed'
      }
    }
  }

  /**
   * Start WebAuthn authentication ceremony
   * Call this when the user wants to login with a passkey
   */
  const authenticate = async (
    options: PublicKeyCredentialRequestOptionsJSON,
    useAutofill: boolean = false
  ): Promise<WebAuthnResult> => {
    if (!isSupported()) {
      return {
        success: false,
        error: 'WebAuthn is not supported by this browser'
      }
    }

    try {
      const response = await startAuthentication({
        optionsJSON: options,
        useBrowserAutofill: useAutofill
      })
      return {
        success: true,
        response
      }
    } catch (error: any) {
      console.error('[WebAuthn] Authentication failed:', error)
      
      // Handle specific error types
      if (error.name === 'NotAllowedError') {
        return {
          success: false,
          error: 'Passkey authentication was cancelled or timed out'
        }
      }
      if (error.name === 'AbortError') {
        return {
          success: false,
          error: 'Passkey authentication was cancelled'
        }
      }
      if (error.name === 'SecurityError') {
        return {
          success: false,
          error: 'Security error during authentication. Please check your browser settings.'
        }
      }
      
      return {
        success: false,
        error: error.message || 'Passkey authentication failed'
      }
    }
  }

  /**
   * Parse Kratos WebAuthn options from the flow UI
   * Returns the options in a format suitable for @simplewebauthn/browser
   * Works for login, registration, and settings flows
   */
  const parseKratosWebAuthnOptions = (nodes: UiNode[], isRegistration: boolean): PublicKeyCredentialCreationOptionsJSON | PublicKeyCredentialRequestOptionsJSON | null => {
    // Look for the script node that contains WebAuthn configuration
    const scriptNode = nodes.find(
      (node) =>
        (node.group === 'webauthn' || node.group === 'passkey') &&
        node.attributes.node_type === 'script'
    )

    if (!scriptNode) {
      console.debug('[WebAuthn] No script node found in flow')
      return null
    }

    // Possible trigger names for different flows
    const triggerNames = isRegistration 
      ? ['webauthn_register_trigger', 'passkey_register_trigger']
      : ['webauthn_login_trigger', 'passkey_login_trigger']
    
    // Look for the trigger node that contains the options
    const triggerNode = nodes.find(
      (node) =>
        (node.group === 'webauthn' || node.group === 'passkey') &&
        (node.attributes.node_type === 'input' || node.attributes.node_type === 'button') &&
        triggerNames.includes((node.attributes as UiNodeInputAttributes).name)
    )

    if (!triggerNode) {
      console.debug('[WebAuthn] No trigger node found. Looking for options in script...')
      
      // Fallback: try to extract options from script node text
      const scriptText = (scriptNode.attributes as any).text || (scriptNode.attributes as any).src
      if (scriptText && typeof scriptText === 'string') {
        // Look for embedded publicKey options in script
        const pkMatch = scriptText.match(/publicKey\s*[=:]\s*({[\s\S]*?})\s*[,;}\n]/m)
        if (pkMatch && pkMatch[1]) {
          try {
            // This is risky - the script may contain JS objects, not valid JSON
            // Try to evaluate it safely
            const cleanJson = pkMatch[1]
              .replace(/'/g, '"')
              .replace(/(\w+):/g, '"$1":')
            const options = JSON.parse(cleanJson)
            return options
          } catch (e) {
            console.debug('[WebAuthn] Failed to parse publicKey from script:', e)
          }
        }
      }
      
      console.debug('[WebAuthn] Available nodes:', 
        nodes.filter(n => n.group === 'webauthn' || n.group === 'passkey')
          .map(n => ({ group: n.group, type: n.attributes.node_type, name: (n.attributes as any).name }))
      )
      return null
    }

    const attrs = triggerNode.attributes as UiNodeInputAttributes
    
    // Parse the onclick attribute which contains the WebAuthn options
    if (attrs.onclick) {
      try {
        // Format variations:
        // - __ory_webauthn_registration({...}) - older Kratos
        // - window.__oryWebAuthnRegistration({...}) - newer Kratos (camelCase)
        const match = attrs.onclick.match(/(?:__ory_webauthn_\w+|__oryWebAuthn\w+)\(([\s\S]*)\)/m)
        if (match && match[1]) {
          const optionsString = match[1]
          const options = JSON.parse(optionsString)
          return options.publicKey
        }
      } catch (e) {
        console.error('[WebAuthn] Failed to parse Kratos WebAuthn options from onclick:', e)
      }
    }

    // Fallback: try to extract from onclickTrigger if onclick is not present
    if ((attrs as any).onclickTrigger) {
      try {
        const trigger = (attrs as any).onclickTrigger
        const match = trigger.match(/(?:__ory_webauthn_\w+|__oryWebAuthn\w+)\(([\s\S]*)\)/m)
        if (match && match[1]) {
          const options = JSON.parse(match[1])
          return options.publicKey
        }
      } catch (e) {
        console.error('[WebAuthn] Failed to parse from onclickTrigger:', e)
      }
    }

    console.debug('[WebAuthn] Trigger node found but no onclick/options:', attrs)
    return null
  }

  /**
   * Create the response body for Kratos WebAuthn submission
   */
  const createKratosWebAuthnBody = (
    response: RegistrationResponseJSON | AuthenticationResponseJSON,
    csrfToken: string,
    isRegistration: boolean
  ): Record<string, string> => {
    const body: Record<string, string> = {
      csrf_token: csrfToken,
      method: 'webauthn'
    }

    if (isRegistration) {
      body.webauthn_register = JSON.stringify(response)
    } else {
      body.webauthn_login = JSON.stringify(response)
    }

    return body
  }

  return {
    // Feature detection
    isSupported,
    supportsAutofill,
    hasPlatformAuthenticator,
    
    // Kratos integration
    extractWebAuthnOptions,
    extractWebAuthnChallenge,
    getWebAuthnScript,
    parseKratosWebAuthnOptions,
    createKratosWebAuthnBody,
    
    // WebAuthn ceremonies
    register,
    authenticate
  }
}
