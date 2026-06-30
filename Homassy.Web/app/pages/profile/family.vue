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

      <!-- Incoming Join Requests Section -->
      <div v-if="joinRequests.length > 0" class="space-y-4">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-user-plus" class="text-2xl text-primary" />
          <div class="flex-1">
            <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ $t('profile.family.joinRequests.title') }}</h3>
            <p class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.family.joinRequests.description') }}</p>
          </div>
          <div class="flex items-center justify-center min-w-[24px] h-6 px-2 rounded-full bg-primary-500 text-white text-xs font-bold">
            {{ joinRequests.length }}
          </div>
        </div>

        <div class="space-y-3">
          <div v-for="req in joinRequests" :key="req.publicId" class="flex items-center gap-3 p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50">
            <UAvatar
              :src="req.profilePictureBase64 ? `data:image/jpeg;base64,${req.profilePictureBase64}` : undefined"
              :alt="req.displayName || req.name"
              class="h-12 w-12"
            />
            <div class="flex-1 min-w-0">
              <div class="font-medium truncate">{{ req.displayName || req.name }}</div>
              <div class="text-sm text-gray-500 dark:text-gray-400">
                {{ $t('profile.family.joinRequests.sentAt') }}: {{ formatTimestamp(req.requestedAt) }}
              </div>
            </div>
            <div class="flex items-center gap-2 flex-shrink-0">
              <UButton
                color="primary"
                variant="solid"
                size="sm"
                icon="i-lucide-check"
                :loading="processingId === req.publicId"
                :disabled="processingId !== null"
                @click="onApproveRequest(req)"
              >
                {{ $t('profile.family.joinRequests.approve') }}
              </UButton>
              <UButton
                color="error"
                variant="soft"
                size="sm"
                icon="i-lucide-x"
                :disabled="processingId !== null"
                @click="onRejectRequest(req)"
              >
                {{ $t('profile.family.joinRequests.decline') }}
              </UButton>
            </div>
          </div>
        </div>
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

      <!-- External Calendars Section -->
      <div class="space-y-4">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-calendar-plus" class="text-2xl text-primary" />
          <div class="flex-1">
            <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ $t('profile.family.externalCalendars.title') }}</h3>
            <p class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.family.externalCalendars.description') }}</p>
          </div>
          <UButton
            size="sm"
            variant="outline"
            color="primary"
            icon="i-lucide-plus"
            @click="showAddCalendar = !showAddCalendar"
          >
            {{ $t('profile.family.externalCalendars.add') }}
          </UButton>
        </div>

        <!-- Add form -->
        <div v-if="showAddCalendar" class="rounded-lg border border-gray-200 dark:border-gray-700 p-4 space-y-3">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label class="block text-sm font-medium mb-1.5">{{ $t('common.name') }}</label>
              <UInput v-model="newCalName" :placeholder="$t('profile.family.externalCalendars.namePlaceholder')" class="w-full" />
            </div>
            <div>
              <label class="block text-sm font-medium mb-1.5">{{ $t('profile.family.externalCalendars.urlLabel') }}</label>
              <UInput v-model="newCalUrl" placeholder="webcal://..." class="w-full" />
            </div>
          </div>
          <div class="flex items-center gap-3">
            <label class="text-sm font-medium">{{ $t('common.color') }}</label>
            <div class="flex items-center gap-2">
              <span v-if="!showNewColorPicker" class="w-6 h-6 rounded-full border border-gray-300 cursor-pointer" :style="{ backgroundColor: newCalColor }" @click="showNewColorPicker = true" />
              <template v-else>
                <input type="color" v-model="newCalColor" class="w-8 h-8 rounded cursor-pointer border-0 bg-transparent" />
                <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-x" @click="showNewColorPicker = false" />
              </template>
            </div>
          </div>
          <div class="flex gap-2">
            <UButton color="primary" size="sm" icon="i-lucide-plus" :loading="isAddingCalendar" :disabled="!newCalName.trim() || !newCalUrl.trim()" @click="onAddCalendar">
              {{ $t('profile.family.externalCalendars.add') }}
            </UButton>
            <UButton color="neutral" variant="soft" size="sm" icon="i-lucide-x" @click="showAddCalendar = false">
              {{ $t('common.cancel') }}
            </UButton>
          </div>
        </div>

        <!-- Calendar list -->
        <div v-if="externalCalendars.length > 0" class="space-y-2">
          <div v-for="cal in externalCalendars" :key="cal.publicId" class="rounded-lg border border-gray-200 dark:border-gray-700 p-3">
            <div v-if="editingCalId !== cal.publicId">
              <div class="flex items-center gap-3">
                <span class="w-4 h-4 rounded-full flex-shrink-0" :style="{ backgroundColor: cal.color }" />
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2">
                    <span class="font-medium truncate">{{ cal.name }}</span>
                    <span v-if="!cal.isEnabled" class="text-xs px-1.5 py-0.5 rounded bg-gray-100 dark:bg-gray-700 text-gray-500">{{ $t('profile.family.externalCalendars.disabled') }}</span>
                  </div>
                  <div class="text-xs text-gray-500 dark:text-gray-400 truncate mt-0.5">{{ cal.iCalUrl }}</div>
                  <div class="flex items-center gap-2 mt-1 text-xs text-gray-400">
                    <span v-if="cal.lastSyncedAt">{{ $t('profile.family.externalCalendars.lastSync') }}: {{ formatTimestamp(cal.lastSyncedAt) }}</span>
                    <span v-else>{{ $t('profile.family.externalCalendars.neverSynced') }}</span>
                    <span>·</span>
                    <span>{{ $t('profile.family.externalCalendars.eventCount', { count: cal.eventCount }) }}</span>
                  </div>
                  <div v-if="cal.lastSyncError" class="mt-1 text-xs text-red-500 flex items-center gap-1">
                    <UIcon name="i-lucide-alert-circle" class="h-3 w-3" />
                    {{ cal.lastSyncError }}
                  </div>
                </div>
                <div class="flex items-center gap-1 flex-shrink-0">
                  <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-refresh-cw" :loading="syncingId === cal.publicId" :disabled="syncingId !== null" :title="$t('profile.family.externalCalendars.sync')" @click="onSyncCalendar(cal)" />
                  <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-pencil" :title="$t('common.edit')" @click="startEditCalendar(cal)" />
                  <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" :title="$t('common.delete')" @click="onDeleteCalendar(cal)" />
                </div>
              </div>
            </div>
            <!-- Edit inline -->
            <div v-else class="space-y-3">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-sm font-medium mb-1.5">{{ $t('common.name') }}</label>
                  <UInput v-model="editCalName" class="w-full" />
                </div>
                <div>
                  <label class="block text-sm font-medium mb-1.5">{{ $t('profile.family.externalCalendars.urlLabel') }}</label>
                  <UInput v-model="editCalUrl" class="w-full" />
                </div>
              </div>
              <div class="flex items-center gap-3 flex-wrap">
                <div class="flex items-center gap-2">
                  <label class="text-sm font-medium">{{ $t('common.color') }}</label>
                  <span v-if="!showEditColorPicker" class="w-6 h-6 rounded-full border border-gray-300 cursor-pointer" :style="{ backgroundColor: editCalColor }" @click="showEditColorPicker = true" />
                  <template v-else>
                    <input type="color" v-model="editCalColor" class="w-8 h-8 rounded cursor-pointer border-0 bg-transparent" />
                    <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-x" @click="showEditColorPicker = false" />
                  </template>
                </div>
                <div class="flex items-center gap-2">
                  <label class="text-sm font-medium">{{ $t('profile.family.externalCalendars.enabledLabel') }}</label>
                  <UToggle v-model="editCalEnabled" />
                </div>
              </div>
              <div class="flex gap-2">
                <UButton color="primary" size="sm" icon="i-lucide-check" :loading="isSavingCalendar" @click="onSaveEditCalendar">
                  {{ $t('common.save') }}
                </UButton>
                <UButton color="neutral" variant="soft" size="sm" icon="i-lucide-x" @click="editingCalId = null">
                  {{ $t('common.cancel') }}
                </UButton>
              </div>
            </div>
          </div>
        </div>
        <div v-else class="text-sm text-gray-400 dark:text-gray-500 py-2">
          {{ $t('profile.family.externalCalendars.empty') }}
        </div>
      </div>
    </div>
    <div v-else class="space-y-6">
      <!-- Pending join request state -->
      <div v-if="myRequest" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-6 space-y-4">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-clock" class="text-xl text-primary" />
          <h2 class="text-xl font-semibold">{{ $t('profile.family.pending.title') }}</h2>
        </div>
        <div class="rounded-md bg-primary-50 dark:bg-primary-900/20 border border-primary-200 dark:border-primary-800 p-4">
          <p class="text-sm text-gray-800 dark:text-gray-200">
            {{ $t('profile.family.pending.body', { family: myRequest.familyName }) }}
          </p>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-2">
            {{ $t('profile.family.pending.sentAt') }}: {{ formatTimestamp(myRequest.requestedAt) }}
          </p>
        </div>
        <UButton color="error" variant="soft" class="w-full flex items-center justify-center gap-2" icon="i-lucide-x" :loading="isWithdrawing" :disabled="isWithdrawing" @click="onWithdrawRequest">
          {{ $t('profile.family.pending.withdraw') }}
        </UButton>
      </div>

      <!-- Show buttons when no mode selected -->
      <div v-if="!mode && !myRequest" class="grid grid-cols-1 md:grid-cols-2 gap-6">
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
      <div v-if="mode === 'create' && !myRequest" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-6">
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
      <div v-if="mode === 'join' && !myRequest" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-6">
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
import { useExternalCalendarApi } from '~/composables/api/useExternalCalendarApi'
import type { FamilyDetailsResponse, FamilyMemberResponse, MyJoinRequestResponse, FamilyJoinRequestResponse } from '~/types/family'
import type { ExternalCalendarResponse } from '~/types/externalCalendar'

