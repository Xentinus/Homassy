<script setup lang="ts">
/**
 * CodeLogin Component
 * Handles code-based (magic link/OTP) authentication with Kratos
 */
import * as z from 'zod'
import type { FormSubmitEvent } from '@nuxt/ui'
import type { LoginFlow } from '@ory/client'
import { useKratos } from '~/composables/useKratos'
import { useAuthStore } from '~/stores/auth'

const props = defineProps<{
  flow: LoginFlow
  email?: string
}>()

const emit = defineEmits<{
  (e: 'success'): void
  (e: 'error', error: string): void
  (e: 'emailSubmitted', email: string): void
  (e: 'flowUpdate', flow: LoginFlow): void
  (e: 'back'): void
}>()

const { t } = useI18n()
const toast = useToast()
const kratos = useKratos()
const authStore = useAuthStore()

const loading = ref(false)
const codeSent = ref(false)
const emailValue = ref(props.email || '')
const code = ref<string[]>([])
const cooldownSeconds = ref(0)
let cooldownInterval: ReturnType<typeof setInterval> | null = null

// Transform input to uppercase automatically
const handleCodeUpdate = (newCode: string[]) => {
  code.value = newCode.map(char => char?.toUpperCase() || '')
}

// Cleanup interval on unmount
onBeforeUnmount(() => {
  if (cooldownInterval) {
    clearInterval(cooldownInterval)
  }
})

// Schemas
const emailSchema = z.object({
  email: z.string({ required_error: t('validation.emailRequired') }).email(t('validation.emailInvalid'))
})

// Each code character must be non-empty
const codeSchema = z.object({
  code: z.array(z.string().min(1, t('validation.codeIncomplete'))).length(6, t('validation.codeMustBe6'))
})

type EmailSchema = z.output<typeof emailSchema>
type CodeSchema = z.output<typeof codeSchema>

// Check if code method is available in the flow
const hasCodeOption = computed(() => {
  return kratos.hasCode(props.flow)
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

// Can request code
const canRequestCode = computed(() => {
  return emailValue.value.length > 0 && cooldownSeconds.value === 0 && !loading.value
})

// Request button text
const requestButtonText = computed(() => {
  if (cooldownSeconds.value > 0) {
    return `${t('auth.wait')} ${cooldownSeconds.value}s`
  }
  return loading.value ? t('auth.sending') : t('auth.sendCode')
})

/**
 * Request code (send email)
 */
async function requestCode() {
  if (!canRequestCode.value) return

  loading.value = true

  try {
    // Get CSRF token from flow
    const csrfToken = kratos.getCsrfToken(props.flow.ui.nodes) || ''

    // Submit email to get code sent
    await kratos.submitLoginFlow(props.flow.id, {
      method: 'code',
      csrf_token: csrfToken,
      identifier: emailValue.value
    })

    // Code was sent
    codeSent.value = true
    startCooldown()
    emit('emailSubmitted', emailValue.value)

    toast.add({
      title: t('toast.codeSent'),
      description: t('toast.checkEmail'),
      color: 'success',
      icon: 'i-heroicons-envelope'
    })
  } catch (error: any) {
    console.error('[CodeLogin] Failed to request code:', error)
    
    // Check if the flow returned a new state with code input
    // This happens when Kratos responds with the code input UI
    if (error.response?.data?.ui?.nodes) {
      const hasCodeInput = error.response.data.ui.nodes.some(
        (node: any) => node.attributes?.name === 'code'
      )
      if (hasCodeInput) {
        // Emit flow update to parent
        emit('flowUpdate', error.response.data as LoginFlow)
        codeSent.value = true
        startCooldown()
        emit('emailSubmitted', emailValue.value)
        return
      }
    }

    const errorMsg = error.message || t('auth.failedToSendCode')
    toast.add({
      title: t('auth.error'),
      description: errorMsg,
      color: 'error',
      icon: 'i-heroicons-x-circle'
    })
    emit('error', errorMsg)
  } finally {
    loading.value = false
  }
}

/**
 * Verify code and complete login
 */
async function verifyCode(event: FormSubmitEvent<CodeSchema>) {
  loading.value = true

  try {
    const codeString = event.data.code.join('')
    
    // Get CSRF token
    const csrfToken = kratos.getCsrfToken(props.flow.ui.nodes) || ''

    // Submit code to complete login - identifier (email) is required
    await kratos.submitLoginFlow(props.flow.id, {
      method: 'code',
      csrf_token: csrfToken,
      code: codeString,
      identifier: emailValue.value
    })

    // Refresh auth state
    await authStore.refreshSession()

    toast.add({
      title: t('toast.loginSuccess'),
      description: t('toast.welcomeBack'),
      color: 'success',
      icon: 'i-heroicons-check-circle'
    })

    emit('success')
  } catch (error: any) {
    console.error('[CodeLogin] Code verification failed:', error)
    
    // Update flow if Kratos returned new state
    if (error.response?.data?.ui) {
      emit('flowUpdate', error.response.data as LoginFlow)
    }
    
    const errorMsg = error.message || t('auth.invalidCode')
    toast.add({
      title: t('auth.error'),
      description: errorMsg,
      color: 'error',
      icon: 'i-heroicons-x-circle'
    })
    emit('error', errorMsg)
  } finally {
    loading.value = false
  }
}

/**
 * Go back to email input
 */
function goBack() {
  codeSent.value = false
  code.value = []
  emit('back')
}
</script>

<template>
  <div class="space-y-6">
    <!-- Email Input Step -->
    <div v-if="!codeSent">
      <UForm :schema="emailSchema" :state="{ email: emailValue }" class="space-y-5">
        <UFormField name="email" :label="$t('auth.emailAddress')">
          <UInput
            v-model="emailValue"
            type="email"
            :placeholder="$t('auth.enterYourEmail')"
            class="w-full"
            size="lg"
          />
        </UFormField>

        <UButton
          block
          size="lg"
          :loading="loading"
          :disabled="!canRequestCode"
          @click="requestCode"
        >
          <template #leading>
            <UIcon name="i-heroicons-envelope" class="size-5" />
          </template>
          {{ requestButtonText }}
        </UButton>
      </UForm>
    </div>

    <!-- Code Input Step -->
    <div v-else>
      <div class="mb-4 text-center">
        <p class="text-sm text-muted">
          {{ $t('auth.codeSentTo') }} <strong>{{ emailValue }}</strong>
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
      <div class="text-center mt-4">
        <UButton
          variant="link"
          size="sm"
          :disabled="cooldownSeconds > 0 || loading"
          @click="requestCode"
        >
          {{ cooldownSeconds > 0 ? `${$t('auth.resendIn')} ${cooldownSeconds}s` : $t('auth.resendCode') }}
        </UButton>
      </div>
    </div>
  </div>
</template>
