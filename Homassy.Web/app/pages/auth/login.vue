<script setup lang="ts">
/**
 * Login Page (Kratos Version)
 * Handles authentication using Ory Kratos flows with Conditional UI
 * Supports passkey autofill and code-based login
 */
import type { LoginFlow } from '@ory/client'
import * as z from 'zod'

definePageMeta({
  layout: 'public'
})

const router = useRouter()
const route = useRoute()
const kratos = useKratos()
const webauthn = useWebAuthn()
const authStore = useAuthStore()
const { t } = useI18n()
const toast = useToast()

// Core state
const loading = ref(true)
const flow = ref<LoginFlow | null>(null)
const error = ref<string | null>(null)

// Login flow state
const email = ref('')
const step = ref<'email' | 'code'>('email')
const passkeyAvailable = ref(false)
const code = ref<string[]>([])
const cooldownSeconds = ref(0)
let cooldownInterval: ReturnType<typeof setInterval> | null = null

// Email validation schema
const emailSchema = z.object({
  email: z.string({ required_error: t('validation.emailRequired') }).email(t('validation.emailInvalid'))
})

// Code validation schema
const codeSchema = z.object({
  code: z.array(z.string().min(1, t('validation.codeIncomplete'))).length(6, t('validation.codeMustBe6'))
})

// Transform code input to uppercase
const handleCodeUpdate = (newCode: string[]) => {
  code.value = newCode.map(char => char?.toUpperCase() || '')
}

// Cleanup on unmount
onBeforeUnmount(() => {
  if (cooldownInterval) {
    clearInterval(cooldownInterval)
  }
})

// Initialize login flow
onMounted(async () => {
  console.debug('[Login] Initializing login flow...')
  
  // Initialize auth state
  await authStore.initialize()

  // Check for refresh query parameter (re-authentication required)
  const refreshRequired = route.query.refresh === 'true'

  // If already authenticated and NOT a refresh request, redirect to activity
  if (authStore.isAuthenticated && !refreshRequired) {
    console.debug('[Login] User is authenticated, redirecting to activity')
    await router.push('/activity')
    return
  }

  // Check for flow ID in URL (redirect from Kratos)
  const flowId = route.query.flow as string
  
  try {
    if (flowId) {
      // Get existing flow
      flow.value = await kratos.getLoginFlow(flowId)
    } else {
      // Create new flow (with refresh if required for re-authentication)
      flow.value = await kratos.createLoginFlow(refreshRequired)
    }

    // Check for errors in flow
    const errors = kratos.getFlowErrors(flow.value)
    if (errors.length > 0) {
      error.value = errors[0]
    }

    // Check if passkey/WebAuthn is supported in this browser
    if (flow.value && webauthn.isSupported()) {
      passkeyAvailable.value = true
    }
  } catch (e: any) {
    console.error('[Login] Failed to initialize login flow:', e)
    error.value = e.message || t('auth.loginFlowError')
    
    // If flow expired, create a new one
    if (e.code === '410' || e.message?.includes('expired')) {
      try {
        flow.value = await kratos.createLoginFlow()
        error.value = null
      } catch (retryError) {
        console.error('[Login] Failed to create new flow:', retryError)
      }
    }
  } finally {
    loading.value = false
  }
})

/**
 * Complete passkey/webauthn login after user selects a passkey
 * Kratos requires identifier (email) for webauthn login
 */
async function completePasskeyLogin(credential: any) {
  if (!flow.value || !email.value) return
  
  loading.value = true
  error.value = null
  
  try {
    // Get CSRF token from flow
    const csrfToken = kratos.getCsrfToken(flow.value.ui.nodes) || ''
    
    // Submit to Kratos using 'webauthn' method with identifier
    await kratos.submitLoginFlow(flow.value.id, {
      method: 'webauthn',
      identifier: email.value,
      csrf_token: csrfToken,
      webauthn_login: JSON.stringify(credential)
    })
    
    // Refresh auth state
    await authStore.refreshSession()
    
    toast.add({
      title: t('toast.loginSuccess'),
      description: t('toast.welcomeBack'),
      color: 'success',
      icon: 'i-heroicons-check-circle'
    })
    
    await handleLoginSuccess()
  } catch (e: any) {
    console.error('[Login] Passkey login failed:', e)
    error.value = e.message || t('auth.passkeyError')
  } finally {
    loading.value = false
  }
}

/**
 * Trigger passkey login manually (when user clicks the button)
 * Flow: 1. Create fresh flow → 2. Submit identifier with webauthn method → 3. Get challenge from response → 4. Authenticate → 5. Submit result
 */
