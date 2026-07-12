<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6 max-w-2xl mx-auto">
    <!-- Header with back button -->
    <div class="flex items-center gap-3">
      <NuxtLink to="/profile/data">
        <UButton icon="i-lucide-arrow-left" color="neutral" variant="ghost" />
      </NuxtLink>
      <UIcon name="i-lucide-calendar-plus" class="text-xl text-primary" />
      <h1 class="text-2xl font-semibold">{{ $t('profile.family.externalCalendars.title') }}</h1>
    </div>

    <template v-if="loading">
      <USkeleton class="h-4 w-2/3 rounded mb-4" />
      <USkeleton class="h-20 w-full rounded-lg mb-3" />
      <USkeleton class="h-20 w-full rounded-lg" />
    </template>

    <!-- Not part of a family: external calendars are family-scoped -->
    <div v-else-if="!hasFamily" class="rounded-lg border border-default p-6 text-center space-y-4">
      <UIcon name="i-lucide-users" class="h-10 w-10 mx-auto text-muted" />
      <p class="text-sm text-muted">{{ $t('profile.externalCalendars.emptyFamily') }}</p>
      <UButton to="/profile?open=family" color="primary" variant="soft" trailing-icon="i-lucide-arrow-right">
        {{ $t('profile.family.title') }}
      </UButton>
    </div>

    <!-- External calendars CRUD -->
    <div v-else class="space-y-4">
      <div class="flex items-start gap-3">
        <p class="flex-1 text-sm text-gray-500 dark:text-gray-400">
          {{ $t('profile.family.externalCalendars.description') }}
        </p>
        <UButton
          size="sm"
          variant="outline"
          color="primary"
          icon="i-lucide-plus"
          @click="() => { showAddCalendar = !showAddCalendar }"
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
              <input v-model="newCalColor" type="color" class="w-8 h-8 rounded cursor-pointer border-0 bg-transparent" >
              <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-x" @click="() => { showNewColorPicker = false }" />
            </template>
          </div>
        </div>
        <div class="flex gap-2">
          <UButton color="primary" size="sm" icon="i-lucide-plus" :loading="isAddingCalendar" :disabled="!newCalName.trim() || !newCalUrl.trim()" @click="onAddCalendar">
            {{ $t('profile.family.externalCalendars.add') }}
          </UButton>
          <UButton color="neutral" variant="soft" size="sm" icon="i-lucide-x" @click="() => { showAddCalendar = false }">
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
                  <input v-model="editCalColor" type="color" class="w-8 h-8 rounded cursor-pointer border-0 bg-transparent" >
                  <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-x" @click="() => { showEditColorPicker = false }" />
                </template>
              </div>
              <div class="flex items-center gap-2">
                <label class="text-sm font-medium">{{ $t('profile.family.externalCalendars.enabledLabel') }}</label>
                <USwitch v-model="editCalEnabled" />
              </div>
            </div>
            <div class="flex gap-2">
              <UButton color="primary" size="sm" icon="i-lucide-check" :loading="isSavingCalendar" @click="onSaveEditCalendar">
                {{ $t('common.save') }}
              </UButton>
              <UButton color="neutral" variant="soft" size="sm" icon="i-lucide-x" @click="() => { editingCalId = null }">
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
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { useFamilyApi } from '~/composables/api/useFamilyApi'
import { useExternalCalendarApi } from '~/composables/api/useExternalCalendarApi'
import type { ExternalCalendarResponse } from '~/types/externalCalendar'
import type { MasterDataDeletedEvent } from '~/types/masterData'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getFamily } = useFamilyApi()
const masterDataSocket = useMasterDataSocket()
const {
  getExternalCalendars,
  createExternalCalendar,
  updateExternalCalendar,
  deleteExternalCalendar,
  syncExternalCalendar
} = useExternalCalendarApi()
const { t: $t } = useI18n()
const toast = useToast()

const loading = ref(true)
const hasFamily = ref(false)

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

async function load() {
  loading.value = true
  try {
    const familyRes = await getFamily()
    hasFamily.value = !!familyRes.data
  } catch {
    hasFamily.value = false
  }

  if (hasFamily.value) {
    try {
      const calsRes = await getExternalCalendars()
      externalCalendars.value = calsRes.data ?? []
    } catch (error) {
      console.error('Failed to load external calendars:', error)
      externalCalendars.value = []
    }
  }

  loading.value = false
}

// Realtime: mirror another family member's (or device's) change in place. Idempotent, so the acting
// client's own echoed event is a no-op on top of its local patch.
function handleCalendarUpserted(dto: ExternalCalendarResponse) {
  const idx = externalCalendars.value.findIndex(c => c.publicId === dto.publicId)
  if (idx >= 0) externalCalendars.value[idx] = dto
  else externalCalendars.value.push(dto)
}

function handleCalendarDeleted(payload: MasterDataDeletedEvent) {
  externalCalendars.value = externalCalendars.value.filter(c => c.publicId !== payload.publicId)
}

onMounted(async () => {
  await load()
  await masterDataSocket.ensureConnected()
  masterDataSocket.on('ExternalCalendarUpserted', handleCalendarUpserted)
  masterDataSocket.on('ExternalCalendarDeleted', handleCalendarDeleted)
  masterDataSocket.onReconnected(load)
})

onBeforeUnmount(() => {
  masterDataSocket.off('ExternalCalendarUpserted', handleCalendarUpserted)
  masterDataSocket.off('ExternalCalendarDeleted', handleCalendarDeleted)
  masterDataSocket.offReconnected(load)
})

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
