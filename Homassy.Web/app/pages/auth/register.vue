<script setup lang="ts">
/**
 * Register Page (Kratos Version)
 * Handles user registration using Ory Kratos flows
 */
import * as z from 'zod'
import type { FormSubmitEvent } from '@nuxt/ui'
import type { RegistrationFlow } from '@ory/client'
import { getBrowserKratosTimezone } from '~/utils/enumMappers'

definePageMeta({
  layout: 'public'
})

const router = useRouter()
const route = useRoute()
const kratos = useKratos()
const webauthn = useWebAuthn()
const authStore = useAuthStore()
const toast = useToast()
const { t } = useI18n()

const loading = ref(true)
const submitting = ref(false)
const flow = ref<RegistrationFlow | null>(null)
const error = ref<string | null>(null)
const registrationEnabled = ref(true)

// Registration flow state
const step = ref<'details' | 'code'>('details')
const email = ref('')
const name = ref('')
const displayName = ref('')
const code = ref<string[]>([])
const cooldownSeconds = ref(0)
let cooldownInterval: ReturnType<typeof setInterval> | null = null

// Form state
const formState = reactive({
  email: '',
  name: '',
  displayName: ''
})

// Schema for registration form
const schema = z.object({
  email: z.string({ required_error: t('validation.emailRequired') }).email(t('validation.emailInvalid')),
  name: z.string({ required_error: t('validation.nameRequired') }).min(1, t('validation.nameRequired')),
  displayName: z.string().optional()
})

// Schema for code verification - each character must be non-empty
const codeSchema = z.object({
  code: z.array(z.string().min(1, t('validation.codeIncomplete'))).length(6, t('validation.codeMustBe6'))
})

type Schema = z.output<typeof schema>
type CodeSchema = z.output<typeof codeSchema>

// Transform code input to uppercase
const handleCodeUpdate = (newCode: string[]) => {
  code.value = newCode.map(char => char?.toUpperCase() || '')
}

// Cleanup interval on unmount
onBeforeUnmount(() => {
  if (cooldownInterval) {
    clearInterval(cooldownInterval)
  }
})

// Check if user is already authenticated on mount
onMounted(async () => {
  console.debug('[Register] Checking existing authentication...')

  // Check if registration is enabled before initializing Kratos flow
  try {
    const nuxtApp = useNuxtApp()
    const $api = nuxtApp.$api as any
    const configResponse = await $api('/api/v1.0/auth/config') as any
    registrationEnabled.value = configResponse?.data?.registrationEnabled ?? true
  } catch {
    // If config fetch fails, default to enabled so we don't block registration unnecessarily
    registrationEnabled.value = true
  }

  if (!registrationEnabled.value) {
    loading.value = false
    return
  }
  
  // Initialize auth state
  await authStore.initialize()

  // If already authenticated, automatically log out to proceed with registration
  if (authStore.isAuthenticated) {
    console.debug('[Register] User is already authenticated, logging out to proceed with registration')
    try {
      // Logout and create fresh registration flow
      flow.value = await kratos.logoutAndCreateFlow('registration')
      loading.value = false
      return
    } catch (logoutError) {
      console.error('[Register] Failed to logout:', logoutError)
      error.value = t('auth.switchAccountError')
      loading.value = false
      return
    }
  }

  // Check for flow ID in URL (redirect from Kratos)
  const flowId = route.query.flow as string
  
  try {
    if (flowId) {
      // Get existing flow
      flow.value = await kratos.getRegistrationFlow(flowId)
      
      // Check if flow is already in sent_email state (code was sent)
      if ((flow.value as any).state === 'sent_email') {
        // Flow already had code sent, show code input
        // Extract all stored trait values from flow
        const emailNode = flow.value.ui.nodes.find(
          (node: any) => node.attributes?.name === 'traits.email'
        )
        const nameNode = flow.value.ui.nodes.find(
          (node: any) => node.attributes?.name === 'traits.name'
        )
        const displayNameNode = flow.value.ui.nodes.find(
          (node: any) => node.attributes?.name === 'traits.display_name'
        )
        if (emailNode) {
          email.value = (emailNode.attributes as any)?.value || ''
        }
        if (nameNode) {
          name.value = (nameNode.attributes as any)?.value || ''
        }
        if (displayNameNode) {
          displayName.value = (displayNameNode.attributes as any)?.value || ''
        }
        step.value = 'code'
      }
    } else {
      // Create new flow
      flow.value = await kratos.createRegistrationFlow()
    }

    // Check for errors in flow
    const errors = kratos.getFlowErrors(flow.value)
    if (errors.length > 0) {
      error.value = errors[0]
    }
  } catch (e: any) {
    console.error('[Register] Failed to initialize registration flow:', e)
    
    // Check if error is "already logged in"
    if (e.message?.toLowerCase().includes('already logged in')) {
      // Session might have been created after initialize() - logout and retry
      console.debug('[Register] Detected session after initialize, logging out')
      try {
        flow.value = await kratos.logoutAndCreateFlow('registration')
        loading.value = false
        return
      } catch (logoutError) {
        console.error('[Register] Failed to logout:', logoutError)
        error.value = t('auth.switchAccountError')
        loading.value = false
        return
      }
    }
    
    error.value = e.message || t('auth.registrationFlowError')
    
    // If flow expired, create a new one
    if (e.code === '410' || e.message?.includes('expired')) {
      try {
        flow.value = await kratos.createRegistrationFlow()
        error.value = null
      } catch (retryError) {
        console.error('[Register] Failed to create new flow:', retryError)
      }
    }
  } finally {
    loading.value = false
  }
})

