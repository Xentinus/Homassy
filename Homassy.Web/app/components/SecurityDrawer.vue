<template>
  <UDrawer
    :open="open"
    :dismissible="false"
    :ui="{
      content: 'h-[94dvh] rounded-t-2xl overflow-hidden',
      container: 'flex flex-1 flex-col min-h-0 gap-0 p-0 overflow-hidden',
      header: 'shrink-0 border-b border-default p-4 sm:px-6',
      body: 'flex-1 min-h-0 overflow-y-auto p-4 sm:p-6',
      footer: 'shrink-0 flex flex-row items-center justify-between gap-2 border-t border-default p-4 sm:px-6'
    }"
    @update:open="(value) => emit('update:open', value)"
  >
    <template #header>
      <div ref="headerEl" class="flex items-center gap-3 w-full" style="touch-action: none">
        <UIcon name="i-lucide-shield" class="h-7 w-7 shrink-0 text-primary-500" />
        <DrawerTitle class="text-xl sm:text-2xl font-semibold">{{ headerTitle }}</DrawerTitle>
        <DrawerDescription class="sr-only">{{ headerTitle }}</DrawerDescription>
        <UButton
          class="ml-auto"
          icon="i-lucide-x"
          color="neutral"
          variant="ghost"
          :aria-label="t('common.close')"
          @click="emit('update:open', false)"
        />
      </div>
    </template>

    <template #body>
      <!-- LIST -->
      <div v-if="view === 'list'" class="space-y-4">
        <p class="text-sm text-muted">{{ t('profile.security.passkeysDescription') }}</p>

        <UAlert
          v-if="!webauthnSupported"
          color="warning"
          variant="soft"
          icon="i-heroicons-exclamation-triangle"
          :title="t('auth.passkeyNotSupported')"
          :description="t('profile.security.passkeyNotSupportedDescription')"
        />

        <template v-else>
          <template v-if="loading">
            <USkeleton v-for="i in 2" :key="i" class="h-20 w-full rounded-xl mb-3" />
          </template>
          <template v-else>
            <div
              v-if="passkeys.length === 0"
              class="text-center py-8 bg-gray-50 dark:bg-gray-800/50 rounded-xl"
            >
              <UIcon name="i-heroicons-finger-print" class="w-12 h-12 mx-auto text-gray-400 dark:text-gray-600 mb-3" />
              <p class="text-gray-600 dark:text-gray-400 mb-1">{{ t('profile.security.noPasskeys') }}</p>
              <p class="text-sm text-gray-500 dark:text-gray-500">{{ t('profile.security.noPasskeysHint') }}</p>
            </div>
            <div v-else class="space-y-3">
              <PasskeyCard
                v-for="passkey in passkeys"
                :key="passkey.id"
                :passkey="passkey"
                @delete="requestDelete"
              />
            </div>
          </template>
        </template>

        <UAlert
          v-if="errorMessage"
          color="error"
          variant="soft"
          :description="errorMessage"
        />
      </div>

      <!-- ADD -->
      <div v-else-if="view === 'add'" class="space-y-4">
        <UAlert
          v-if="!webauthnSupported"
          color="warning"
          variant="soft"
          :title="t('auth.passkeyNotSupported')"
        />
        <div v-else>
          <label class="block text-sm font-medium mb-1">{{ t('profile.security.passkeyName') }}</label>
          <UInput
            v-model="displayName"
            :placeholder="t('profile.security.passkeyNamePlaceholder')"
            :disabled="isRegistering"
            class="w-full"
          />
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-1">{{ t('profile.security.passkeyNameHint') }}</p>
        </div>

        <div v-if="webauthnSupported && hasPlatformAuth" class="flex items-center gap-2 p-3 rounded-lg bg-blue-50 dark:bg-blue-900/20">
          <UIcon name="i-heroicons-finger-print" class="w-5 h-5 text-blue-600 dark:text-blue-400" />
          <p class="text-sm text-blue-700 dark:text-blue-300">{{ t('auth.passkeyWithBiometrics') }}</p>
        </div>

        <UAlert
          v-if="errorMessage"
          color="error"
          variant="soft"
          :description="errorMessage"
        />
      </div>

      <!-- CONFIRM DELETE -->
      <div v-else class="space-y-3">
        <p class="text-sm">{{ t('profile.security.deletePasskeyWarning') }}</p>
        <div class="p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50">
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ t('common.name') }}:</span>
          <span class="text-sm ml-2">{{ pendingDelete?.displayName }}</span>
        </div>
        <UAlert
          v-if="errorMessage"
          color="error"
          variant="soft"
          :description="errorMessage"
        />
      </div>
    </template>

    <template #footer>
      <!-- LIST footer -->
      <template v-if="view === 'list'">
        <UButton :label="t('common.close')" color="neutral" variant="ghost" @click="emit('update:open', false)" />
        <UButton
          v-if="webauthnSupported"
          :label="t('profile.security.addPasskey')"
          color="primary"
          icon="i-lucide-plus"
          @click="startAdd"
        />
      </template>

      <!-- ADD footer -->
      <template v-else-if="view === 'add'">
        <UButton :label="t('common.back')" color="neutral" variant="ghost" icon="i-lucide-arrow-left" :disabled="isRegistering" @click="() => { view = 'list' }" />
        <UButton
          :label="t('profile.security.registerPasskey')"
          color="primary"
          icon="i-heroicons-finger-print"
          :loading="isRegistering"
          :disabled="!webauthnSupported"
          @click="register"
        />
      </template>

      <!-- CONFIRM DELETE footer -->
      <template v-else>
        <UButton :label="t('common.cancel')" color="neutral" variant="ghost" :disabled="isDeleting" @click="() => { view = 'list' }" />
        <UButton :label="t('common.delete')" color="error" icon="i-lucide-trash-2" :loading="isDeleting" @click="confirmDelete" />
      </template>
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { DrawerTitle, DrawerDescription } from 'vaul-vue'
import type { SettingsFlow } from '@ory/client'
import { useKratos, type WebAuthnCredential } from '~/composables/useKratos'
import { useWebAuthn } from '~/composables/useWebAuthn'
import PasskeyCard from '~/components/security/PasskeyCard.vue'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const { t } = useI18n()
const kratos = useKratos()
const webauthn = useWebAuthn()
const toast = useToast()

