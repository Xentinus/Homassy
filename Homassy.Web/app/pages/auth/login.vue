<script setup lang="ts">
import * as z from 'zod'
import type { FormSubmitEvent } from '@nuxt/ui'
import { useAuthApi } from '../../composables/api/useAuthApi'

definePageMeta({
  layout: 'public'
})

const router = useRouter()
const authApi = useAuthApi()
const authStore = useAuthStore()

const loading = ref(false)
const requestingCode = ref(false)
const code = ref<string[]>([])
const email = ref('')
const cooldownSeconds = ref(0)
let cooldownInterval: ReturnType<typeof setInterval> | null = null

// Check if user is already authenticated on mount
onMounted(async () => {
  console.debug('[Login] Checking existing authentication...')
  
  // Load tokens from cookies if not already loaded
  await authStore.loadFromCookies()

  // If already authenticated with valid user, redirect to activity
  if (authStore.isAuthenticated) {
    console.debug('[Login] User is authenticated, redirecting to activity')
    loading.value = true
    try {
      await router.push('/activity')
    } finally {
      loading.value = false
    }
    return
  }

  // If tokens exist but no user, they're likely invalid - clear them
  const { accessToken, refreshToken } = authStore.getTokensFromCookies()
  if ((accessToken || refreshToken) && !authStore.user) {
    console.debug('[Login] Tokens exist but no user - clearing invalid tokens')
    authStore.clearAuthData()
  }
  
  console.debug('[Login] Ready for login')
})

onBeforeUnmount(() => {
  if (cooldownInterval) {
    clearInterval(cooldownInterval)
  }
})

const startCooldown = () => {
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

const canRequestCode = computed(() => {
  return email.value.length > 0 && cooldownSeconds.value === 0 && !requestingCode.value
})

const { t } = useI18n()

const requestButtonText = computed(() => {
  if (cooldownSeconds.value > 0) {
    return `${t('auth.wait')} ${cooldownSeconds.value}s`
  }
  return requestingCode.value ? t('auth.sending') : t('auth.requestCode')
})

const schema = z.object({
  code: z.array(z.string()).length(8, 'Code must be 8 digits')
})

const emailSchema = z.object({
  email: z.string({ required_error: 'Email is required' }).email('Invalid email address')
})

type Schema = z.output<typeof schema>

async function onSubmit(event: FormSubmitEvent<Schema>) {
  try {
    loading.value = true
    // Join digits to a single string (no hyphen)
    const rawCode = event.data.code.join('')
    await authApi.verifyCode(email.value, rawCode)
    await router.push('/activity')
  } catch (error) {
    console.error('Login failed:', error)
  } finally {
    loading.value = false
  }
}

async function requestCode() {
  if (!canRequestCode.value) return
  
  try {
    requestingCode.value = true
    await authApi.requestCode(email.value)
    startCooldown()
  } catch (error) {
    console.error('Failed to request code:', error)
  } finally {
    requestingCode.value = false
  }
}
</script>

<template>
  <div class="flex flex-col items-center justify-center gap-4 p-4 min-h-[calc(100vh-var(--header-height))]">
    <UPageCard class="w-full max-w-md">
      <div class="space-y-6">
        <div class="flex flex-col text-center">
          <div class="mb-2">
            <UIcon name="i-lucide-mail" class="size-8 shrink-0 inline-block" />
          </div>
          <h2 class="text-xl text-pretty font-semibold text-highlighted">{{ $t('auth.signIn') }}</h2>
          <p class="mt-1 text-base text-pretty text-muted">{{ $t('auth.enterEmail') }}</p>
          <p class="mt-1 text-sm text-pretty text-muted">
            {{ $t('auth.dontHaveAccount') }}
            <ULink to="/auth/register" class="text-primary font-medium">{{ $t('auth.signUp') }}</ULink>.
          </p>
        </div>

        <UForm :schema="emailSchema" :state="{ email }" class="space-y-5">
          <UFormField name="email">
            <template #label>
              <div class="flex items-center justify-between w-full">
                <span>{{ $t('auth.emailAddress') }}</span>
                <UButton
                  size="xs"
                  variant="ghost"
                  :disabled="!canRequestCode"
                  @click="requestCode"
                >
                  {{ requestButtonText }}
                </UButton>
              </div>
            </template>
            <UInput v-model="email" type="email" :placeholder="$t('auth.enterYourEmail')" class="w-full" />
          </UFormField>
        </UForm>

        <UForm :schema="schema" :state="{ code }" class="space-y-5" @submit="onSubmit">
          <UFormField name="code" :label="$t('auth.verificationCode')">
            <div class="flex items-center justify-center gap-2">
              <UPinInput
                v-model="code"
                :length="8"
                placeholder="0"
                :ui="{ root: 'gap-1.5' }"
              />
            </div>
          </UFormField>

          <UButton type="submit" block :loading="loading">
            {{ $t('auth.verifyAndSignIn') }}
          </UButton>
        </UForm>
      </div>
    </UPageCard>
  </div>
</template>


