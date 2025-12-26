<script setup lang="ts">
import * as z from 'zod'
import type { FormSubmitEvent } from '@nuxt/ui'
import { useAuthApi } from '../../composables/api/useAuthApi'

definePageMeta({
  layout: 'public'
})

const router = useRouter()
const authApi = useAuthApi()

const loading = ref(false)
const code = ref<string[]>([])
const email = ref('')
const codeRequested = ref(false)

const schema = z.object({
  code: z.array(z.string()).length(8, 'Code must be 8 digits')
})

const emailSchema = z.object({
  email: z.string({ required_error: 'Email is required' }).email('Invalid email address')
})

type EmailSchema = z.output<typeof emailSchema>

type Schema = z.output<typeof schema>

async function onSubmit(event: FormSubmitEvent<Schema>) {
  try {
    loading.value = true
    // Join digits to a single string (no hyphen)
    const rawCode = event.data.code.join('')
    await authApi.verifyCode(email.value, rawCode)
    await router.push('/')
  } catch (error) {
    console.error('Login failed:', error)
  } finally {
    loading.value = false
  }
}
async function onEmailSubmit(event: FormSubmitEvent<EmailSchema>) {
  try {
    loading.value = true
    await authApi.requestCode(event.data.email)
    email.value = event.data.email
    codeRequested.value = true
  } catch (error) {
    console.error('Failed to request code:', error)
  } finally {
    loading.value = false
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

        <UForm :schema="emailSchema" :state="{ email }" class="space-y-5" @submit="onEmailSubmit">
          <UFormField name="email" label="Email Address">
            <UInput v-model="email" type="email" placeholder="Enter your email" class="w-full" />
          </UFormField>

        <UButton type="submit" block :loading="loading">
            Continue
          </UButton>
        </UForm>

        <div v-if="codeRequested" class="space-y-4 pt-2">
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
      </div>
    </UPageCard>
  </div>
</template>