type View = 'list' | 'add' | 'confirm-delete'
const view = ref<View>('list')
const loading = ref(true)
const isRegistering = ref(false)
const isDeleting = ref(false)
const errorMessage = ref<string | null>(null)
const webauthnSupported = ref(false)
const hasPlatformAuth = ref(false)
const passkeys = ref<WebAuthnCredential[]>([])
const displayName = ref('')
const pendingDelete = ref<WebAuthnCredential | null>(null)
// Held so the header drag-gesture can be disabled during a ceremony.
const currentFlow = ref<SettingsFlow | null>(null)

const headerEl = ref<HTMLElement | null>(null)
useDrawerDragToClose(headerEl, {
  onClose: () => emit('update:open', false),
  disabled: () => isRegistering.value || isDeleting.value
})

const headerTitle = computed(() => {
  if (view.value === 'add') return t('profile.security.addPasskey')
  if (view.value === 'confirm-delete') return t('profile.security.deletePasskey')
  return t('profile.security.title')
})

// Load on open (fresh Kratos settings flow lists the credentials).
watch(() => props.open, async (isOpen) => {
  if (!isOpen) return
  view.value = 'list'
  errorMessage.value = null
  displayName.value = ''
  pendingDelete.value = null
  webauthnSupported.value = webauthn.isSupported()
  if (webauthnSupported.value) {
    hasPlatformAuth.value = await webauthn.hasPlatformAuthenticator()
  }
  await loadPasskeys()
})

