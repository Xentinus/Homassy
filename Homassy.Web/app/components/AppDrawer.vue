<template>
  <UDrawer
    :open="open"
    :dismissible="dismissible && closable"
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
            v-if="closable"
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
  /** Show the ✕ close button and allow drag/backdrop close (default true). Set false to lock the sheet (e.g. while processing). */
  closable?: boolean
  /** Force a high z-index so this drawer stacks above another drawer it opens over (nested/child overlays). */
  elevated?: boolean
}>(), {
  icon: undefined,
  description: undefined,
  dismissible: false,
  fit: 'full',
  dragToClose: true,
  loading: false,
  padded: true,
  closable: true,
  elevated: false
})

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

const { t } = useI18n()

const ui = computed(() => {
  const bodyPad = props.padded ? ' p-4 sm:p-6' : ''
  // vaul drawers carry no explicit z-index — they stack purely by DOM order,
  // which follows *declaration* order, not open order. So a drawer opened from
  // inside another drawer can render *behind* its parent if it is declared
  // earlier in the template. `elevated` forces this drawer above with an
  // explicit z-index so nested/child drawers always paint on top.
  const zContent = props.elevated ? ' z-[90]' : ''
  const base = {
    // NOTE: `fit` is accepted for API stability but always resolves to the fixed
    // near-fullscreen height. Auto/content-height bottom drawers do not position
    // correctly in this vaul-vue / Nuxt UI version — they render below the
    // viewport (transform = translateY(fullHeight), `--snap-point-height: 0`),
    // leaving only a click-blocking overlay. A fixed height is the reliable option.
    content: `h-[94dvh] rounded-t-2xl overflow-hidden${zContent}`,
    container: 'flex flex-1 flex-col min-h-0 gap-0 p-0 overflow-hidden',
    header: 'shrink-0 border-b border-default p-4 sm:px-6',
    body: `flex-1 min-h-0 overflow-y-auto${bodyPad}`,
    footer: 'shrink-0 flex flex-row items-center justify-between gap-2 border-t border-default p-4 sm:px-6'
  }
  if (props.elevated) base.overlay = 'z-[80]'
  return base
})

// Drag the header down to dismiss. Native vaul dismiss stays governed by
// `dismissible` (default false, so outside-tap / Esc don't close by accident);
// this gesture and the ✕ button are the deliberate exits.
const headerEl = ref<HTMLElement | null>(null)
useDrawerDragToClose(headerEl, {
  onClose: () => emit('update:open', false),
  disabled: () => !props.dragToClose || !props.closable || props.loading
})
</script>
