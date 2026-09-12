<template>
  <UDrawer
    :open="open"
    :dismissible="dismissible && closable"
    :direction="isSidePanel ? 'right' : 'bottom'"
    :handle="!isSidePanel"
    :modal="!isDetailPanel"
    :overlay="!isDetailPanel"
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
 * The app's standard drawer: a `UDrawer` with a fixed header (icon + title +
 * close ✕), a scrollable body, and an optional pinned footer for action buttons.
 *
 * This is the single source of truth for drawer chrome — every sheet (wizards,
 * forms, overviews, filters, the barcode scanner, …) wraps this so they are all
 * the same size, look identical, and are always closable via the ✕ button or the
 * drag gesture. Small confirm/progress dialogs stay `UModal`.
 *
 * **Below `lg` it is always a bottom sheet**, dragged down to dismiss. **Above
 * `lg` the `desktop` prop decides what it becomes** (#122) — a right-hand side
 * panel by default, a non-modal detail pane for read-only overviews, or a bottom
 * sheet still, for the few places where that is the right shape at any width.
 * One prop rather than a second component: a desktop-only drawer component would
 * be a second copy of this chrome to keep in step with this one.
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
  /**
   * Extra resting heights, as a fraction of the sheet's own height (e.g.
   * `[0.5, 1]`). The sheet still opens full height; the lower points are where a
   * downward drag can settle instead of closing. Below the top point the body
   * stops scrolling and a drag anywhere in the sheet moves it.
   *
   * Footer-less sheets only — a footer is pinned to the bottom of the content
   * and would be dragged off-screen with it.
   */
  snapPoints?: number[]
  /**
   * What this drawer becomes above `lg` (#122). Below `lg` it is a bottom sheet
   * whatever this says.
   *
   * - `panel` (default) — a right-hand side panel, still modal: the overlay and
   *   the focus trap stay, which is what a form or a wizard needs.
   * - `detail` — a right-hand side panel with no overlay and no scroll lock, so
   *   the list it was opened from stays readable and clickable next to it. This
   *   is the master/detail shape, and it is only right for a read-only overview:
   *   picking another row swaps what the pane shows instead of closing it.
   * - `sheet` — stay a bottom sheet at every width.
   */
  desktop?: 'panel' | 'detail' | 'sheet'
}>(), {
  icon: undefined,
  description: undefined,
  dismissible: false,
  fit: 'full',
  dragToClose: true,
  loading: false,
  padded: true,
  closable: true,
  elevated: false,
  snapPoints: undefined,
  desktop: 'panel'
})

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

const { t } = useI18n()
const { isDesktop } = useBreakpoint()

/** True while this drawer is a right-hand panel rather than a bottom sheet. */
const isSidePanel = computed(() => isDesktop.value && props.desktop !== 'sheet')

/** True while it is the non-modal master/detail pane — a stricter case of the above. */
const isDetailPanel = computed(() => isSidePanel.value && props.desktop === 'detail')

const ui = computed(() => {
  const bodyPad = props.padded ? ' p-4 sm:p-6' : ''
  // vaul drawers carry no explicit z-index — they stack purely by DOM order,
  // which follows *declaration* order, not open order. So a drawer opened from
  // inside another drawer can render *behind* its parent if it is declared
  // earlier in the template. `elevated` forces this drawer above with an
  // explicit z-index so nested/child drawers always paint on top.
  const zContent = props.elevated ? ' z-[90]' : ''
  // Typed explicitly so `overlay` can be added below: inferred from the literal, `base` would
  // have exactly the five keys it starts with and assigning a sixth is a type error.
  const base: Record<string, string> = {
    // NOTE: `fit` is accepted for API stability but always resolves to the fixed
    // near-fullscreen height. Auto/content-height bottom drawers do not position
    // correctly in this vaul-vue / Nuxt UI version — they render below the
    // viewport (transform = translateY(fullHeight), `--snap-point-height: 0`),
    // leaving only a click-blocking overlay. A fixed height is the reliable option.
    // A side panel is the same sheet turned on its side: a fixed width instead of a
    // fixed height, rounded on the edge that faces the list, and capped so it never
    // takes more than half of a very wide window away from the master list.
    // `mx-0` undoes the `mx-auto` `app.config.ts` sets for the centred bottom sheet: a panel
    // pinned to the right edge must not also try to centre itself.
    content: isSidePanel.value
      ? `h-full w-full max-w-xl mx-0 rounded-l-2xl overflow-hidden${zContent}`
      : `h-[94dvh] rounded-t-2xl overflow-hidden${zContent}`,
    container: 'flex flex-1 flex-col min-h-0 gap-0 p-0 overflow-hidden',
    header: 'shrink-0 border-b border-default p-4 sm:px-6',
    body: `flex-1 min-h-0 overflow-y-auto${bodyPad}`,
    footer: 'shrink-0 flex flex-row items-center justify-between gap-2 border-t border-default p-4 sm:px-6'
  }
  if (props.elevated) base.overlay = 'z-[80]'
  return base
})

// Drag the header down to dismiss (and, with `snapPoints`, to park the sheet at
// an intermediate height). Native vaul dismiss stays governed by `dismissible`
// (default false, so outside-tap / Esc don't close by accident); this gesture
// and the ✕ button are the deliberate exits.
const headerEl = ref<HTMLElement | null>(null)
// A side panel has no drag gesture: the composable's whole vocabulary is vertical
// (drag down to dismiss, park at a snap point), and there is no equivalent worth
// inventing for a panel pinned to the right edge of a desktop window with a mouse.
// Its ✕ button is the exit.
useDrawerDragToClose(headerEl, {
  onClose: () => emit('update:open', false),
  disabled: () => isSidePanel.value || !props.dragToClose || !props.closable || props.loading,
  snapPoints: () => (isSidePanel.value ? undefined : props.snapPoints)
})
</script>
