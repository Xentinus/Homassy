<template>
  <div
    class="relative overflow-hidden rounded-2xl border-2 bg-default"
    :class="[
      here ? 'border-blue-300/70 dark:border-blue-600/60' : 'border-default',
      attribution ? 'item-attribution-flash' : ''
    ]"
    :style="attribution?.style"
  >
    <!-- The whole row is the tick target. Everything else on this screen is secondary, so nothing
         else competes for the tap: the details toggle is a separate, deliberately smaller control. -->
    <button
      type="button"
      class="flex w-full items-center gap-3 p-4 text-left"
      :aria-label="$t('shoppingList.shoppingMode.tick', { name: displayName })"
      @click="emit('toggle')"
    >
      <span
        class="flex h-11 w-11 shrink-0 items-center justify-center rounded-full border-2 border-default text-transparent transition-colors"
        aria-hidden="true"
      >
        <UIcon name="i-lucide-check" class="h-6 w-6" />
      </span>

      <span class="min-w-0 flex-1">
        <span class="block truncate text-base font-semibold text-highlighted">{{ displayName }}</span>
        <span v-if="item.product?.brand" class="block truncate text-xs text-muted">{{ item.product.brand }}</span>
      </span>

      <!-- Quantity and unit, at the size the question "how many do I pick up" deserves. -->
      <span class="shrink-0 text-right">
        <span class="block text-xl font-bold tabular-nums text-highlighted">{{ item.quantity }}</span>
        <span class="block text-xs text-muted">{{ unitLabel }}</span>
      </span>
    </button>

    <!-- Everything the planning view shows on the card lives behind this one tap. -->
    <button
      v-if="hasDetails"
      type="button"
      class="flex w-full items-center justify-center gap-1 border-t border-default py-1.5 text-xs text-muted"
      :aria-expanded="detailsOpen"
      @click="toggleDetails"
    >
      <UIcon :name="detailsOpen ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'" class="h-4 w-4" />
      {{ detailsOpen ? $t('shoppingList.shoppingMode.hideDetails') : $t('shoppingList.shoppingMode.showDetails') }}
    </button>

    <div v-if="detailsOpen" class="space-y-1.5 border-t border-default px-4 py-3 text-sm">
      <p v-if="item.note" class="flex items-start gap-2 text-muted">
        <UIcon name="i-lucide-sticky-note" class="mt-0.5 h-4 w-4 shrink-0 text-purple-500" />
        <span class="italic">{{ item.note }}</span>
      </p>
      <p v-if="item.shoppingLocation" class="flex items-center gap-2 text-toned">
        <UIcon name="i-lucide-map-pin" class="h-4 w-4 shrink-0 text-blue-500" />
        {{ item.shoppingLocation.name }}
      </p>
      <p v-if="item.dueAt" class="flex items-center gap-2 text-toned">
        <UIcon name="i-lucide-calendar-clock" class="h-4 w-4 shrink-0 text-orange-500" />
        {{ formatDate(item.dueAt) }}
      </p>
      <p v-if="item.deadlineAt" class="flex items-center gap-2 text-toned">
        <UIcon name="i-lucide-calendar-x" class="h-4 w-4 shrink-0 text-red-500" />
        {{ formatDate(item.deadlineAt) }}
      </p>
    </div>

    <!-- Who else just touched this row. The point is not politeness: it is the thing that stops two
         people in the same shop putting the same item in the trolley (#97). -->
    <p v-if="attribution" class="item-attribution-label">
      <span class="item-attribution-dot" :style="attribution.style" />
      {{ $t('shoppingList.changedBy', { name: attribution.name }) }}
    </p>
  </div>
</template>

<script setup lang="ts">
/**
 * One row of shopping mode (#131). Large, single-purpose and self-contained: a tick target, a
 * quantity, and everything else folded away behind one tap.
 *
 * Separate from `ShoppingListItemCard` on purpose rather than as a variant of it — that card carries
 * a swipe gesture, an edit drawer, a purchase drawer, a delete confirmation and a lightbox, none of
 * which belong on a screen meant to be used while walking. Sharing it would mean a prop that turns
 * most of the component off.
 */
import type { CSSProperties } from 'vue'
import type { ShoppingListItemInfo } from '~/types/shoppingList'

const props = withDefaults(defineProps<{
  item: ShoppingListItemInfo
  /** Buyable at the store the user is standing in — the row takes the same blue the planning view uses. */
  here?: boolean
  attribution?: { name: string, style: CSSProperties } | null
}>(), {
  here: false,
  attribution: null
})

const emit = defineEmits<{
  /** The row was tapped — the parent turns that into a purchase. */
  toggle: []
}>()

const { t, locale } = useI18n()
const haptics = useHaptics()

const detailsOpen = ref(false)

const displayName = computed(() => props.item.product?.name || props.item.customName || t('common.unnamed'))

const unitLabel = computed(() =>
  props.item.unit === null || props.item.unit === undefined ? '' : t(`enums.unit.${props.item.unit}`))

const hasDetails = computed(() =>
  !!(props.item.note || props.item.shoppingLocation || props.item.dueAt || props.item.deadlineAt))

const formatDate = (value: string) => new Date(value).toLocaleDateString(locale.value)

const toggleDetails = () => {
  detailsOpen.value = !detailsOpen.value
  haptics.tap()
}
</script>
