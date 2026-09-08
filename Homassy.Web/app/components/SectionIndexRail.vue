<template>
  <div
    v-if="sections.length >= minSections"
    class="fixed right-0 z-30 flex touch-none select-none items-center"
    :style="railStyle"
    @pointerdown="onPointerDown"
    @pointermove="onPointerMove"
    @pointerup="onPointerUp"
    @pointercancel="onPointerUp"
  >
    <!-- Floating bubble naming what the scrub is currently over. Only while dragging: the rail is
         a scrubber, not a legend. -->
    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="opacity-0 translate-x-2"
      leave-active-class="transition duration-150 ease-in"
      leave-to-class="opacity-0 translate-x-2"
    >
      <div
        v-if="activeIndex !== null"
        class="pointer-events-none absolute right-9 max-w-[60vw] truncate rounded-lg bg-inverted px-3 py-1.5 text-sm font-semibold text-inverted shadow-lg"
        :style="{ top: `${bubbleTop}px`, transform: 'translateY(-50%)' }"
      >{{ sections[activeIndex]?.label }}</div>
    </Transition>

    <ul
      ref="track"
      class="flex flex-col items-center gap-0.5 rounded-l-lg bg-default/70 py-2 pl-1 pr-1.5 backdrop-blur"
      :aria-label="ariaLabel"
    >
      <li v-for="(section, i) in sections" :key="section.key">
        <button
          type="button"
          class="flex h-4 w-5 items-center justify-center text-[10px] font-semibold uppercase leading-none transition-colors"
          :class="i === activeIndex ? 'text-primary-600 dark:text-primary-400' : 'text-dimmed'"
          :aria-label="section.label"
          @click="select(i)"
        >{{ section.tick }}</button>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import type { CSSProperties } from 'vue'

/**
 * The right-edge index for a long grouped list: tap a tick to jump to that section, or drag along
 * the rail to scrub through them with a bubble naming where you are.
 *
 * It deliberately knows nothing about what is being listed. It takes the sections, reports which
 * one was picked, and leaves revealing and scrolling to the page — which matters because the
 * target section may not be rendered yet: this list pages in with an `IntersectionObserver`, so
 * the sections come from the whole filtered dataset while only the first slice is on screen.
 */
export interface IndexSection {
  key: string
  /** Full name, for the bubble and the accessible label. */
  label: string
  /** One or two characters for the rail itself. */
  tick: string
}

const props = withDefaults(defineProps<{
  sections: IndexSection[]
  /** Below this many sections the rail is more chrome than help, so it hides. */
  minSections?: number
  /** CSS length for the inset from the top of the viewport — the caller clears its own header. */
  top?: string
  /** CSS length for the inset from the bottom, to clear the floating bottom nav. */
  bottom?: string
  ariaLabel?: string
}>(), {
  minSections: 5,
  top: '120px',
  bottom: '120px',
  ariaLabel: undefined
})

const emit = defineEmits<{ select: [key: string] }>()

const haptics = useHaptics()

const track = ref<HTMLElement | null>(null)
/** Which tick the scrub is over. Null when not scrubbing — the rail shows no persistent state. */
const activeIndex = ref<number | null>(null)
const bubbleTop = ref(0)

const railStyle = computed<CSSProperties>(() => ({
  top: props.top,
  bottom: props.bottom
}))

function select(index: number) {
  const section = props.sections[index]
  if (!section) return
  emit('select', section.key)
}

/** Which tick a viewport y falls on, from the rail's own geometry rather than a fixed row height. */
function indexAt(clientY: number): number | null {
  const el = track.value
  if (!el || props.sections.length === 0) return null

  const box = el.getBoundingClientRect()
  const ratio = (clientY - box.top) / box.height
  const index = Math.floor(ratio * props.sections.length)

  return Math.min(Math.max(index, 0), props.sections.length - 1)
}

function scrub(clientY: number) {
  const index = indexAt(clientY)
  if (index === null || index === activeIndex.value) return

  activeIndex.value = index
  bubbleTop.value = clientY - (track.value?.getBoundingClientRect().top ?? 0)
  // Each detent crossed, not each pointer move: the rail's job is to feel like a row of stops.
  haptics.select()
  select(index)
}

function onPointerDown(event: PointerEvent) {
  try {
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
  } catch {
    // Not capturable; scrubbing still works while the pointer stays on the rail.
  }

  // A press is itself a pick, so a tap anywhere on the rail lands somewhere rather than waiting
  // for a move.
  activeIndex.value = null
  scrub(event.clientY)
}

function onPointerMove(event: PointerEvent) {
  if (activeIndex.value === null) return
  event.preventDefault()
  scrub(event.clientY)
}

function onPointerUp() {
  activeIndex.value = null
}
</script>