// Start cooldown timer
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

/**
 * Submit registration details (step 1)
 */
async function submitDetails(event: FormSubmitEvent<Schema>) {
  if (!flow.value) return

  submitting.value = true
  error.value = null

  try {
    const csrfToken = kratos.getCsrfToken(flow.value.ui.nodes) || ''

    // Submit registration with code method
    await kratos.submitRegistrationFlow(flow.value.id, {
      method: 'code',
      csrf_token: csrfToken,
      traits: {
        email: event.data.email,
        name: event.data.name,
        display_name: event.data.displayName || event.data.name,
        default_language: 'en',
        default_currency: 'EUR',
        default_timezone: getBrowserKratosTimezone()
      }
    })

    // If we get here without error, move to code verification
    email.value = event.data.email
    name.value = event.data.name
    displayName.value = event.data.displayName || event.data.name
    step.value = 'code'
    startCooldown()

    toast.add({
      title: t('toast.codeSent'),
      description: t('toast.checkEmailForCode'),
      color: 'success',
      icon: 'i-heroicons-envelope'
    })
  } catch (e: any) {
    console.error('[Register] Registration failed:', e)
    
    // Check if the error response contains a code input - this means code was sent
    if (e.response?.data?.ui?.nodes) {
      const hasCodeInput = e.response.data.ui.nodes.some(
        (node: any) => node.attributes?.name === 'code'
      )
      if (hasCodeInput) {
        // Update the flow with the new state (important for CSRF token!)
        flow.value = e.response.data as RegistrationFlow
        email.value = event.data.email
        name.value = event.data.name
        displayName.value = event.data.displayName || event.data.name
        step.value = 'code'
        startCooldown()
        
        toast.add({
          title: t('toast.codeSent'),
          description: t('toast.checkEmailForCode'),
          color: 'success',
          icon: 'i-heroicons-envelope'
        })
        return
      }
    }

    error.value = e.message || t('auth.registrationError')
  } finally {
    submitting.value = false
  }
}

/**
 * Verify registration code (step 2)
 */
async function verifyCode(event: FormSubmitEvent<CodeSchema>) {
  if (!flow.value) return

  submitting.value = true
  error.value = null

  try {
    const codeString = event.data.code.join('')
    const csrfToken = kratos.getCsrfToken(flow.value.ui.nodes) || ''

    // Submit code to complete registration - all traits are required
    await kratos.submitRegistrationFlow(flow.value.id, {
      method: 'code',
      csrf_token: csrfToken,
      code: codeString,
      'traits.email': email.value,
      'traits.name': name.value,
      'traits.display_name': displayName.value,
      'traits.default_language': 'en',
      'traits.default_currency': 'EUR',
      'traits.default_timezone': getBrowserKratosTimezone()
    })

    // Refresh auth state
    await authStore.refreshSession()

    toast.add({
      title: t('toast.registrationSuccess'),
      description: t('toast.accountCreated'),
      color: 'success',
      icon: 'i-heroicons-check-circle'
    })

    // Redirect to activity
    await router.push('/activity')
  } catch (e: any) {
    console.error('[Register] Code verification failed:', e)
    
    // Update flow if Kratos returned new state
    if (e.response?.data?.ui) {
      flow.value = e.response.data as RegistrationFlow
    }
    
    error.value = e.message || t('auth.invalidCode')
  } finally {
    submitting.value = false
  }
}

/**
 * Resend verification code
 */
