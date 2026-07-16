<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6">
    <!-- Join Family Form -->
    <UForm @submit.prevent="onSubmit" class="space-y-4">
      <div>
        <label class="block text-sm font-medium mb-1.5">{{ $t('profile.family.codeLabel') }}</label>
        <UInput v-model="shareCode" :placeholder="$t('profile.family.codeLabel')" required class="w-full" />
      </div>
      <div class="space-y-3">
        <UButton type="submit" color="primary" class="w-full" icon="i-lucide-log-in">
          {{ $t('profile.family.join') }}
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

const shareCode = ref('')
const { requestJoin } = useFamilyApi()
const router = useRouter()
const { t } = useI18n()

// Persistent header (auth layout) — back + identity.
usePageHeader(() => ({
  backTo: '/profile',
  icon: 'i-lucide-users',
  title: t('profile.family.join')
}))

async function onSubmit() {
  const res = await requestJoin({ shareCode: shareCode.value })
  if (res.success) {
    router.push('/profile?open=family')
  }
}
</script>