async function loadPasskeys() {
  loading.value = true
  errorMessage.value = null
  try {
    const flow = await kratos.createSettingsFlow()
    currentFlow.value = flow
    passkeys.value = kratos.getWebAuthnCredentialsFromFlow(flow)
  } catch (error: unknown) {
    console.error('[Security] Failed to load passkeys:', error)
    errorMessage.value = (error as Error)?.message || t('profile.security.loadFailed')
  } finally {
    loading.value = false
  }
}

function startAdd() {
  errorMessage.value = null
  displayName.value = ''
  view.value = 'add'
}

async function register() {
  if (!webauthnSupported.value) return
  isRegistering.value = true
  errorMessage.value = null
  try {
    const flow = await kratos.createSettingsFlow()
    if (!kratos.hasWebAuthnRegistration(flow)) {
      throw new Error(t('profile.security.webauthnNotAvailable'))
    }
    const options = webauthn.parseKratosWebAuthnOptions(flow.ui.nodes, true)
    if (!options) throw new Error(t('profile.security.webauthnOptionsError'))

    const result = await webauthn.register(options as never)
    if (!result.success) throw new Error(result.error || t('profile.security.registrationFailed'))

    const csrfToken = kratos.getCsrfToken(flow.ui.nodes) || ''
    await kratos.submitSettingsFlow(flow.id, {
      method: 'webauthn',
      csrf_token: csrfToken,
      webauthn_register: JSON.stringify(result.response),
      webauthn_register_displayname: displayName.value || undefined
    })

    toast.add({ title: t('common.success'), description: t('profile.security.passkeyAdded'), color: 'success' })
    await loadPasskeys()
    view.value = 'list'
  } catch (error: unknown) {
    const err = error as { code?: string, response?: { status?: number }, message?: string }
    const is403 = err.code === '403' || err.response?.status === 403 ||
      err.message?.toLowerCase().includes('forbidden') ||
      err.message?.toLowerCase().includes('session is too old') ||
      err.message?.toLowerCase().includes('re-authenticate')
    if (is403) {
      errorMessage.value = t('profile.security.sessionTooOld')
      await reauth()
    } else {
      errorMessage.value = err.message || t('profile.security.registrationFailed')
    }
  } finally {
    isRegistering.value = false
  }
}

function requestDelete(id: string) {
  pendingDelete.value = passkeys.value.find(p => p.id === id) ?? null
  if (!pendingDelete.value) return
  errorMessage.value = null
  view.value = 'confirm-delete'
}

async function confirmDelete() {
  if (!pendingDelete.value) return
  isDeleting.value = true
  errorMessage.value = null
  try {
    const flow = await kratos.createSettingsFlow()
    const csrfToken = kratos.getCsrfToken(flow.ui.nodes) || ''
    await kratos.submitSettingsFlow(flow.id, {
      method: 'webauthn',
      csrf_token: csrfToken,
      webauthn_remove: pendingDelete.value.id
    })
    toast.add({ title: t('common.success'), description: t('profile.security.passkeyDeleted'), color: 'success' })
    await loadPasskeys()
    view.value = 'list'
  } catch (error: unknown) {
    const msg = (error as Error)?.message || ''
    const isLastCredential = msg.toLowerCase().includes('lock you out')
    if (isLastCredential) {
      errorMessage.value = t('profile.security.cannotDeleteLastPasskey')
      toast.add({ title: t('common.error'), description: t('profile.security.cannotDeleteLastPasskey'), color: 'warning' })
    } else {
      errorMessage.value = msg || t('profile.security.deleteFailed')
      toast.add({ title: t('common.error'), description: t('profile.security.deleteFailed'), color: 'error' })
    }
  } finally {
    isDeleting.value = false
  }
}

async function reauth() {
  toast.add({
    title: t('profile.security.reauthRequired'),
    description: t('profile.security.reauthDescription'),
    color: 'warning'
  })
  const returnTo = encodeURIComponent('/profile?open=security')
  await navigateTo(`/auth/login?refresh=true&return_to=${returnTo}`)
}
</script>
