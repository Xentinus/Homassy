<script setup lang="ts">
/**
 * Email Verification Page (Kratos Version)
 * Handles email verification after registration
 */
import * as z from 'zod'
import type { FormSubmitEvent } from '@nuxt/ui'
import type { VerificationFlow } from '@ory/client'

definePageMeta({
  layout: 'public'
})

const router = useRouter()
const route = useRoute()
const kratos = useKratos()
const toast = useToast()
const { t } = useI18n()

const loading = ref(true)
const submitting = ref(false)
const flow = ref<VerificationFlow | null>(null)
const error = ref<string | null>(null)
const success = ref(false)
const email = ref('')
const code = ref<string[]>([])
const codeSent = ref(false)
const cooldownSeconds = ref(0)
let cooldownInterval: ReturnType<typeof setInterval> | null = null

// Schema for email input
const emailSchema = z.object({
  email: z.string({ required_error: t('validation.emailRequired') }).email(t('validation.emailInvalid'))
})

// Schema for code verification
const codeSchema = z.object({
  code: z.array(z.string()).length(6, t('validation.codeMustBe6'))
})

type EmailSchema = z.output<typeof emailSchema>
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

// Initialize flow on mount
onMounted(async () => {
  // Check for flow ID in URL
  const flowId = route.query.flow as string
  
  try {
    if (flowId) {
      // Get existing flow
      flow.value = await kratos.getVerificationFlow(flowId)
      
      // Check if verification was already completed
      if (flow.value.state === 'passed_challenge') {
        success.value = true
      }
    } else {
      // Create new flow
      flow.value = await kratos.createVerificationFlow()
    }

    // Check for errors in flow
    const errors = kratos.getFlowErrors(flow.value)
    if (errors.length > 0) {
      error.value = errors[0] ?? null
    }

    // Check for success messages
    const messages = kratos.getFlowMessages(flow.value)
    if (messages.length > 0 && flow.value.state === 'passed_challenge') {
      success.value = true
    }
  } catch (e: any) {
    console.error('[Verify] Failed to initialize verification flow:', e)
    error.value = e.message || t('auth.verificationFlowError')
    
    // If flow expired, create a new one
    if (e.code === '410' || e.message?.includes('expired')) {
      try {
        flow.value = await kratos.createVerificationFlow()
        error.value = null
      } catch (retryError) {
        console.error('[Verify] Failed to create new flow:', retryError)
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
 * Request verification code
 */
async function requestCode(event: FormSubmitEvent<EmailSchema>) {
  if (!flow.value) return

  submitting.value = true
  error.value = null

  try {
    const csrfToken = kratos.getCsrfToken(flow.value.ui.nodes) || ''

    await kratos.submitVerificationFlow(flow.value.id, {
      method: 'code',
      csrf_token: csrfToken,
      email: event.data.email
    })

    email.value = event.data.email
    codeSent.value = true
    startCooldown()

    toast.add({
      title: t('toast.codeSent'),
      description: t('toast.checkEmailForCode'),
      color: 'success',
      icon: 'i-heroicons-envelope'
    })
  } catch (e: any) {
    // Check if code was sent (some errors still mean code was sent)
    if (e.response?.data?.ui?.nodes?.some((node: any) => node.attributes?.name === 'code')) {
      email.value = event.data.email
      codeSent.value = true
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
 * Verify code
 */
async function verifyCode(event: FormSubmitEvent<CodeSchema>) {
  if (!flow.value) return

  submitting.value = true
  error.value = null

  try {
    const codeString = event.data.code.join('')
    const csrfToken = kratos.getCsrfToken(flow.value.ui.nodes) || ''

    await kratos.submitVerificationFlow(flow.value.id, {
      method: 'code',
      csrf_token: csrfToken,
      code: codeString
    })

    success.value = true

    toast.add({
      title: t('toast.emailVerified'),
      description: t('toast.emailVerifiedDescription'),
      color: 'success',
      icon: 'i-heroicons-check-circle'
    })
  } catch (e: any) {
    // Check if verification succeeded
    if (e.response?.data?.state === 'passed_challenge') {
      success.value = true
      toast.add({
        title: t('toast.emailVerified'),
        description: t('toast.emailVerifiedDescription'),
        color: 'success',
        icon: 'i-heroicons-check-circle'
      })
    } else {
      error.value = e.message || t('auth.invalidCode')
    }
  } finally {
    submitting.value = false
  }
}

/**
 * Go back to email input
 */
function goBack() {
  codeSent.value = false
  code.value = []
  error.value = null
}

/**
 * Resend verification code
 */
async function resendCode() {
  if (cooldownSeconds.value > 0 || !flow.value) return

  submitting.value = true
  error.value = null

  try {
    // Create a new flow and resubmit
    flow.value = await kratos.createVerificationFlow()
    
    const csrfToken = kratos.getCsrfToken(flow.value.ui.nodes) || ''

    await kratos.submitVerificationFlow(flow.value.id, {
      method: 'code',
      csrf_token: csrfToken,
      email: email.value
    })

    startCooldown()
    
    toast.add({
      title: t('toast.codeSent'),
      description: t('toast.checkEmailForCode'),
      color: 'success',
      icon: 'i-heroicons-envelope'
    })
  } catch (e: any) {
    if (e.response?.data?.ui?.nodes?.some((node: any) => node.attributes?.name === 'code')) {
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
</script>

<template>
  <div class="flex flex-col items-center justify-center gap-4 p-4 min-h-[calc(100vh-var(--header-height))]">
    <UPageCard class="w-full max-w-md">
      <div class="space-y-6">
        <!-- Header -->
        <div class="flex flex-col text-center">
          <div class="mb-2">
            <UIcon 
              :name="success ? 'i-heroicons-check-circle' : 'i-heroicons-envelope'" 
              :class="['size-8 shrink-0 inline-block', success ? 'text-green-500' : '']" 
            />
          </div>
          <h2 class="text-xl text-pretty font-semibold text-highlighted">
            {{ success ? $t('auth.emailVerified') : $t('auth.verifyEmail') }}
          </h2>
          <p v-if="!success" class="mt-1 text-base text-pretty text-muted">
            {{ $t('auth.verifyEmailDescription') }}
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

        <!-- Success State -->
        <div v-if="success && !loading" class="text-center">
          <p class="text-muted mb-4">{{ $t('auth.emailVerifiedMessage') }}</p>
          <UButton to="/auth/login" block>
            {{ $t('auth.continueToLogin') }}
          </UButton>
        </div>

        <!-- Email Input -->
        <div v-if="!loading && !success && !codeSent">
          <UForm :schema="emailSchema" :state="{ email }" class="space-y-5" @submit="requestCode">
            <UFormField name="email" :label="$t('auth.emailAddress')">
              <UInput
                v-model="email"
                type="email"
                :placeholder="$t('auth.enterYourEmail')"
                class="w-full"
                size="lg"
              />
            </UFormField>

            <UButton type="submit" block size="lg" :loading="submitting">
              <template #leading>
                <UIcon name="i-heroicons-envelope" class="size-5" />
              </template>
              {{ $t('auth.sendVerificationCode') }}
            </UButton>
          </UForm>
        </div>

        <!-- Code Input -->
        <div v-if="!loading && !success && codeSent">
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

            <UButton type="submit" block size="lg" :loading="submitting">
              {{ $t('auth.verifyEmail') }}
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

        <!-- Back to login link -->
        <div v-if="!loading && !success" class="text-center pt-4 border-t border-default">
          <ULink to="/auth/login" class="text-sm text-muted hover:text-primary">
            {{ $t('auth.backToLogin') }}
          </ULink>
        </div>
      </div>
    </UPageCard>
  </div>
</template>
