<template>
  <AppDrawer
    :open="open"
    :title="t('profile.family.title')"
    icon="i-lucide-users"
    :loading="isSubmitting || isLeavingFamily"
    @update:open="(value) => emit('update:open', value)"
  >
    <template v-if="loading">
      <USkeleton class="h-16 w-16 rounded-full mb-3" />
      <USkeleton class="h-6 w-32 mb-2" />
      <USkeleton class="h-4 w-40 mb-4" />
      <USkeleton class="h-10 w-full rounded-lg mb-6" />
      <USkeleton class="h-20 w-full rounded-lg" />
    </template>

    <!-- HAS FAMILY -->
    <div v-else-if="family" class="space-y-8">
      <div class="space-y-4">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-home" class="text-2xl text-primary" />
          <div>
            <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ t('profile.family.infoSection') }}</h3>
            <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.family.infoDescription') }}</p>
          </div>
        </div>

        <div class="flex items-center gap-4">
          <div v-if="family.familyPictureBase64">
            <UAvatar :src="`data:image/jpeg;base64,${family.familyPictureBase64}`" :alt="family.name" class="h-16 w-16" />
          </div>
          <div class="flex-1">
            <div class="text-lg font-semibold">{{ family.name }}</div>
            <div v-if="family.description" class="text-sm text-gray-500 dark:text-gray-400">{{ family.description }}</div>
          </div>
        </div>

        <div class="pt-2">
          <div class="text-xs text-gray-500 dark:text-gray-400 mb-1">{{ t('profile.family.codeLabel') }}</div>
          <div class="text-2xl font-extrabold text-primary-600 tracking-widest uppercase">{{ family.shareCode }}</div>
        </div>

        <UButton color="error" variant="soft" class="w-full flex items-center justify-center gap-2" icon="i-lucide-log-out" :loading="isLeavingFamily" :disabled="isLeavingFamily" @click="onLeaveFamily">
          {{ t('profile.family.leave') }}
        </UButton>
      </div>

      <!-- Incoming join requests -->
      <div v-if="joinRequests.length > 0" class="space-y-4">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-user-plus" class="text-2xl text-primary" />
          <div class="flex-1">
            <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ t('profile.family.joinRequests.title') }}</h3>
            <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.family.joinRequests.description') }}</p>
          </div>
          <div class="flex items-center justify-center min-w-[24px] h-6 px-2 rounded-full bg-primary-500 text-white text-xs font-bold">{{ joinRequests.length }}</div>
        </div>

        <div class="space-y-3">
          <div v-for="req in joinRequests" :key="req.publicId" class="flex items-center gap-3 p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50">
            <UAvatar :src="req.profilePictureBase64 ? `data:image/jpeg;base64,${req.profilePictureBase64}` : undefined" :alt="req.displayName || req.name" class="h-12 w-12" />
            <div class="flex-1 min-w-0">
              <div class="font-medium truncate">{{ req.displayName || req.name }}</div>
              <div class="text-sm text-gray-500 dark:text-gray-400">{{ t('profile.family.joinRequests.sentAt') }}: {{ formatTimestamp(req.requestedAt) }}</div>
            </div>
            <div class="flex items-center gap-2 flex-shrink-0">
              <UButton color="primary" variant="solid" size="sm" icon="i-lucide-check" :loading="processingId === req.publicId" :disabled="processingId !== null" @click="onApproveRequest(req)">
                {{ t('profile.family.joinRequests.approve') }}
              </UButton>
              <UButton color="error" variant="soft" size="sm" icon="i-lucide-x" :disabled="processingId !== null" @click="onRejectRequest(req)">
                {{ t('profile.family.joinRequests.decline') }}
              </UButton>
            </div>
          </div>
        </div>
      </div>

      <!-- Members -->
      <div class="space-y-4">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-users" class="text-2xl text-primary" />
          <div>
            <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ t('profile.family.members') }}</h3>
            <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.family.membersDescription') }}</p>
          </div>
        </div>
        <div class="space-y-3">
          <div v-for="member in members" :key="member.publicId" class="flex items-center gap-3 p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50">
            <UAvatar :src="member.profilePictureBase64 ? `data:image/jpeg;base64,${member.profilePictureBase64}` : undefined" :alt="member.displayName || member.name" class="h-12 w-12" />
            <div class="flex-1">
              <div class="flex items-center gap-2">
                <span class="font-medium">{{ member.displayName || member.name }}</span>
                <div v-if="member.isCurrentUser" class="flex items-center gap-1 px-2 py-0.5 rounded-md border bg-primary-50 dark:bg-primary-900/20 border-primary-200 dark:border-primary-800">
                  <span class="text-xs font-semibold text-primary-700 dark:text-primary-300">{{ t('profile.family.currentUserBadge') }}</span>
                </div>
              </div>
              <div class="text-sm text-gray-500 dark:text-gray-400">{{ t('profile.family.lastSeen') }}: {{ formatTimestamp(member.lastLoginAt) }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- PENDING outgoing request -->
    <div v-else-if="myRequest" class="space-y-4">
      <div class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-6 space-y-4">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-clock" class="text-xl text-primary" />
          <h2 class="text-xl font-semibold">{{ t('profile.family.pending.title') }}</h2>
        </div>
        <div class="rounded-md bg-primary-50 dark:bg-primary-900/20 border border-primary-200 dark:border-primary-800 p-4">
          <p class="text-sm text-gray-800 dark:text-gray-200">{{ t('profile.family.pending.body', { family: myRequest.familyName }) }}</p>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-2">{{ t('profile.family.pending.sentAt') }}: {{ formatTimestamp(myRequest.requestedAt) }}</p>
        </div>
        <UButton color="error" variant="soft" class="w-full flex items-center justify-center gap-2" icon="i-lucide-x" :loading="isWithdrawing" :disabled="isWithdrawing" @click="onWithdrawRequest">
          {{ t('profile.family.pending.withdraw') }}
        </UButton>
      </div>
    </div>

    <!-- CREATE form -->
    <div v-else-if="mode === 'create'" class="space-y-4">
      <div>
        <label class="block text-sm font-medium mb-1.5">{{ t('profile.family.nameLabel') }}</label>
        <UInput v-model="familyName" :placeholder="t('profile.family.nameLabel')" :disabled="isSubmitting" class="w-full" />
      </div>
      <div>
        <label class="block text-sm font-medium mb-1.5">{{ t('profile.family.descriptionLabel') }}</label>
        <UInput v-model="familyDescription" :placeholder="t('profile.family.descriptionLabel')" :disabled="isSubmitting" class="w-full" />
      </div>
    </div>

    <!-- JOIN form -->
    <div v-else-if="mode === 'join'" class="space-y-4">
      <div>
        <label class="block text-sm font-medium mb-1.5">{{ t('profile.family.codeLabel') }}</label>
        <UInput v-model="shareCode" :placeholder="t('profile.family.codeLabel')" :disabled="isSubmitting" class="w-full" />
      </div>
    </div>

    <!-- CHOOSER -->
    <div v-else class="grid grid-cols-1 gap-4">
      <button type="button" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors flex items-start gap-3 text-left" @click="mode = 'create'">
        <UIcon name="i-lucide-plus-circle" class="h-7 w-7 text-primary-500 mr-2 flex-shrink-0 self-center" />
        <div class="flex-1">
          <h2 class="text-base font-semibold mb-1">{{ t('profile.family.create') }}</h2>
          <div class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.family.createFamilyCard') }}</div>
        </div>
        <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
      </button>
      <button type="button" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors flex items-start gap-3 text-left" @click="mode = 'join'">
        <UIcon name="i-lucide-user-plus" class="h-7 w-7 text-primary-500 mr-2 flex-shrink-0 self-center" />
        <div class="flex-1">
          <h2 class="text-base font-semibold mb-1">{{ t('profile.family.join') }}</h2>
          <div class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.family.joinFamilyCard') }}</div>
        </div>
        <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
      </button>
    </div>

    <template #footer>
      <!-- create/join: back + submit -->
      <template v-if="!loading && !family && !myRequest && mode">
        <UButton :label="t('common.back')" color="neutral" variant="ghost" icon="i-lucide-arrow-left" :disabled="isSubmitting" @click="() => { mode = null }" />
        <UButton
          v-if="mode === 'create'"
          :label="t('profile.family.create')"
          color="primary"
          icon="i-lucide-plus-circle"
          :loading="isSubmitting"
          :disabled="!familyName.trim()"
          @click="onCreateFamily"
        />
        <UButton
          v-else
          :label="t('profile.family.join')"
          color="primary"
          icon="i-lucide-log-in"
          :loading="isSubmitting"
          :disabled="!shareCode.trim()"
          @click="onJoinFamily"
        />
      </template>
      <!-- everything else: just Close -->
      <UButton v-else class="ml-auto" :label="t('common.close')" color="neutral" variant="ghost" @click="emit('update:open', false)" />
    </template>
  </AppDrawer>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useFamilyApi } from '~/composables/api/useFamilyApi'
