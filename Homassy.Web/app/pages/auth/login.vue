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
  // Load tokens from cookies if not already loaded
  authStore.loadFromCookies()
  
  // If user is authenticated, try to refresh token and redirect
  if (authStore.isAuthenticated) {
    try {
      loading.value = true
      // Try to refresh the access token
      await authStore.refreshAccessToken()
      // If successful, redirect to activity
      await router.push('/activity')
    } catch (error) {
      // If refresh fails, clear auth data and stay on login
      console.error('Token refresh failed:', error)
      authStore.clearAuthData()
    } finally {
      loading.value = false
    }
  }
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

const requestButtonText = computed(() => {
  if (cooldownSeconds.value > 0) {
    return `Wait ${cooldownSeconds.value}s`
  }
  return requestingCode.value ? 'Sending...' : 'Request Code'
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
          <h2 class="text-xl text-pretty font-semibold text-highlighted">Sign in</h2>
          <p class="mt-1 text-base text-pretty text-muted">Enter your email to receive a verification code</p>
          <p class="mt-1 text-sm text-pretty text-muted">
            Don't have an account?
            <ULink to="/auth/register" class="text-primary font-medium">Sign up</ULink>.
          </p>
        </div>

        <UForm :schema="emailSchema" :state="{ email }" class="space-y-5">
          <UFormField name="email">
            <template #label>
              <div class="flex items-center justify-between w-full">
                <span>Email Address</span>
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
            <UInput v-model="email" type="email" placeholder="Enter your email" class="w-full" />
          </UFormField>
        </UForm>

        <UForm :schema="schema" :state="{ code }" class="space-y-5" @submit="onSubmit">
          <UFormField name="code" label="Verification Code">
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
            Verify and Sign in
          </UButton>
        </UForm>
      </div>
    </UPageCard>
  </div>
</template>