async function resendCode() {
  if (cooldownSeconds.value > 0 || !flow.value) return

  submitting.value = true

  try {
    // Create a new registration flow and resubmit
    flow.value = await kratos.createRegistrationFlow()
    
    const csrfToken = kratos.getCsrfToken(flow.value.ui.nodes) || ''

    await kratos.submitRegistrationFlow(flow.value.id, {
      method: 'code',
      csrf_token: csrfToken,
      traits: {
        email: email.value,
        name: formState.name,
        display_name: formState.displayName || formState.name,
        default_language: 'en',
        default_currency: 'EUR',
        default_timezone: getBrowserKratosTimezone()
      }
    })

    startCooldown()
    
    toast.add({
      title: t('toast.codeSent'),
      description: t('toast.checkEmailForCode'),
      color: 'success',
      icon: 'i-heroicons-envelope'
    })
  } catch (e: any) {
    // Code sent even on "error" response - update flow with new state
    if (e.response?.data?.ui?.nodes?.some((node: any) => node.attributes?.name === 'code')) {
      flow.value = e.response.data as RegistrationFlow
      startCooldown()
      toast.add({
        title: t('toast.codeSent'),
        description: t('toast.checkEmailForCode'),
        color: 'success',
        icon: 'i-heroicons-envelope'
      })
    } else {
      error.value = e.message || t('auth.failedToSendCode')
    }
  } finally {
    submitting.value = false
  }
}

/**
 * Go back to details step
 */
function goBack() {
  step.value = 'details'
  code.value = []
  error.value = null
}
</script>

<template>
  <div class="flex flex-col items-center justify-center gap-4 p-4 min-h-[calc(100vh-var(--header-height)-var(--footer-height))]">
    <UPageCard class="w-full max-w-md">
      <div class="space-y-6">
        <!-- Header -->
        <div v-if="registrationEnabled" class="flex flex-col text-center">
          <div class="mb-2">
            <UIcon name="i-lucide-user-plus" class="size-8 shrink-0 inline-block" />
          </div>
          <h2 class="text-xl text-pretty font-semibold text-highlighted">{{ $t('auth.createAccount') }}</h2>
          <p class="mt-1 text-base text-pretty text-muted">
            {{ $t('auth.alreadyHaveAccount') }}
            <ULink to="/auth/login" class="text-primary font-medium">{{ $t('auth.signIn') }}</ULink>.
          </p>
        </div>

        <!-- Registration Disabled State -->
        <div v-if="!registrationEnabled && !loading" class="text-center space-y-4">
          <div class="flex justify-center">
            <UIcon name="i-heroicons-lock-closed" class="size-12 text-muted" />
          </div>
          <div>
            <h3 class="text-lg font-semibold text-highlighted">{{ $t('auth.registrationDisabled') }}</h3>
            <p class="mt-1 text-sm text-muted">{{ $t('auth.registrationDisabledDescription') }}</p>
          </div>
          <UButton to="/auth/login" variant="soft">
            {{ $t('auth.signIn') }}
          </UButton>
        </div>

        <!-- Loading State -->
        <div v-if="loading" class="flex justify-center py-8">
          <UIcon name="i-heroicons-arrow-path" class="size-8 animate-spin text-muted" />
        </div>

        <!-- Error Alert -->
        <UAlert
          v-if="registrationEnabled && error && !loading"
          color="error"
          variant="soft"
          :title="$t('auth.error')"
          :description="error"
          :close-button="{ icon: 'i-heroicons-x-mark', color: 'gray', variant: 'link' }"
          @close="error = null"
        />

        <!-- Step 1: Details -->
        <div v-if="registrationEnabled && !loading && step === 'details'">
          <UForm :schema="schema" :state="formState" class="space-y-5" @submit="submitDetails">
            <UFormField name="email" :label="$t('auth.email')">
              <UInput
                v-model="formState.email"
                type="email"
                :placeholder="$t('auth.enterYourEmail')"
                class="w-full"
              />
            </UFormField>

            <UFormField name="name" :label="$t('profile.name')">
              <UInput
                v-model="formState.name"
                type="text"
                :placeholder="$t('auth.enterYourName')"
                class="w-full"
              />
            </UFormField>

            <UFormField name="displayName" :label="$t('auth.displayNameOptional')">
              <UInput
                v-model="formState.displayName"
                type="text"
                :placeholder="$t('auth.enterDisplayName')"
                class="w-full"
              />
            </UFormField>

            <UButton type="submit" block :loading="submitting">
              {{ $t('auth.continue') }}
            </UButton>
          </UForm>
        </div>

        <!-- Step 2: Code Verification -->
        <div v-if="registrationEnabled && !loading && step === 'code'">
          <div class="mb-4 text-center">
            <p class="text-sm text-muted">
              {{ $t('auth.codeSentTo') }} <strong>{{ email }}</strong>
            </p>
            <UButton
              variant="link"
              size="xs"
              class="mt-1"
              @click="goBack"
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

            <UButton type="submit" block :loading="submitting">
              {{ $t('auth.verifyAndCreate') }}
            </UButton>
          </UForm>

          <!-- Resend code -->
          <div class="text-center mt-4">
            <UButton
              variant="link"
              size="sm"
              :disabled="cooldownSeconds > 0 || submitting"
              @click="resendCode"
            >
              {{ cooldownSeconds > 0 ? `${$t('auth.resendIn')} ${cooldownSeconds}s` : $t('auth.resendCode') }}
            </UButton>
          </div>
        </div>
      </div>
    </UPageCard>
  </div>
</template>