async function triggerPasskeyLogin() {
  if (loading.value) return
  
  // Kratos requires email for webauthn
  if (!email.value) {
    error.value = t('auth.enterEmailFirst')
    return
  }
  
  loading.value = true
  error.value = null
  
  try {
    // Step 1: Create a fresh login flow for WebAuthn
    // We need a clean flow that hasn't been used for code authentication
    console.debug('[Login] Creating fresh flow for WebAuthn login')
    const freshFlow = await kratos.createLoginFlow()
    flow.value = freshFlow
    
    const csrfToken = kratos.getCsrfToken(freshFlow.ui.nodes) || ''
    
    // Step 2: Submit identifier with webauthn method
    // Kratos will return 400/422 with the WebAuthn challenge in the response
    console.debug('[Login] Requesting WebAuthn challenge for:', email.value)
    
    let webauthnFlow: LoginFlow | null = null
    
    try {
      // This will return 400 or 422 with the WebAuthn challenge
      await kratos.submitLoginFlow(freshFlow.id, {
        method: 'webauthn',
        identifier: email.value,
        csrf_token: csrfToken
      })
      // Unexpected success - shouldn't happen without webauthn_login
      console.warn('[Login] Unexpected success without WebAuthn credential')
    } catch (e: any) {
      // Handle 422 browser_location_change_required - Kratos redirects to a new flow
      if (e.response?.status === 422 && e.response?.data?.redirect_browser_to) {
        const redirectUrl = e.response.data.redirect_browser_to as string
        console.debug('[Login] Kratos redirecting to:', redirectUrl)
        
        // Extract flow ID from the URL
        const flowMatch = redirectUrl.match(/[?&]flow=([^&]+)/)
        if (flowMatch) {
          const newFlowId = flowMatch[1]
          console.debug('[Login] Fetching redirected flow:', newFlowId)
          webauthnFlow = await kratos.getLoginFlow(newFlowId)
          flow.value = webauthnFlow
        } else {
          throw new Error('Invalid redirect URL from Kratos')
        }
      }
      // Handle 400 with updated flow containing WebAuthn challenge
      else if (e.response?.data?.ui?.nodes) {
        webauthnFlow = e.response.data as LoginFlow
        flow.value = webauthnFlow
        console.debug('[Login] Got updated flow with WebAuthn options')
      } else {
        // Real error
        throw e
      }
    }
    
    if (!webauthnFlow) {
      throw new Error(t('auth.passkeyError'))
    }
    
    // Step 3: Parse WebAuthn options from the response
    const options = webauthn.parseKratosWebAuthnOptions(webauthnFlow.ui.nodes, false)
    
    if (!options) {
      // No WebAuthn options - user doesn't have a passkey
      console.debug('[Login] No WebAuthn options in response - user has no passkey')
      throw new Error(t('auth.noPasskeyRegistered'))
    }
    
    console.debug('[Login] Got WebAuthn challenge, starting authentication...')
    
    // Step 4: Perform WebAuthn authentication with browser
    const result = await webauthn.authenticate(options as any)
    
    if (!result.success || !result.response) {
      throw new Error(result.error || t('auth.passkeyError'))
    }
    
    // Step 5: Submit the WebAuthn credential to complete login
    // Use the same flow that has the challenge
    const loginCsrfToken = kratos.getCsrfToken(webauthnFlow.ui.nodes) || ''
    
    await kratos.submitLoginFlow(webauthnFlow.id, {
      method: 'webauthn',
      identifier: email.value,
      csrf_token: loginCsrfToken,
      webauthn_login: JSON.stringify(result.response)
    })
    
    // Login successful!
    await authStore.refreshSession()
    
    toast.add({
      title: t('toast.loginSuccess'),
      description: t('toast.welcomeBack'),
      color: 'success',
      icon: 'i-heroicons-check-circle'
    })
    
    await handleLoginSuccess()
    
  } catch (e: any) {
    console.error('[Login] Passkey login failed:', e)
    error.value = e.message || t('auth.passkeyError')
  } finally {
    loading.value = false
  }
}

/**
 * Handle successful login - redirect to appropriate page
 */
async function handleLoginSuccess() {
  console.debug('[Login] Login successful, redirecting...')
  
  const returnTo = route.query.return_to as string
  if (returnTo) {
    await router.push(returnTo)
  } else {
    await router.push('/activity')
  }
}

/**
 * Request code for email-based login
 */
