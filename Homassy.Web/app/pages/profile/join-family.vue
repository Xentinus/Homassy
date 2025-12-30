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
      <UIcon name="i-lucide-users" class="text-xl text-primary" />
      <div>
        <h1 class="text-2xl font-semibold">{{ $t('profile.family.join') }}</h1>
      </div>
    </div>

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
definePageMeta({ layout: 'auth', middleware: 'auth' })
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useFamilyApi } from '~/composables/api/useFamilyApi'

const shareCode = ref('')
const { joinFamily } = useFamilyApi()
const router = useRouter()

async function onSubmit() {
  await joinFamily({ shareCode: shareCode.value })
  router.push('/profile')
}
</script>
