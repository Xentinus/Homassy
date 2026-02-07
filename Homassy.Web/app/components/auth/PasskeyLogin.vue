<script setup lang="ts">
/**
 * PasskeyLogin Component
 * Handles WebAuthn/Passkey authentication with Kratos
 */
import type { LoginFlow } from '@ory/client'
import { useKratos } from '~/composables/useKratos'
import { useWebAuthn } from '~/composables/useWebAuthn'
import { useAuthStore } from '~/stores/auth'

const props = defineProps<{
  flow: LoginFlow
}>()

const emit = defineEmits<{
  (e: 'success'): void
  (e: 'error', error: string): void
  (e: 'fallback'): void
}>()

const { t } = useI18n()
const kratos = useKratos()
const webauthn = useWebAuthn()
const authStore = useAuthStore()

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const isSupported = ref(false)
const hasPlatformAuth = ref(false)

// Check WebAuthn support on mount
onMounted(async () => {
  isSupported.value = webauthn.isSupported()
  if (isSupported.value) {
    hasPlatformAuth.value = await webauthn.hasPlatformAuthenticator()
  }
})

// Check if WebAuthn is available in the flow
const hasWebAuthnOption = computed(() => {
  return kratos.hasWebAuthn(props.flow)
})

/**
 * Start WebAuthn authentication
 */
async function authenticate() {
  if (!hasWebAuthnOption.value || !isSupported.value) {
    emit('fallback')
    return
  }

  loading.value = true
  errorMessage.value = null

  try {
    // Get WebAuthn options from flow
    const options = webauthn.parseKratosWebAuthnOptions(props.flow.ui.nodes, false)
    
    if (!options) {
      // No WebAuthn options available - fallback to code login
      console.debug('[PasskeyLogin] No WebAuthn options in flow, falling back to code')
      emit('fallback')
      return
    }

    // Start WebAuthn authentication ceremony
    const result = await webauthn.authenticate(options as any)

    if (!result.success) {
      throw new Error(result.error || 'Passkey authentication failed')
    }

    // Get CSRF token from flow
    const csrfToken = kratos.getCsrfToken(props.flow.ui.nodes) || ''

    // Submit to Kratos - identifier is resolved from the passkey credential
    await kratos.submitLoginFlow(props.flow.id, {
      method: 'webauthn',
      identifier: '', // Resolved from passkey credential
      csrf_token: csrfToken,
      webauthn_login: JSON.stringify(result.response)
    })

    // Refresh auth state
    await authStore.refreshSession()

    emit('success')
  } catch (error: any) {
    console.error('[PasskeyLogin] Authentication failed:', error)
    const errMsg = error.message || t('auth.passkeyError')
    errorMessage.value = errMsg
    emit('error', errMsg)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <!-- Passkey Button -->
    <UButton
      v-if="hasWebAuthnOption && isSupported"
      block
      color="primary"
      variant="soft"
      size="lg"
      :loading="loading"
      :disabled="loading"
      @click="authenticate"
    >
      <template #leading>
        <UIcon name="i-heroicons-finger-print" class="size-5" />
      </template>
      {{ $t('auth.signInWithPasskey') }}
    </UButton>

    <!-- Passkey not supported message -->
    <div
      v-else-if="hasWebAuthnOption && !isSupported"
      class="p-4 rounded-lg bg-amber-50 dark:bg-amber-900/20 text-amber-800 dark:text-amber-200 text-sm"
    >
      <div class="flex items-start gap-3">
        <UIcon name="i-heroicons-exclamation-triangle" class="size-5 shrink-0 mt-0.5" />
        <p>{{ $t('auth.passkeyNotSupported') }}</p>
      </div>
    </div>

    <!-- Platform authenticator info -->
    <p
      v-if="hasWebAuthnOption && isSupported && hasPlatformAuth"
      class="text-xs text-muted text-center"
    >
      {{ $t('auth.passkeyWithBiometrics') }}
    </p>

    <!-- Error message -->
    <UAlert
      v-if="errorMessage"
      color="error"
      variant="soft"
      :title="$t('auth.error')"
      :description="errorMessage"
      :close-button="{ icon: 'i-heroicons-x-mark', color: 'gray', variant: 'link' }"
      @close="errorMessage = null"
    />

    <!-- Fallback link -->
    <div v-if="hasWebAuthnOption" class="text-center">
      <UButton
        variant="link"
        size="sm"
        @click="emit('fallback')"
      >
        {{ $t('auth.useEmailCode') }}
      </UButton>
    </div>
  </div>
</template>
