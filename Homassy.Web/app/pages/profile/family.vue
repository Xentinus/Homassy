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
        <h1 class="text-2xl font-semibold">{{ $t('profile.family.title') }}</h1>
      </div>
    </div>

    <template v-if="loading">
      <USkeleton class="h-8 w-1/2 rounded mb-2 mt-4" />
      <USkeleton class="h-4 w-2/3 rounded mb-4" />
      <USkeleton class="h-16 w-16 rounded-full mb-2" />
      <USkeleton class="h-6 w-32 mb-2" />
      <USkeleton class="h-4 w-40 mb-2" />
      <USkeleton class="h-4 w-24 mb-2" />
      <USkeleton class="h-8 w-1/3 mb-2" />
      <USkeleton class="h-10 w-full rounded-lg mt-6" />
      <USkeleton class="h-8 w-1/3 rounded mt-8 mb-4" />
      <USkeleton class="h-20 w-full rounded-lg mb-3" />
      <USkeleton class="h-20 w-full rounded-lg mb-3" />
      <USkeleton class="h-20 w-full rounded-lg" />
    </template>
    <div v-else-if="family" class="space-y-8">
      <!-- Family Information Section -->
      <div class="space-y-4">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-home" class="text-2xl text-primary" />
          <div>
            <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ $t('profile.family.infoSection') }}</h3>
            <p class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.family.infoDescription') }}</p>
          </div>
        </div>
        
        <div class="flex items-center gap-4">
          <div v-if="family.familyPictureBase64">
            <UAvatar :src="`data:image/jpeg;base64,${family.familyPictureBase64}`" :alt="family.name" class="h-16 w-16" />
          </div>
          <div class="flex-1">
            <div class="text-lg font-semibold">{{ family.name }}</div>
            <div v-if="family.description" class="text-sm text-gray-500 dark:text-gray-400">
              {{ family.description }}
            </div>
          </div>
        </div>

        <div class="pt-2">
          <div class="text-xs text-gray-500 dark:text-gray-400 mb-1">
            {{ $t('profile.family.codeLabel') }}
          </div>
          <div class="text-2xl font-extrabold text-primary-600 tracking-widest" style="text-transform: uppercase;">
            {{ family.shareCode }}
          </div>
        </div>

        <UButton color="error" variant="soft" class="w-full flex items-center justify-center gap-2" @click="onLeaveFamily" :loading="isLeavingFamily" :disabled="isLeavingFamily">
          <UIcon name="i-lucide-log-out" class="h-4 w-4" />
          {{ $t('profile.family.leave') }}
        </UButton>
      </div>

      <!-- Family Members Section -->
      <div class="space-y-4">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-users" class="text-2xl text-primary" />
          <div>
            <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ $t('profile.family.members') }}</h3>
            <p class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.family.membersDescription') }}</p>
          </div>
        </div>
        
        <div class="space-y-3">
          <div v-for="member in members" :key="member.publicId" class="flex items-center gap-3 p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50">
            <UAvatar
              :src="member.profilePictureBase64 ? `data:image/jpeg;base64,${member.profilePictureBase64}` : undefined"
              :alt="member.displayName || member.name"
              class="h-12 w-12"
            />
            <div class="flex-1">
              <div class="flex items-center gap-2">
                <span class="font-medium">{{ member.displayName || member.name }}</span>
                <div v-if="member.isCurrentUser" class="flex items-center gap-1 px-2 py-0.5 rounded-md border bg-primary-50 dark:bg-primary-900/20 border-primary-200 dark:border-primary-800">
                  <span class="text-xs font-semibold text-primary-700 dark:text-primary-300">{{ $t('profile.family.currentUserBadge') }}</span>
                </div>
              </div>
              <div class="text-sm text-gray-500 dark:text-gray-400">
                {{ $t('profile.family.lastSeen') }}: {{ formatTimestamp(member.lastLoginAt) }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
    <div v-else class="space-y-6">
      <!-- Show buttons when no mode selected -->
      <div v-if="!mode" class="grid grid-cols-1 md:grid-cols-2 gap-6">
        <!-- Create Family Button -->
        <div @click="mode = 'create'" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer flex items-start gap-3 h-full">
          <UIcon name="i-lucide-plus-circle" class="h-7 w-7 text-primary-500 mr-2 flex-shrink-0 self-center" />
          <div class="flex-1">
            <h2 class="text-base font-semibold mb-1">{{ $t('profile.family.create') }}</h2>
            <div class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.family.createFamilyCard') }}</div>
          </div>
          <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
        </div>

        <!-- Join Family Button -->
        <div @click="mode = 'join'" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer flex items-start gap-3 h-full">
          <UIcon name="i-lucide-user-plus" class="h-7 w-7 text-primary-500 mr-2 flex-shrink-0 self-center" />
          <div class="flex-1">
            <h2 class="text-base font-semibold mb-1">{{ $t('profile.family.join') }}</h2>
            <div class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.family.joinFamilyCard') }}</div>
          </div>
          <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
        </div>
      </div>

      <!-- Create Family Form -->
      <div v-if="mode === 'create'" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-6">
        <div class="flex items-center gap-3 mb-6">
          <UIcon name="i-lucide-plus-circle" class="text-xl text-primary" />
          <h2 class="text-xl font-semibold">{{ $t('profile.family.create') }}</h2>
        </div>
        <UForm @submit.prevent="onCreateFamily" class="space-y-4">
          <div>
            <label class="block text-sm font-medium mb-1.5">{{ $t('profile.family.nameLabel') }}</label>
            <UInput v-model="familyName" :placeholder="$t('profile.family.nameLabel')" required class="w-full" />
          </div>
          <div>
            <label class="block text-sm font-medium mb-1.5">{{ $t('profile.family.descriptionLabel') }}</label>
            <UInput v-model="familyDescription" :placeholder="$t('profile.family.descriptionLabel')" class="w-full" />
          </div>
          <div class="space-y-3">
            <UButton type="submit" color="primary" class="w-full" icon="i-lucide-plus-circle">
              {{ $t('profile.family.create') }}
            </UButton>
            <UButton @click="mode = null" color="neutral" variant="soft" class="w-full" icon="i-lucide-x">
              {{ $t('common.cancel') }}
            </UButton>
          </div>
        </UForm>
      </div>

      <!-- Join Family Form -->
      <div v-if="mode === 'join'" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-6">
        <div class="flex items-center gap-3 mb-6">
          <UIcon name="i-lucide-user-plus" class="text-xl text-primary" />
          <h2 class="text-xl font-semibold">{{ $t('profile.family.join') }}</h2>
        </div>
        <UForm @submit.prevent="onJoinFamily" class="space-y-4">
          <div>
            <label class="block text-sm font-medium mb-1.5">{{ $t('profile.family.codeLabel') }}</label>
            <UInput v-model="shareCode" :placeholder="$t('profile.family.codeLabel')" required class="w-full" />
          </div>
          <div class="space-y-3">
            <UButton type="submit" color="primary" class="w-full" icon="i-lucide-log-in">
              {{ $t('profile.family.join') }}
            </UButton>
            <UButton @click="mode = null" color="neutral" variant="soft" class="w-full" icon="i-lucide-x">
              {{ $t('common.cancel') }}
            </UButton>
          </div>
        </UForm>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useFamilyApi } from '~/composables/api/useFamilyApi'
