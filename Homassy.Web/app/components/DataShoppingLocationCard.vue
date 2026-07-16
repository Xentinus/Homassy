<template>
  <div class="relative rounded-2xl overflow-hidden card-animate" style="touch-action: pan-y" data-no-pull-refresh>
    <!-- Swipe action layer -->
    <div
      v-show="swipe.isSwiping.value"
      aria-hidden="true"
      class="absolute inset-0 rounded-2xl flex items-center justify-between px-4"
      :class="swipe.direction.value === 'left' ? 'bg-error-500 dark:bg-error-600' : 'bg-primary-500 dark:bg-primary-600'"
    >
      <UIcon name="i-lucide-pencil" class="h-5 w-5 text-white transition-transform duration-150"
        :class="[swipe.direction.value === 'right' ? 'opacity-100' : 'opacity-0', swipe.progress.value >= 1 ? 'scale-125' : '']" />
      <UIcon name="i-lucide-trash-2" class="h-5 w-5 text-white transition-transform duration-150"
        :class="[swipe.direction.value === 'left' ? 'opacity-100' : 'opacity-0', swipe.progress.value >= 1 ? 'scale-125' : '']" />
    </div>

    <!-- Card surface -->
    <div
      ref="cardEl"
      class="relative h-full bg-default rounded-2xl border-2 p-3 cursor-pointer shadow-sm hover:shadow-lg transition-shadow duration-200 flex flex-col overflow-hidden select-none"
      :class="cardBorderClass"
      :style="swipe.cardStyle.value"
      @click="handleCardClick"
    >
      <!-- Header: color + name + shared -->
      <div class="min-w-0 space-y-1">
        <div class="flex items-center gap-2">
          <span v-if="location.color" class="h-2.5 w-2.5 rounded-full flex-shrink-0" :style="{ backgroundColor: location.color }" />
          <h3 class="text-sm font-bold break-words text-highlighted flex-1" v-html="highlightText(location.name, searchQuery)" />
          <UIcon v-if="location.isSharedWithFamily" name="i-lucide-users" class="h-3.5 w-3.5 text-primary-500 flex-shrink-0" :title="$t('common.family')" />
        </div>
        <p v-if="location.description" class="text-xs text-muted break-words line-clamp-2" v-html="highlightText(location.description, searchQuery)" />
        <div v-if="location.storeTypes?.length" class="flex flex-wrap gap-1 pt-0.5">
          <span
            v-for="st in location.storeTypes"
            :key="st"
            class="inline-flex items-center rounded-full border border-primary-200/60 bg-primary-50 px-2 py-0.5 text-[10px] font-medium text-primary-700 dark:border-primary-700/50 dark:bg-primary-900/30 dark:text-primary-300"
          >
            {{ formatStoreType(st) }}
          </span>
        </div>
      </div>

      <!-- Address + links (pinned bottom) -->
      <div class="mt-auto pt-4 space-y-2.5">
        <div v-if="hasAddress" class="flex items-start gap-2 text-xs">
          <UIcon name="i-lucide-map-pin" class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400 mt-0.5 flex-shrink-0" />
          <div class="min-w-0 space-y-0.5 text-toned">
            <p v-if="location.country" class="break-words" v-html="highlightText(location.country, searchQuery)" />
            <p v-if="location.city || location.postalCode" class="break-words">
              <span v-if="location.postalCode" v-html="highlightText(location.postalCode, searchQuery)" />
              <span v-if="location.city" v-html="highlightText(location.city, searchQuery)" />
            </p>
            <p v-if="location.address" class="break-words" v-html="highlightText(location.address, searchQuery)" />
          </div>
        </div>

        <div v-if="location.website || location.googleMaps" class="flex flex-wrap gap-2">
          <UButton
            v-if="location.website"
            icon="i-lucide-external-link"
            :label="$t('profile.shoppingLocations.website')"
            size="xs"
            color="primary"
            variant="soft"
            class="rounded-full"
            :to="location.website"
            target="_blank"
            rel="noopener noreferrer"
            @click.stop
          />
          <UButton
            v-if="location.googleMaps"
            icon="i-lucide-map"
            :label="$t('profile.shoppingLocations.googleMaps')"
            size="xs"
            color="success"
            variant="soft"
            class="rounded-full"
            :to="location.googleMaps"
            target="_blank"
            rel="noopener noreferrer"
            @click.stop
          />
        </div>
      </div>
    </div>

    <!-- Delete confirmation -->
    <UModal :open="isDeleteModalOpen" :dismissible="false" @update:open="(v) => { isDeleteModalOpen = v }">
      <template #title>{{ $t('profile.shoppingLocations.deleteLocation') }}</template>
      <template #description>{{ $t('profile.shoppingLocations.deleteLocationWarning') }}</template>
      <template #body>
        <div class="space-y-2">
          <div>
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ $t('common.name') }}:</span>
            <span class="text-sm ml-2">{{ location.name }}</span>
          </div>
          <div v-if="location.description">
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ $t('common.description') }}:</span>
            <span class="text-sm ml-2">{{ location.description }}</span>
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton :label="$t('common.cancel')" color="neutral" variant="outline" @click="() => { isDeleteModalOpen = false }" />
          <UButton :label="$t('common.delete')" color="error" :loading="isDeleting" @click="handleDelete" />
        </div>
      </template>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ShoppingLocationInfo } from '~/types/location'
import { useLocationsApi } from '~/composables/api/useLocationsApi'

const props = withDefaults(defineProps<{
  location: ShoppingLocationInfo
  isActive?: boolean
  searchQuery?: string
}>(), {
  isActive: false,
  searchQuery: ''
})

const emit = defineEmits<{
  select: [publicId: string]
  edit: [location: ShoppingLocationInfo]
  deleted: [publicId: string]
}>()

const { t } = useI18n()
const toast = useToast()
const { deleteShoppingLocation } = useLocationsApi()
const { highlightText } = useSearchHighlight()
const { formatStoreType } = useEnumLabel()

const isDeleteModalOpen = ref(false)
const isDeleting = ref(false)

const hasAddress = computed(() =>
  !!(props.location.country || props.location.city || props.location.postalCode || props.location.address)
)

const cardEl = ref<HTMLElement | null>(null)
const swipe = useSwipeActions(cardEl, {
  onSwipeLeft: () => { isDeleteModalOpen.value = true },
  onSwipeRight: () => emit('edit', props.location),
  disabled: () => isDeleteModalOpen.value || isDeleting.value
})

const cardBorderClass = computed(() =>
  props.isActive ? 'border-primary-400 dark:border-primary-500' : 'border-gray-200 dark:border-gray-700'
)

function handleCardClick(event: MouseEvent) {
  if (swipe.suppressClick.value) return
  const target = event.target as HTMLElement
  if (target.closest('a, button')) return
  emit('select', props.location.publicId)
}

async function handleDelete() {
  isDeleting.value = true
  try {
    await deleteShoppingLocation(props.location.publicId)
    isDeleteModalOpen.value = false
    emit('deleted', props.location.publicId)
  } catch (error) {
    console.error('Failed to delete shopping location:', error)
    toast.add({ title: t('common.error'), description: t('profile.shoppingLocations.deleteFailed'), color: 'error' })
  } finally {
    isDeleting.value = false
  }
}
</script>

<style scoped>
@keyframes slideInUp {
  from { opacity: 0; transform: translateY(20px); }
  to { opacity: 1; transform: translateY(0); }
}
.card-animate {
  animation: slideInUp 0.4s ease-out;
}
</style>
