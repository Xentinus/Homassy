<template>
  <UModal :open="isOpen" @update:open="handleClose" :dismissible="!isRegistering">
    <template #title>
      {{ $t('profile.security.addPasskey') }}
    </template>

    <template #description>
      {{ $t('profile.security.addPasskeyDescription') }}
    </template>

    <template #body>
      <div class="space-y-4">
        <!-- WebAuthn not supported warning -->
        <UAlert
          v-if="!webauthnSupported"
          color="warning"
          variant="soft"
          :title="$t('auth.passkeyNotSupported')"
        />

        <!-- Display name input -->
        <div v-else>
          <label class="block text-sm font-medium mb-1">
            {{ $t('profile.security.passkeyName') }}
          </label>
          <UInput
            v-model="displayName"
            :placeholder="$t('profile.security.passkeyNamePlaceholder')"
            :disabled="isRegistering"
            class="w-full"
          />
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-1">
            {{ $t('profile.security.passkeyNameHint') }}
          </p>
        </div>

        <!-- Error message -->
        <UAlert
          v-if="errorMessage"
          color="error"
          variant="soft"
          :description="errorMessage"
          :close-button="{ icon: 'i-heroicons-x-mark', color: 'gray', variant: 'link' }"
          @close="errorMessage = null"
        />

        <!-- Platform authenticator info -->
        <div v-if="webauthnSupported && hasPlatformAuth" class="flex items-center gap-2 p-3 rounded-lg bg-blue-50 dark:bg-blue-900/20">
          <UIcon name="i-heroicons-finger-print" class="w-5 h-5 text-blue-600 dark:text-blue-400" />
          <p class="text-sm text-blue-700 dark:text-blue-300">
            {{ $t('auth.passkeyWithBiometrics') }}
          </p>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          :label="$t('common.cancel')"
          color="neutral"
          variant="outline"
          :disabled="isRegistering"
          @click="handleClose"
        />
        <UButton
          :label="$t('profile.security.registerPasskey')"
          :loading="isRegistering"
          :disabled="!webauthnSupported"
          @click="handleRegister"
        >
          <template #leading>
            <UIcon name="i-heroicons-finger-print" class="w-4 h-4" />
          </template>
        </UButton>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import type { SettingsFlow } from '@ory/client'
import { useKratos } from '~/composables/useKratos'
import { useWebAuthn } from '~/composables/useWebAuthn'

interface Props {
  isOpen: boolean
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:isOpen': [value: boolean]
  'registered': []
  'reauth-required': []
}>()

const { t } = useI18n()
const kratos = useKratos()
const webauthn = useWebAuthn()
const toast = useToast()

const displayName = ref('')
const errorMessage = ref<string | null>(null)
const isRegistering = ref(false)
const webauthnSupported = ref(false)
const hasPlatformAuth = ref(false)

// Check WebAuthn support on mount
onMounted(async () => {
  webauthnSupported.value = webauthn.isSupported()
  if (webauthnSupported.value) {
    hasPlatformAuth.value = await webauthn.hasPlatformAuthenticator()
  }
})

// Reset state when modal opens
watch(() => props.isOpen, (isOpen) => {
  if (isOpen) {
    displayName.value = ''
    errorMessage.value = null
  }
})

function handleClose() {
  if (!isRegistering.value) {
    emit('update:isOpen', false)
  }
}

async function handleRegister() {
  if (!webauthnSupported.value) return

  isRegistering.value = true
  errorMessage.value = null

  try {
    // 1. Create a settings flow
    const flow = await kratos.createSettingsFlow()
    
    // Debug: log the flow structure
    console.debug('[AddPasskeyModal] Settings flow created:', flow.id)
    console.debug('[AddPasskeyModal] Available node groups:', 
      [...new Set(flow.ui.nodes.map(n => n.group))])
    console.debug('[AddPasskeyModal] WebAuthn/Passkey nodes:', 
      flow.ui.nodes
        .filter(n => n.group === 'webauthn' || n.group === 'passkey')
        .map(n => ({ 
          group: n.group, 
          type: n.attributes.node_type, 
          name: (n.attributes as any).name,
          hasOnclick: !!(n.attributes as any).onclick
        })))

    // 2. Check if WebAuthn registration is available
    if (!kratos.hasWebAuthnRegistration(flow)) {
      console.debug('[AddPasskeyModal] WebAuthn registration not available in flow')
      throw new Error(t('profile.security.webauthnNotAvailable'))
    }

    // 3. Get WebAuthn options from the flow
    const options = webauthn.parseKratosWebAuthnOptions(flow.ui.nodes, true)
    
    if (!options) {
      console.debug('[AddPasskeyModal] Failed to parse WebAuthn options from flow')
      throw new Error(t('profile.security.webauthnOptionsError'))
    }
    
    console.debug('[AddPasskeyModal] WebAuthn options parsed successfully')

    // 4. Start WebAuthn registration ceremony
    const result = await webauthn.register(options as any)

    if (!result.success) {
      throw new Error(result.error || t('profile.security.registrationFailed'))
    }

    // 5. Submit to Kratos
    const csrfToken = kratos.getCsrfToken(flow.ui.nodes) || ''
    
    await kratos.submitSettingsFlow(flow.id, {
      method: 'webauthn',
      csrf_token: csrfToken,
      webauthn_register: JSON.stringify(result.response),
      webauthn_register_displayname: displayName.value || undefined
    })

    // Success
    toast.add({
      title: t('common.success'),
      description: t('profile.security.passkeyAdded'),
      color: 'success'
    })

    emit('registered')
    emit('update:isOpen', false)

  } catch (error: any) {
    console.error('[AddPasskeyModal] Registration failed:', error)
    
    // Check if session is too old (403 forbidden - requires re-authentication)
    const is403Error = error.code === '403' || 
                       error.response?.status === 403 ||
                       error.message?.toLowerCase().includes('forbidden') ||
                       error.message?.toLowerCase().includes('session is too old') ||
                       error.message?.toLowerCase().includes('re-authenticate')
    
    if (is403Error) {
      errorMessage.value = t('profile.security.sessionTooOld')
      // Emit event to trigger re-authentication
      emit('reauth-required')
    } else {
      errorMessage.value = error.message || t('profile.security.registrationFailed')
    }
  } finally {
    isRegistering.value = false
  }
}
</script>
