<template>
  <UModal
    :open="isOpen"
    @update:open="handleModalUpdate"
    :dismissible="!isProcessing"
    :ui="{ width: 'sm:max-w-lg' }"
  >
    <template #title>
      {{ $t('pages.addProduct.progressModal.title') }}
    </template>

    <template #description>
      {{ getDescriptionText() }}
    </template>

    <template #body>
      <div class="space-y-3">
        <!-- Items List -->
        <div class="space-y-2 max-h-96 overflow-y-auto">
          <div
            v-for="(item, index) in items"
            :key="item.id"
            class="flex items-center gap-3 p-3 rounded-lg border transition-colors"
            :class="getItemCardClass(item.status)"
          >
            <!-- Status Icon -->
            <div class="flex-shrink-0">
              <div v-if="item.status === 'pending'" class="h-5 w-5 rounded-full bg-gray-300 dark:bg-gray-600" />
              <UIcon
                v-else-if="item.status === 'in-progress'"
                name="i-lucide-loader-2"
                class="h-5 w-5 text-primary-500 animate-spin"
              />
              <UIcon
                v-else-if="item.status === 'success'"
                name="i-lucide-check-circle"
                class="h-5 w-5 text-green-500"
              />
              <UIcon
                v-else-if="item.status === 'error'"
                name="i-lucide-x-circle"
                class="h-5 w-5 text-red-500"
              />
            </div>

            <!-- Item Info -->
            <div class="flex-1 min-w-0">
              <p class="text-sm font-medium truncate">
                {{ item.displayText }}
              </p>
              <p v-if="item.errorMessage" class="text-xs text-red-600 dark:text-red-400 mt-1">
                {{ item.errorMessage }}
              </p>
            </div>

            <!-- Status Badge -->
            <div class="flex-shrink-0">
              <UBadge
                v-if="item.status === 'error'"
                color="error"
                variant="soft"
                size="sm"
              >
                {{ $t('pages.addProduct.progressModal.failed') }}
              </UBadge>
            </div>
          </div>
        </div>

        <!-- Summary -->
        <div v-if="allProcessed" class="mt-4 p-3 rounded-lg bg-gray-50 dark:bg-gray-800">
          <p class="text-sm font-medium">
            {{ getSummaryText() }}
          </p>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end">
        <UButton
          v-if="allProcessed && hasFailures"
          :label="$t('common.confirm')"
          color="primary"
          @click="handleClose"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface ProgressItem {
  id: string
  displayText: string
  status: 'pending' | 'in-progress' | 'success' | 'error'
  errorMessage?: string
}

const props = withDefaults(defineProps<{
  isOpen?: boolean
  items?: ProgressItem[]
  isProcessing?: boolean
}>(), {
  isOpen: false,
  items: () => [],
  isProcessing: false
})

const emit = defineEmits<{
  'update:isOpen': [value: boolean]
  'close': []
}>()

const { t } = useI18n()

const allProcessed = computed(() => {
  if (props.items.length === 0) return false
  return props.items.every(item => item.status === 'success' || item.status === 'error')
})

const hasFailures = computed(() => {
  return props.items.some(item => item.status === 'error')
})

const successCount = computed(() => {
  return props.items.filter(item => item.status === 'success').length
})

const getItemCardClass = (status: string): string => {
  switch (status) {
    case 'success':
      return 'border-green-200 dark:border-green-800 bg-green-50 dark:bg-green-900/20'
    case 'error':
      return 'border-red-200 dark:border-red-800 bg-red-50 dark:bg-red-900/20'
    case 'in-progress':
      return 'border-primary-200 dark:border-primary-800 bg-primary-50 dark:bg-primary-900/20'
    default:
      return 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800'
  }
}

const getDescriptionText = (): string => {
  if (allProcessed.value) {
    return t('pages.addProduct.progressModal.completed')
  }

  const currentIndex = props.items.findIndex(item => item.status === 'in-progress') + 1
  if (currentIndex > 0) {
    return t('pages.addProduct.progressModal.processing', {
      current: currentIndex,
      total: props.items.length
    })
  }

  return t('pages.addProduct.progressModal.description')
}

const getSummaryText = (): string => {
  const total = props.items.length
  const success = successCount.value

  if (success === total) {
    return t('pages.addProduct.progressModal.summary.allSuccess', { count: total })
  } else if (success === 0) {
    return t('pages.addProduct.progressModal.summary.allFailed')
  } else {
    return t('pages.addProduct.progressModal.summary.partialSuccess', { success, total })
  }
}

const handleModalUpdate = (value: boolean) => {
  if (!props.isProcessing) {
    emit('update:isOpen', value)
  }
}

const handleClose = () => {
  emit('close')
  emit('update:isOpen', false)
}
</script>
