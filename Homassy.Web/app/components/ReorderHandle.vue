<template>
  <button
    type="button"
    class="reorder-handle inline-flex items-center justify-center rounded-lg text-dimmed hover:text-toned focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary transition-colors"
    :class="[sizeClass, { 'text-primary': dragging }]"
    :aria-label="label ?? $t('common.reorder.handle')"
    :aria-describedby="describedBy"
    :aria-pressed="dragging"
    @pointerdown="emit('lift', $event)"
    @keydown.up.prevent="emit('move', -1)"
    @keydown.down.prevent="emit('move', 1)"
    @keydown.left.prevent="emit('move', -1)"
    @keydown.right.prevent="emit('move', 1)"
    @click.stop
  >
    <UIcon name="i-lucide-grip-vertical" class="h-4 w-4" />
  </button>
</template>

<script setup lang="ts">
/**
 * The grab affordance for a reorderable card (#113).
 *
 * A handle rather than the whole card: these cards already own a tap (open) and a horizontal swipe
 * (edit / delete), and a long press on the card itself would be a fourth gesture competing with
 * text selection and the browser's own context menu.
 *
 * `touch-action: none` is set in CSS rather than as an attribute because it has to win over the
 * `pan-y` the surrounding card sets for its swipe gesture — without it the browser claims the
 * vertical drag for scrolling and the pointer events stop arriving mid-gesture.
 *
 * The arrow keys move the row one position at a time, so the list is reorderable without a pointer
 * at all; a drag gesture has no keyboard equivalent to fall back on.
 */
const props = withDefaults(defineProps<{
  /** True while this row is the one being dragged — the handle takes the accent colour. */
  dragging?: boolean
  /** Overrides the generic handle label, e.g. with the row's own name. */
  label?: string
  /** Id of an element describing the gesture, announced after the label. */
  describedBy?: string
  size?: 'sm' | 'md'
}>(), {
  dragging: false,
  label: undefined,
  describedBy: undefined,
  size: 'md'
})

const emit = defineEmits<{
  /** The pointer went down on the handle — the caller starts the drag from here. */
  lift: [event: PointerEvent]
  /** An arrow key asked for a one-position move. */
  move: [direction: -1 | 1]
}>()

const sizeClass = computed(() => (props.size === 'sm' ? 'h-7 w-6' : 'h-8 w-7'))
</script>

<style scoped>
.reorder-handle {
  touch-action: none;
  cursor: grab;
}

.reorder-handle:active {
  cursor: grabbing;
}
</style>