import type { FamilyDetailsResponse, FamilyMemberResponse } from '~/types/family'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getFamily, getFamilyMembers, leaveFamily, createFamily, joinFamily } = useFamilyApi()
const router = useRouter()
const { t: $t } = useI18n()
const toast = useToast()

const family = ref<FamilyDetailsResponse | null>(null)
const members = ref<FamilyMemberResponse[]>([])
const loading = ref(true)
const isLeavingFamily = ref(false)
const mode = ref<'create' | 'join' | null>(null)
const familyName = ref('')
const familyDescription = ref('')
const shareCode = ref('')

const formatTimestamp = (timestamp: string): string => {
  const date = new Date(timestamp)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffMins = Math.floor(diffMs / 60000)
  const diffHours = Math.floor(diffMs / 3600000)
  const diffDays = Math.floor(diffMs / 86400000)

  if (diffMins < 1) return $t('time.justNow')
  if (diffMins < 60) return $t('time.minutesAgo', { count: diffMins })
  if (diffHours < 24) return $t('time.hoursAgo', { count: diffHours })
  if (diffDays < 7) return $t('time.daysAgo', { count: diffDays })

  return date.toLocaleDateString()
}

async function fetchFamily() {
  loading.value = true
  try {
    const res = await getFamily()
    family.value = res.data ?? null
  } catch {
    family.value = null
  }

  if (family.value) {
    try {
      const membersRes = await getFamilyMembers()
      members.value = membersRes.data ?? []
    } catch (error) {
      console.error('Failed to load family members:', error)
      members.value = []
    }
  }

  loading.value = false
}

onMounted(fetchFamily)

async function onLeaveFamily() {
  isLeavingFamily.value = true
  try {
    await leaveFamily()
    family.value = null
    members.value = []
    router.push('/profile')
  } catch (error) {
    console.error('Failed to leave family:', error)
    toast.add({
      title: $t('profile.family.leaveFailed'),
      color: 'red',
      icon: 'i-lucide-alert-circle'
    })
  } finally {
    isLeavingFamily.value = false
  }
}

async function onCreateFamily() {
  await createFamily({ name: familyName.value, description: familyDescription.value })
  mode.value = null
  familyName.value = ''
  familyDescription.value = ''
  await fetchFamily()
}

async function onJoinFamily() {
  await joinFamily({ shareCode: shareCode.value })
  mode.value = null
  shareCode.value = ''
  await fetchFamily()
}
</script>
