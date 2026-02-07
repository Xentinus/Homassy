<script setup lang="ts">
/**
 * Login Page (Kratos Version)
 * Handles authentication using Ory Kratos flows
 */
import type { LoginFlow } from '@ory/client'

definePageMeta({
  layout: 'public'
})

const router = useRouter()
const route = useRoute()
const kratos = useKratos()
const authStore = useAuthStore()
const { t } = useI18n()

const loading = ref(true)
const flow = ref<LoginFlow | null>(null)
const error = ref<string | null>(null)
const authMethod = ref<'passkey' | 'code'>('passkey')

// Check if user is already authenticated on mount
onMounted(async () => {
  console.debug('[Login] Checking existing authentication...')
  
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

    // Check available methods and set default
    if (flow.value) {
      const hasPasskey = kratos.hasWebAuthn(flow.value)
      const hasCode = kratos.hasCode(flow.value)
      
      if (hasPasskey) {
        authMethod.value = 'passkey'
      } else if (hasCode) {
        authMethod.value = 'code'
      }
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
 * Handle successful login
 */
async function handleLoginSuccess() {
  console.debug('[Login] Login successful, redirecting...')
  
  // Check for return URL
  const returnTo = route.query.return_to as string
  if (returnTo) {
    await router.push(returnTo)
  } else {
    await router.push('/activity')
  }
}

/**
 * Handle login error
 */
function handleLoginError(errorMsg: string) {
  error.value = errorMsg
}

/**
 * Switch to code-based login
 */
function switchToCode() {
  authMethod.value = 'code'
  error.value = null
}

/**
 * Switch to passkey login
 */
function switchToPasskey() {
  authMethod.value = 'passkey'
  error.value = null
}
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

        <!-- Auth Methods -->
        <div v-if="flow && !loading" class="space-y-6">
          <!-- Passkey Login -->
          <div v-if="authMethod === 'passkey'">
            <AuthPasskeyLogin
              :flow="flow"
              @success="handleLoginSuccess"
              @error="handleLoginError"
              @fallback="switchToCode"
            />
          </div>

          <!-- Code Login -->
          <div v-if="authMethod === 'code'">
            <AuthCodeLogin
              :flow="flow"
              @success="handleLoginSuccess"
              @error="handleLoginError"
              @flowUpdate="flow = $event"
            />
            
            <!-- Switch to passkey -->
            <div v-if="kratos.hasWebAuthn(flow)" class="text-center mt-4">
              <UButton
                variant="link"
                size="sm"
                @click="switchToPasskey"
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