async function requestCode() {
  if (!flow.value || !email.value) return
  
  loading.value = true
  error.value = null
  
  try {
    const csrfToken = kratos.getCsrfToken(flow.value.ui.nodes) || ''
    
    // Submit email to request code
    await kratos.submitLoginFlow(flow.value.id, {
      method: 'code',
      csrf_token: csrfToken,
      identifier: email.value
    })
    
    // Move to code step
    step.value = 'code'
    startCooldown()
    
    toast.add({
      title: t('toast.codeSent'),
      description: t('toast.checkEmail'),
      color: 'success',
      icon: 'i-heroicons-envelope'
    })
  } catch (e: any) {
    console.error('[Login] Failed to request code:', e)
    
    // Check if the flow returned a new state with code input
    if (e.response?.data?.ui?.nodes) {
      const hasCodeInput = e.response.data.ui.nodes.some(
        (node: any) => node.attributes?.name === 'code'
      )
      if (hasCodeInput) {
        flow.value = e.response.data as LoginFlow
        step.value = 'code'
        startCooldown()
        return
      }
    }
    
    error.value = e.message || t('auth.failedToSendCode')
  } finally {
    loading.value = false
  }
}

/**
 * Verify code and complete login
 */
async function verifyCode() {
  if (!flow.value || code.value.length !== 6) return
  
  loading.value = true
  error.value = null
  
  try {
    const codeString = code.value.join('')
    const csrfToken = kratos.getCsrfToken(flow.value.ui.nodes) || ''
    
    await kratos.submitLoginFlow(flow.value.id, {
      method: 'code',
      csrf_token: csrfToken,
      code: codeString,
      identifier: email.value
    })
    
    // Refresh auth state
    await authStore.refreshSession()
    
    toast.add({
      title: t('toast.loginSuccess'),
      description: t('toast.welcomeBack'),
      color: 'success',
      icon: 'i-heroicons-check-circle'
    })
    
    await handleLoginSuccess()
  } catch (e: any) {
    console.error('[Login] Code verification failed:', e)
    
    // Update flow if Kratos returned new state
    if (e.response?.data?.ui) {
      flow.value = e.response.data as LoginFlow
    }
    
    error.value = e.message || t('auth.invalidCode')
  } finally {
    loading.value = false
  }
}

/**
 * Go back to email step
 */
function goBackToEmail() {
  step.value = 'email'
  code.value = []
  error.value = null
}

/**
 * Start cooldown timer for resending code
 */
function startCooldown() {
  cooldownSeconds.value = 30
  
  if (cooldownInterval) {
    clearInterval(cooldownInterval)
  }
  
  cooldownInterval = setInterval(() => {
    cooldownSeconds.value--
    if (cooldownSeconds.value <= 0) {
      if (cooldownInterval) {
        clearInterval(cooldownInterval)
      }
    }
  }, 1000)
}

// Computed properties
const canRequestCode = computed(() => {
  return email.value.length > 0 && cooldownSeconds.value === 0 && !loading.value
})

const requestButtonText = computed(() => {
  if (cooldownSeconds.value > 0) {
    return `${t('auth.wait')} ${cooldownSeconds.value}s`
  }
  return loading.value ? t('auth.sending') : t('auth.sendCode')
})
</script>

