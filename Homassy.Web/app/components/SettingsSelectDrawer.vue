<template>
  <UDrawer
    :open="open"
    :dismissible="false"
    :ui="{
      content: 'h-[80dvh] rounded-t-2xl overflow-hidden',
      container: 'flex flex-1 flex-col min-h-0 gap-0 p-0 overflow-hidden',
      header: 'shrink-0 border-b border-default p-4 sm:px-6',
      body: 'flex-1 min-h-0 overflow-y-auto p-0',
      footer: 'shrink-0 flex flex-row items-center justify-end gap-2 border-t border-default p-4 sm:px-6'
    }"
    @update:open="(value) => emit('update:open', value)"
  >
    <template #header>
      <div ref="headerEl" class="flex items-center gap-3 w-full" style="touch-action: none">
        <UIcon v-if="icon" :name="icon" class="h-7 w-7 shrink-0 text-primary-500" />
        <DrawerTitle class="text-xl sm:text-2xl font-semibold">{{ title }}</DrawerTitle>
        <DrawerDescription class="sr-only">{{ title }}</DrawerDescription>
        <UButton
          class="ml-auto"
          icon="i-lucide-x"
          color="neutral"
          variant="ghost"
          :aria-label="t('common.close')"
          @click="emit('update:open', false)"
        />
      </div>
    </template>

    <template #body>
      <div ref="scrollEl" class="h-full overflow-y-auto">
        <div v-if="searchable" class="sticky top-0 z-10 bg-default p-4 sm:px-6 pb-2 border-b border-default">
          <UInput
            v-model="query"
            icon="i-lucide-search"
            :placeholder="t('common.search')"
            autofocus
            class="w-full"
          />
        </div>

        <div class="p-2 sm:px-4">
          <template v-if="loading">
            <USkeleton v-for="i in 6" :key="i" class="h-11 w-full rounded-lg mb-1.5" />
          </template>
          <template v-else>
            <button
              v-for="opt in filteredItems"
              :key="opt.value"
              :ref="(el) => registerOption(opt.value, el)"
              type="button"
              class="w-full flex items-center gap-3 px-3 py-3 rounded-lg text-left transition-colors active:bg-elevated hover:bg-elevated/60"
              @click="pending = opt.value"
            >
              <span class="flex-1 truncate" :class="{ 'font-medium text-primary-600 dark:text-primary-400': opt.value === pending }">
                {{ opt.label }}
              </span>
              <UIcon
                v-if="opt.value === pending"
                name="i-lucide-check"
                class="h-5 w-5 shrink-0 text-primary-500"
              />
            </button>
            <p v-if="filteredItems.length === 0" class="px-3 py-6 text-center text-sm text-muted">
              {{ t('common.noData') }}
            </p>
          </template>
        </div>
      </div>
    </template>

    <template #footer>
      <UButton :label="t('common.cancel')" color="neutral" variant="ghost" @click="onCancel" />
      <UButton
        :label="t('common.save')"
        color="primary"
        icon="i-lucide-save"
        :loading="loading"
        :disabled="pending === undefined"
        @click="onSave"
      />
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import { DrawerTitle, DrawerDescription } from 'vaul-vue'

/**
 * Single-select bottom sheet with an explicit Cancel/Save footer (language /
 * currency / time zone). Tapping an option stages it (`pending`); Save commits
 * via `save(value)`, Cancel discards. On open, the currently-selected option is
 * scrolled to the centre of the list. Uses the inventory fixed-footer recipe +
 * drag-to-close (native dismiss off so a mis-tap can't drop a staged choice).
 */
interface Option { label: string, value: string }

const props = withDefaults(defineProps<{
  open: boolean
  title: string
  icon?: string
  items: Option[]
  modelValue?: string
  /** Show a filter input (currency / time zone). */
  searchable?: boolean
  /** Save in flight — disables Save + drag. */
  loading?: boolean
}>(), {
  icon: undefined,
  modelValue: undefined,
  searchable: false,
  loading: false
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  save: [value: string]
}>()

const { t } = useI18n()

const query = ref('')
const pending = ref<string | undefined>(props.modelValue)
const headerEl = ref<HTMLElement | null>(null)
const scrollEl = ref<HTMLElement | null>(null)
const optionEls = new Map<string, HTMLElement>()

useDrawerDragToClose(headerEl, {
  onClose: () => emit('update:open', false),
  disabled: () => props.loading
})

function registerOption(value: string, el: unknown) {
  if (el instanceof HTMLElement) optionEls.set(value, el)
  else optionEls.delete(value)
}

const filteredItems = computed(() => {
  const q = query.value.trim().toLowerCase()
  if (!q) return props.items
  return props.items.filter(o => o.label.toLowerCase().includes(q))
})

// On open: reset the staged value + filter, then centre the selected option.
watch(() => props.open, (isOpen) => {
  if (!isOpen) return
  query.value = ''
  pending.value = props.modelValue
  nextTick(() => {
    requestAnimationFrame(() => {
      if (pending.value === undefined) return
      optionEls.get(pending.value)?.scrollIntoView({ block: 'center' })
    })
  })
})

function onSave() {
  if (pending.value === undefined) return
  emit('save', pending.value)
  emit('update:open', false)
}

function onCancel() {
  emit('update:open', false)
}
</script>