import type { FamilyDetailsResponse, FamilyMemberResponse, MyJoinRequestResponse, FamilyJoinRequestResponse } from '~/types/family'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const {
  getFamily,
  getFamilyMembers,
  leaveFamily,
  createFamily,
  requestJoin,
  getMyJoinRequest,
  cancelMyJoinRequest,
  getJoinRequests,
  approveJoinRequest,
  rejectJoinRequest
} = useFamilyApi()
const { t } = useI18n()
const toast = useToast()

const family = ref<FamilyDetailsResponse | null>(null)
const members = ref<FamilyMemberResponse[]>([])
const joinRequests = ref<FamilyJoinRequestResponse[]>([])
const myRequest = ref<MyJoinRequestResponse | null>(null)
const loading = ref(true)
const isLeavingFamily = ref(false)
const isWithdrawing = ref(false)
const isSubmitting = ref(false)
const processingId = ref<string | null>(null)
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

  if (diffMins < 1) return t('time.justNow')
  if (diffMins < 60) return t('time.minutesAgo', { count: diffMins })
  if (diffHours < 24) return t('time.hoursAgo', { count: diffHours })
  if (diffDays < 7) return t('time.daysAgo', { count: diffDays })
  return date.toLocaleDateString()
}

async function fetchFamily() {
  loading.value = true
  joinRequests.value = []
  myRequest.value = null
  mode.value = null
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
    try {
      const requestsRes = await getJoinRequests()
      joinRequests.value = requestsRes.data ?? []
    } catch (error) {
      console.error('Failed to load join requests:', error)
      joinRequests.value = []
    }
  } else {
    try {
      const myRequestRes = await getMyJoinRequest()
      myRequest.value = myRequestRes.data ?? null
    } catch (error) {
      console.error('Failed to load join request:', error)
      myRequest.value = null
    }
  }

  loading.value = false
}

