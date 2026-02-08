<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6">
    <!-- Header with back button -->
    <div class="flex items-center gap-3">
      <NuxtLink to="/profile">
        <UButton
          icon="i-lucide-arrow-left"
          color="neutral"
          variant="ghost"
        />
      </NuxtLink>
      <UIcon name="i-lucide-shield" class="text-xl text-primary" />
      <div>
        <h1 class="text-2xl font-semibold">{{ $t('profile.security.title') }}</h1>
      </div>
    </div>

    <!-- Loading state -->
    <template v-if="loading">
      <USkeleton class="h-8 w-1/2 rounded mb-2 mt-4" />
      <USkeleton class="h-4 w-2/3 rounded mb-4" />
      <USkeleton class="h-20 w-full rounded-lg mb-2" />
      <USkeleton class="h-20 w-full rounded-lg mb-2" />
      <USkeleton class="h-10 w-full rounded-lg mt-6" />
    </template>

    <div v-else class="space-y-8 mt-4">
      <!-- Passkeys Section -->
      <div class="space-y-4">
        <div class="flex items-center gap-3">
          <UIcon name="i-heroicons-finger-print" class="text-2xl text-primary" />
          <div>
            <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">
              {{ $t('profile.security.passkeysSection') }}
            </h3>
            <p class="text-xs text-gray-500 dark:text-gray-400">
              {{ $t('profile.security.passkeysDescription') }}
            </p>
          </div>
        </div>

        <!-- WebAuthn not supported warning -->
        <UAlert
          v-if="!webauthnSupported"
          color="warning"
          variant="soft"
          icon="i-heroicons-exclamation-triangle"
          :title="$t('auth.passkeyNotSupported')"
          :description="$t('profile.security.passkeyNotSupportedDescription')"
        />

        <!-- Passkeys list -->
        <div v-else class="space-y-3">
          <!-- Empty state -->
          <div
            v-if="passkeys.length === 0"
            class="text-center py-8 bg-gray-50 dark:bg-gray-800/50 rounded-xl"
          >
            <UIcon
              name="i-heroicons-finger-print"
              class="w-12 h-12 mx-auto text-gray-400 dark:text-gray-600 mb-3"
            />
            <p class="text-gray-600 dark:text-gray-400 mb-1">
              {{ $t('profile.security.noPasskeys') }}
            </p>
            <p class="text-sm text-gray-500 dark:text-gray-500">
              {{ $t('profile.security.noPasskeysHint') }}
            </p>
          </div>

          <!-- Passkey cards -->
          <PasskeyCard
            v-for="passkey in passkeys"
            :key="passkey.id"
            :passkey="passkey"
            @delete="handleDeletePasskey"
          />

          <!-- Add passkey button -->
          <UButton
            block
            color="primary"
            variant="soft"
            icon="i-lucide-plus"
            :disabled="isDeleting"
            @click="isAddModalOpen = true"
          >
            {{ $t('profile.security.addPasskey') }}
          </UButton>
        </div>
      </div>

      <!-- Error display -->
      <UAlert
        v-if="errorMessage"
        color="error"
        variant="soft"
        :description="errorMessage"
        :close-button="{ icon: 'i-heroicons-x-mark', color: 'gray', variant: 'link' }"
        @close="errorMessage = null"
      />
    </div>

    <!-- Add Passkey Modal -->
    <AddPasskeyModal
      :is-open="isAddModalOpen"
      @update:is-open="isAddModalOpen = $event"
      @registered="handlePasskeyRegistered"
      @reauth-required="handleReauthRequired"
    />
  </div>
</template>

<script setup lang="ts">
import type { SettingsFlow } from '@ory/client'
import { useKratos, type WebAuthnCredential } from '~/composables/useKratos'
import { useWebAuthn } from '~/composables/useWebAuthn'
import PasskeyCard from '~/components/security/PasskeyCard.vue'
import AddPasskeyModal from '~/components/security/AddPasskeyModal.vue'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { t } = useI18n()
const kratos = useKratos()
const webauthn = useWebAuthn()
const toast = useToast()

// State
const loading = ref(true)
const isDeleting = ref(false)
const isAddModalOpen = ref(false)
const errorMessage = ref<string | null>(null)
const webauthnSupported = ref(false)
const passkeys = ref<WebAuthnCredential[]>([])
const currentFlow = ref<SettingsFlow | null>(null)

// Check WebAuthn support and load passkeys on mount
onMounted(async () => {
  webauthnSupported.value = webauthn.isSupported()
  await loadPasskeys()
})

async function loadPasskeys() {
  loading.value = true
  errorMessage.value = null
  
  try {
    // Create a settings flow to get the list of credentials
    const flow = await kratos.createSettingsFlow()
    currentFlow.value = flow
    
    // Extract WebAuthn credentials from the flow
    passkeys.value = kratos.getWebAuthnCredentialsFromFlow(flow)
  } catch (error: any) {
    console.error('[Security] Failed to load passkeys:', error)
    errorMessage.value = error.message || t('profile.security.loadFailed')
  } finally {
    loading.value = false
  }
}

async function handleDeletePasskey(credentialId: string) {
  isDeleting.value = true
  errorMessage.value = null
  
  try {
    // Create a fresh settings flow for the delete operation
    const flow = await kratos.createSettingsFlow()
    const csrfToken = kratos.getCsrfToken(flow.ui.nodes) || ''
    
    // Submit the delete request
    await kratos.submitSettingsFlow(flow.id, {
      method: 'webauthn',
      csrf_token: csrfToken,
      webauthn_remove: credentialId
    })
    
    toast.add({
      title: t('common.success'),
      description: t('profile.security.passkeyDeleted'),
      color: 'success'
    })
    
    // Reload the list
    await loadPasskeys()
  } catch (error: any) {
    console.error('[Security] Failed to delete passkey:', error)
    
    // Check for "last credential" error from Kratos
    const errorMsg = error.message || error.response?.data?.error?.message || ''
    const isLastCredentialError = errorMsg.toLowerCase().includes('lock you out') ||
                                   errorMsg.toLowerCase().includes('would lock you out')
    
    if (isLastCredentialError) {
      errorMessage.value = t('profile.security.cannotDeleteLastPasskey')
      toast.add({
        title: t('common.error'),
        description: t('profile.security.cannotDeleteLastPasskey'),
        color: 'warning'
      })
    } else {
      errorMessage.value = error.message || t('profile.security.deleteFailed')
      toast.add({
        title: t('common.error'),
        description: t('profile.security.deleteFailed'),
        color: 'error'
      })
    }
  } finally {
    isDeleting.value = false
  }
}

async function handlePasskeyRegistered() {
  // Reload the list to show the new passkey
  await loadPasskeys()
}

async function handleReauthRequired() {
  // Close the modal
  isAddModalOpen.value = false
  
  // Show toast explaining what's happening
  toast.add({
    title: t('profile.security.reauthRequired'),
    description: t('profile.security.reauthDescription'),
    color: 'warning'
  })
  
  // Redirect to login with refresh=true to force re-authentication
  // After successful login, user will be redirected back here
  const returnTo = encodeURIComponent('/profile/security')
  await navigateTo(`/auth/login?refresh=true&return_to=${returnTo}`)
}
</script>
