<template>
  <UDrawer
    :open="open"
    :dismissible="dismissible"
    :ui="ui"
    @update:open="(value) => emit('update:open', value)"
  >
    <template #header>
      <div ref="headerEl" class="w-full space-y-4" style="touch-action: none">
        <div class="flex items-center gap-3">
          <UIcon v-if="icon" :name="icon" class="h-7 w-7 shrink-0 text-primary-500" />
          <DrawerTitle class="text-xl sm:text-2xl font-semibold">{{ title }}</DrawerTitle>
          <DrawerDescription class="sr-only">{{ description || title }}</DrawerDescription>
          <UButton
            class="ml-auto"
            icon="i-lucide-x"
            color="neutral"
            variant="ghost"
            :aria-label="t('common.close')"
            @click="emit('update:open', false)"
          />
        </div>

        <!-- Optional row below the title (e.g. a wizard progress bar). -->
        <slot name="header-extra" />
      </div>
    </template>

    <template #body>
      <slot />
    </template>

    <template v-if="$slots.footer" #footer>
      <slot name="footer" />
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { DrawerTitle, DrawerDescription } from 'vaul-vue'

/**
 * The app's standard bottom-sheet drawer: a near-fullscreen `UDrawer` with a
 * fixed header (icon + title + close ✕), a scrollable body, and an optional
 * pinned footer for action buttons. Drag the header down to dismiss.
 *
 * This is the single source of truth for drawer chrome — every bottom sheet
 * (wizards, forms, overviews, filters, the barcode scanner, …) wraps this so
 * they are all the same size, look identical, and are always closable via the
 * ✕ button or the drag gesture. Small confirm/progress dialogs stay `UModal`.
 *
 * Slots: `header-extra` (below the title), the default slot (scrollable body),
 * and `footer` (only rendered when provided — overview drawers omit it).
 */
const props = withDefaults(defineProps<{
  /** Visibility (use with v-model:open). */
  open: boolean
  /** Header title. */
  title: string
  /** Optional Lucide/Heroicons icon shown before the title. */
  icon?: string
  /** Accessible description (sr-only); falls back to the title. */
  description?: string
  /** Allow outside-tap / Esc to dismiss (default false — ✕ and drag still close it). */
  dismissible?: boolean
  /** `full` = fixed near-fullscreen (default); `content` = grow to fit up to 90dvh. */
  fit?: 'full' | 'content'
  /** Enable drag-the-header-down-to-close (default true). */
  dragToClose?: boolean
  /** While true, the drag gesture is disabled (e.g. during submit). */
  loading?: boolean
  /** Apply default body padding (default true). Set false for edge-to-edge content (sticky search, full-bleed lists). */
  padded?: boolean
}>(), {
  icon: undefined,
  description: undefined,
  dismissible: false,
  fit: 'full',
  dragToClose: true,
  loading: false,
  padded: true
})

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

const { t } = useI18n()

const ui = computed(() => {
  const full = props.fit === 'full'
  return {
    content: `${full ? 'h-[94dvh]' : 'max-h-[90dvh]'} rounded-t-2xl overflow-hidden`,
    container: `flex ${full ? 'flex-1 ' : ''}flex-col min-h-0 gap-0 p-0 overflow-hidden`,
    header: 'shrink-0 border-b border-default p-4 sm:px-6',
    body: `${full ? 'flex-1 ' : ''}min-h-0 overflow-y-auto${props.padded ? ' p-4 sm:p-6' : ''}`,
    footer: 'shrink-0 flex flex-row items-center justify-between gap-2 border-t border-default p-4 sm:px-6'
  }
})

// Drag the header down to dismiss. Native vaul dismiss stays governed by
// `dismissible` (default false, so outside-tap / Esc don't close by accident);
// this gesture and the ✕ button are the deliberate exits.
const headerEl = ref<HTMLElement | null>(null)
useDrawerDragToClose(headerEl, {
  onClose: () => emit('update:open', false),
  disabled: () => !props.dragToClose || props.loading
})
</script>