definePageMeta({ layout: 'auth', middleware: 'auth' })

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
const {
  getExternalCalendars,
  createExternalCalendar,
  updateExternalCalendar,
  deleteExternalCalendar,
  syncExternalCalendar
} = useExternalCalendarApi()
const router = useRouter()
const { t: $t } = useI18n()
const toast = useToast()

const family = ref<FamilyDetailsResponse | null>(null)
const members = ref<FamilyMemberResponse[]>([])
const joinRequests = ref<FamilyJoinRequestResponse[]>([])
const myRequest = ref<MyJoinRequestResponse | null>(null)
const loading = ref(true)
const isLeavingFamily = ref(false)
const isWithdrawing = ref(false)
const processingId = ref<string | null>(null)
const mode = ref<'create' | 'join' | null>(null)
const familyName = ref('')
const familyDescription = ref('')
const shareCode = ref('')

// External calendars state
const externalCalendars = ref<ExternalCalendarResponse[]>([])
const showAddCalendar = ref(false)
const newCalName = ref('')
const newCalUrl = ref('')
const newCalColor = ref('#3B82F6')
const showNewColorPicker = ref(false)
const isAddingCalendar = ref(false)
const syncingId = ref<string | null>(null)
const editingCalId = ref<string | null>(null)
const editCalName = ref('')
const editCalUrl = ref('')
const editCalColor = ref('#3B82F6')
const editCalEnabled = ref(true)
const showEditColorPicker = ref(false)
const isSavingCalendar = ref(false)

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
  joinRequests.value = []
  myRequest.value = null
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
    try {
      const calsRes = await getExternalCalendars()
      externalCalendars.value = calsRes.data ?? []
    } catch (error) {
      console.error('Failed to load external calendars:', error)
      externalCalendars.value = []
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
      color: 'error',
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
  const res = await requestJoin({ shareCode: shareCode.value })
  if (res.success && res.data) {
    myRequest.value = res.data
    mode.value = null
    shareCode.value = ''
    toast.add({
      title: $t('profile.family.pending.submitted'),
      color: 'success',
      icon: 'i-lucide-check-circle'
    })
  }
}

