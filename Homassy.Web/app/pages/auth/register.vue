<script setup lang="ts">
import * as z from 'zod'
import type { FormSubmitEvent, AuthFormField } from '@nuxt/ui'
import { useAuthApi } from '../../composables/api/useAuthApi'

definePageMeta({
  layout: 'public'
})

const router = useRouter()
const authApi = useAuthApi()

const fields: AuthFormField[] = [
  {
    name: 'email',
    type: 'email',
    label: 'Email',
    placeholder: 'Enter your email',
    required: true
  },
  {
    name: 'name',
    type: 'text',
    label: 'Name',
    placeholder: 'Enter your name',
    required: true
  },
  {
    name: 'displayName',
    type: 'text',
    label: 'Display Name (optional)',
    placeholder: 'Enter your display name',
    required: false
  }
]

const schema = z.object({
  email: z.string({ required_error: 'Email is required' }).email('Invalid email address'),
  name: z.string({ required_error: 'Name is required' }).min(1, 'Name is required'),
  displayName: z.string().optional()
})

type Schema = z.output<typeof schema>

async function onSubmit(payload: FormSubmitEvent<Schema>) {
  try {
    const { email, name, displayName } = payload.data
    
    await authApi.register({
      email,
      name,
      displayName
    })

    await router.push('/auth/login')
  } catch (error) {
    console.error('Registration failed:', error)
  }
}
</script>

<template>
  <div class="flex flex-col items-center justify-center gap-4 p-4 min-h-[calc(100vh-var(--header-height)-var(--footer-height))]">
    <UPageCard class="w-full max-w-md">
      <UAuthForm
        :schema="schema"
        :fields="fields"
        title="Create Account"
        icon="i-lucide-user-plus"
        @submit="onSubmit"
      >
        <template #description>
          Already have an account?
          <ULink to="/auth/login" class="text-primary font-medium">Sign in</ULink>.
        </template>
      </UAuthForm>
    </UPageCard>
  </div>
</template>