watch(() => props.open, (isOpen) => {
  if (isOpen) fetchFamily()
})

async function onLeaveFamily() {
  isLeavingFamily.value = true
  try {
    await leaveFamily()
    family.value = null
    members.value = []
    mode.value = null
  } catch (error) {
    console.error('Failed to leave family:', error)
    toast.add({ title: t('profile.family.leaveFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
  } finally {
    isLeavingFamily.value = false
  }
}

async function onCreateFamily() {
  if (!familyName.value.trim()) return
  isSubmitting.value = true
  try {
    await createFamily({ name: familyName.value, description: familyDescription.value })
    familyName.value = ''
    familyDescription.value = ''
    await fetchFamily()
  } finally {
    isSubmitting.value = false
  }
}

async function onJoinFamily() {
  if (!shareCode.value.trim()) return
  isSubmitting.value = true
  try {
    const res = await requestJoin({ shareCode: shareCode.value })
    if (res.success && res.data) {
      myRequest.value = res.data
      mode.value = null
      shareCode.value = ''
      toast.add({ title: t('profile.family.pending.submitted'), color: 'success', icon: 'i-lucide-check-circle' })
    }
  } finally {
    isSubmitting.value = false
  }
}

async function onWithdrawRequest() {
  isWithdrawing.value = true
  try {
    const res = await cancelMyJoinRequest()
    if (res.success) {
      myRequest.value = null
      toast.add({ title: t('profile.family.pending.withdrawn'), color: 'success', icon: 'i-lucide-check-circle' })
    }
  } catch (error) {
    console.error('Failed to withdraw join request:', error)
  } finally {
    isWithdrawing.value = false
  }
}

async function onApproveRequest(request: FamilyJoinRequestResponse) {
  processingId.value = request.publicId
  try {
    const res = await approveJoinRequest(request.publicId)
    if (res.success) {
      toast.add({ title: t('profile.family.joinRequests.approved'), color: 'success', icon: 'i-lucide-check-circle' })
      await fetchFamily()
    }
  } catch (error) {
    console.error('Failed to approve join request:', error)
  } finally {
    processingId.value = null
  }
}

async function onRejectRequest(request: FamilyJoinRequestResponse) {
  processingId.value = request.publicId
  try {
    const res = await rejectJoinRequest(request.publicId)
    if (res.success) {
      toast.add({ title: t('profile.family.joinRequests.declined'), color: 'success', icon: 'i-lucide-check-circle' })
      await fetchFamily()
    }
  } catch (error) {
    console.error('Failed to decline join request:', error)
  } finally {
    processingId.value = null
  }
}
</script>