<template>
  <div class="flex flex-col items-center justify-center gap-4 p-4 min-h-[calc(100vh-var(--header-height))]">
    <UPageCard class="w-full max-w-md">
      <div class="space-y-6">
        <!-- Header -->
        <div class="flex flex-col text-center">
          <div class="mb-2">
            <UIcon name="i-lucide-log-in" class="size-8 shrink-0 inline-block" />
          </div>
          <h2 class="text-xl text-pretty font-semibold text-highlighted">{{ $t('auth.signIn') }}</h2>
          <p class="mt-1 text-base text-pretty text-muted">{{ $t('auth.welcomeBack') }}</p>
          <p class="mt-1 text-sm text-pretty text-muted">
            {{ $t('auth.dontHaveAccount') }}
            <ULink to="/auth/register" class="text-primary font-medium">{{ $t('auth.signUp') }}</ULink>.
          </p>
        </div>

        <!-- Loading State -->
        <div v-if="loading" class="flex justify-center py-8">
          <UIcon name="i-heroicons-arrow-path" class="size-8 animate-spin text-muted" />
        </div>

        <!-- Error Alert -->
        <UAlert
          v-if="error && !loading"
          color="error"
          variant="soft"
          :title="$t('auth.error')"
          :description="error"
          :close-button="{ icon: 'i-heroicons-x-mark', color: 'gray', variant: 'link' }"
          @close="error = null"
        />

        <!-- Login Form -->
        <div v-if="flow && !loading" class="space-y-6">
          
          <!-- Email Step -->
          <div v-if="step === 'email'" class="space-y-4">
            <!-- Email input first -->
            <UForm :schema="emailSchema" :state="{ email }" class="space-y-5">
              <UFormField name="email" :label="$t('auth.emailAddress')">
                <UInput
                  v-model="email"
                  type="email"
                  name="email"
                  :placeholder="$t('auth.enterYourEmail')"
                  class="w-full"
                  size="lg"
                  autocomplete="email"
                />
              </UFormField>

              <!-- Action buttons - shown when email is entered -->
              <div v-if="email" class="space-y-3">
                <!-- Passkey button -->
                <UButton
                  v-if="passkeyAvailable"
                  block
                  color="primary"
                  size="lg"
                  :loading="loading"
                  :disabled="loading"
                  @click="triggerPasskeyLogin"
                >
                  <template #leading>
                    <UIcon name="i-heroicons-finger-print" class="size-5" />
                  </template>
                  {{ $t('auth.signInWithPasskey') }}
                </UButton>
                
                <!-- Divider -->
                <div v-if="passkeyAvailable" class="flex items-center gap-4">
                  <div class="flex-1 h-px bg-gray-200 dark:bg-gray-700"></div>
                  <span class="text-xs text-muted">{{ $t('common.or') }}</span>
                  <div class="flex-1 h-px bg-gray-200 dark:bg-gray-700"></div>
                </div>

                <!-- Send code button -->
                <UButton
                  block
                  size="lg"
                  :variant="passkeyAvailable ? 'outline' : 'solid'"
                  :loading="loading"
                  :disabled="!canRequestCode"
                  @click="requestCode"
                >
                  <template #leading>
                    <UIcon name="i-heroicons-envelope" class="size-5" />
                  </template>
                  {{ $t('auth.sendCodeToEmail') }}
                </UButton>
              </div>
              
              <!-- Hint when email is empty -->
              <p v-else class="text-sm text-muted text-center">
                {{ $t('auth.enterEmailToSeeOptions') }}
              </p>
            </UForm>
          </div>

          <!-- Code Verification Step -->
          <div v-if="step === 'code'" class="space-y-4">
            <!-- Email display and back button -->
            <div class="text-center">
              <p class="text-sm text-muted">
                {{ $t('auth.codeSentTo') }} <strong>{{ email }}</strong>
              </p>
              <UButton
                variant="link"
                size="xs"
                class="mt-1"
                @click="goBackToEmail"
              >
                {{ $t('auth.changeEmail') }}
              </UButton>
            </div>

            <UForm :schema="codeSchema" :state="{ code }" class="space-y-5" @submit="verifyCode">
              <UFormField name="code" :label="$t('auth.verificationCode')">
                <div class="flex items-center justify-center gap-2">
                  <UPinInput
                    :model-value="code"
                    :length="6"
                    placeholder="0"
                    type="text"
                    otp
                    :ui="{ root: 'gap-1.5' }"
                    @update:model-value="handleCodeUpdate"
                  />
                </div>
              </UFormField>

              <UButton
                type="submit"
                block
                size="lg"
                :loading="loading"
              >
                {{ $t('auth.verifyAndSignIn') }}
              </UButton>
            </UForm>

            <!-- Resend code -->
            <div class="text-center">
              <UButton
                variant="link"
                size="sm"
                :disabled="cooldownSeconds > 0 || loading"
                @click="requestCode"
              >
                {{ cooldownSeconds > 0 ? `${$t('auth.resendIn')} ${cooldownSeconds}s` : $t('auth.resendCode') }}
              </UButton>
            </div>
            
            <!-- Or continue with passkey -->
            <div v-if="passkeyAvailable" class="text-center pt-2 border-t border-default">
              <UButton
                variant="soft"
                size="sm"
                @click="triggerPasskeyLogin"
              >
                <template #leading>
                  <UIcon name="i-heroicons-finger-print" class="size-4" />
                </template>
                {{ $t('auth.usePasskey') }}
              </UButton>
            </div>
          </div>
        </div>

        <!-- Recovery Link -->
        <div v-if="!loading" class="text-center pt-4 border-t border-default">
          <ULink to="/auth/recovery" class="text-sm text-muted hover:text-primary">
            {{ $t('auth.forgotAccount') }}
          </ULink>
        </div>
      </div>
    </UPageCard>
  </div>
</template>