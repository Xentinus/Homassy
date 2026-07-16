<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6">
    <!-- Create Family Form -->
    <UForm @submit.prevent="onSubmit" class="space-y-4">
      <div>
        <label class="block text-sm font-medium mb-1.5">{{ $t('profile.family.nameLabel') }}</label>
        <UInput v-model="name" :placeholder="$t('profile.family.nameLabel')" required class="w-full" />
      </div>
      <div>
        <label class="block text-sm font-medium mb-1.5">{{ $t('profile.family.descriptionLabel') }}</label>
        <UInput v-model="description" :placeholder="$t('profile.family.descriptionLabel')" class="w-full" />
      </div>
      <div class="space-y-3">
        <UButton type="submit" color="primary" class="w-full" icon="i-lucide-plus-circle">
          {{ $t('profile.family.create') }}
        </UButton>
        <NuxtLink to="/profile" class="block">
          <UButton color="neutral" variant="soft" class="w-full" icon="i-lucide-x">
            {{ $t('common.cancel') }}
          </UButton>
        </NuxtLink>
      </div>
    </UForm>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useFamilyApi } from '~/composables/api/useFamilyApi'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const name = ref('')
const description = ref('')
const { createFamily } = useFamilyApi()
const router = useRouter()
const { t } = useI18n()

// Persistent header (auth layout) — back + identity.
usePageHeader(() => ({
  backTo: '/profile',
  icon: 'i-lucide-users',
  title: t('profile.family.create')
}))

async function onSubmit() {
  await createFamily({ name: name.value, description: description.value })
  router.push('/profile')
}
</script>