async function onWithdrawRequest() {
  isWithdrawing.value = true
  try {
    const res = await cancelMyJoinRequest()
    if (res.success) {
      myRequest.value = null
      toast.add({
        title: $t('profile.family.pending.withdrawn'),
        color: 'success',
        icon: 'i-lucide-check-circle'
      })
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
      toast.add({
        title: $t('profile.family.joinRequests.approved'),
        color: 'success',
        icon: 'i-lucide-check-circle'
      })
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
      toast.add({
        title: $t('profile.family.joinRequests.declined'),
        color: 'success',
        icon: 'i-lucide-check-circle'
      })
      await fetchFamily()
    }
  } catch (error) {
    console.error('Failed to decline join request:', error)
  } finally {
    processingId.value = null
  }
}

async function onAddCalendar() {
  if (!newCalName.value.trim() || !newCalUrl.value.trim()) return
  isAddingCalendar.value = true
  try {
    const res = await createExternalCalendar({
      name: newCalName.value.trim(),
      iCalUrl: newCalUrl.value.trim(),
      color: newCalColor.value
    })
    if (res.success && res.data) {
      externalCalendars.value.push(res.data)
      newCalName.value = ''
      newCalUrl.value = ''
      newCalColor.value = '#3B82F6'
      showNewColorPicker.value = false
      showAddCalendar.value = false
      toast.add({ title: $t('profile.family.externalCalendars.added'), color: 'success', icon: 'i-lucide-check-circle' })
    } else {
      toast.add({ title: $t('profile.family.externalCalendars.addFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
    }
  } catch {
    toast.add({ title: $t('profile.family.externalCalendars.addFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
  } finally {
    isAddingCalendar.value = false
  }
}

function startEditCalendar(cal: ExternalCalendarResponse) {
  editingCalId.value = cal.publicId
  editCalName.value = cal.name
  editCalUrl.value = cal.iCalUrl
  editCalColor.value = cal.color
  editCalEnabled.value = cal.isEnabled
  showEditColorPicker.value = false
}

async function onSaveEditCalendar() {
  if (!editingCalId.value) return
  isSavingCalendar.value = true
  try {
    const res = await updateExternalCalendar(editingCalId.value, {
      name: editCalName.value.trim(),
      iCalUrl: editCalUrl.value.trim(),
      color: editCalColor.value,
      isEnabled: editCalEnabled.value
    })
    if (res.success && res.data) {
      const idx = externalCalendars.value.findIndex(c => c.publicId === editingCalId.value)
      if (idx >= 0) externalCalendars.value[idx] = res.data
      editingCalId.value = null
      toast.add({ title: $t('profile.family.externalCalendars.updated'), color: 'success', icon: 'i-lucide-check-circle' })
    }
  } catch {
    toast.add({ title: $t('profile.family.externalCalendars.updateFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
  } finally {
    isSavingCalendar.value = false
  }
}

async function onDeleteCalendar(cal: ExternalCalendarResponse) {
  const res = await deleteExternalCalendar(cal.publicId)
  if (res.success) {
    externalCalendars.value = externalCalendars.value.filter(c => c.publicId !== cal.publicId)
    toast.add({ title: $t('profile.family.externalCalendars.deleted'), color: 'success', icon: 'i-lucide-check-circle' })
  }
}

async function onSyncCalendar(cal: ExternalCalendarResponse) {
  syncingId.value = cal.publicId
  try {
    const res = await syncExternalCalendar(cal.publicId)
    if (res.success && res.data) {
      const idx = externalCalendars.value.findIndex(c => c.publicId === cal.publicId)
      if (idx >= 0) externalCalendars.value[idx] = res.data
      toast.add({ title: $t('profile.family.externalCalendars.synced'), color: 'success', icon: 'i-lucide-check-circle' })
    } else {
      toast.add({ title: $t('profile.family.externalCalendars.syncFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
    }
  } catch {
    toast.add({ title: $t('profile.family.externalCalendars.syncFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
  } finally {
    syncingId.value = null
  }
}
</script>
