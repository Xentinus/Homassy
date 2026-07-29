<script setup lang="ts">
/**
 * Shared wrapper for every animated list/grid in the app.
 *
 * Renders a <TransitionGroup> so the transition name, the container classes and
 * the leave-pinning logic live in one place instead of being repeated per call
 * site. The CSS is defined once in app/assets/css/main.css (`.bubble-*`).
 *
 * Usage — this REPLACES the plain <div> that held the v-for, and takes over its
 * classes (a nested wrapper would break `grid` and `space-y-*`, both of which
 * are direct-child selectors):
 *
 *   <AnimatedList class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
 *     <DetailedProductCard v-for="p in items" :key="p.publicId" :product="p" />
 *   </AnimatedList>
 *
 * Two rules for the slot content:
 *   - exactly one root element per keyed child (wrap a v-if/v-else pair in a div)
 *   - a stable :key from the server (publicId) — never the v-for index
 *
 * Also: no child's root may carry a CSS `animation` longer than the move
 * duration. TransitionGroup detects FLIP support by cloning the first child and
 * reading its computed timings; a longer animation makes it report "no
 * transform transition" and silently disables the move animation entirely.
 *
 * Note that this component never re-renders when the list changes — the slot is
 * a stable compiled slot, so the parent patches the transition's children
 * directly. All the logic below therefore runs from the transition hooks, never
 * from this component's own update hooks (see `openBatch`).
 *
 * Do not set inheritAttrs: false — the call site's `class` reaches the rendered
 * container through attribute fallthrough and is merged with `relative` below.
 */
const props = withDefaults(defineProps<{
  /** Element rendered as the list container. */
  tag?: string
  /** Transition name; the classes are defined once in main.css. */
  name?: string
  /** Animate the items already present on the list's first render. */
  appear?: boolean
  /** Stagger that first render (no effect when `appear` is false). */
  stagger?: boolean
  /** Highest index that still gets a delay, so long lists stay snappy. */
  staggerLimit?: number
  /**
   * Most items animated in a single update. A filter change can drop dozens of
   * cards at once; past this many the rest are removed/inserted without
   * animation rather than compositing that many layers on a mid-range phone.
   */
  batchLimit?: number
  /** Render a plain container with no transition at all (static flag). */
  disabled?: boolean
}>(), {
  tag: 'div',
  name: 'bubble',
  appear: true,
  stagger: true,
  staggerLimit: 12,
  batchLimit: 12,
  disabled: false
})

// prefers-reduced-motion is honoured in CSS (transition: none). The pinning
// below has to be skipped too, otherwise a removed card is yanked out of flow
// for the frame it takes to remove it.
const reducedMotion = () =>
  import.meta.client && window.matchMedia('(prefers-reduced-motion: reduce)').matches

interface Box { top: number, left: number, width: number, height: number }

/**
 * Layout of every in-flow child, captured on the first removal of a patch — so a
 * leaving card can be pinned to its own cell and the survivors can close the gap
 * under the FLIP `-move` transition.
 *
 * It has to be a snapshot, not a per-card measurement: pinning takes a card out
 * of flow, so the grid reflows immediately and every *later* removal in the same
 * batch would measure a shifted cell. Filtering drops many cards at once, so
 * this is the common case, not an edge case.
 */
let boxes: Map<HTMLElement, Box> | null = null

const measure = (container: HTMLElement) => {
  const map = new Map<HTMLElement, Box>()
  boxes = map
  const cRect = container.getBoundingClientRect()
  const cStyle = getComputedStyle(container)
  // `top`/`left` on an abspos child resolve against the padding box and are
  // unaffected by scrolling — fold the border and the scroll offset in here.
  const originTop = cRect.top + Number.parseFloat(cStyle.borderTopWidth) - container.scrollTop
  const originLeft = cRect.left + Number.parseFloat(cStyle.borderLeftWidth) - container.scrollLeft

  for (const child of Array.from(container.children)) {
    const el = child as HTMLElement
    if (el.style.position === 'absolute') continue // already leaving
    const r = el.getBoundingClientRect()
    map.set(el, {
      top: r.top - originTop,
      left: r.left - originLeft,
      width: r.width,
      height: r.height
    })
  }
}

let enterCount = 0
let leaveCount = 0
let batchOpen = false

/**
 * Per-batch bookkeeping.
 *
 * This component's own update hooks (onBeforeUpdate/onUpdated) are NOT usable
 * here: the slot content is a stable compiled slot, so the parent patches the
 * transition's children directly without re-rendering this component, and the
 * hooks never fire. Everything therefore hangs off the first transition hook of
 * a patch, and a microtask closes the batch — Vue's scheduler flush is
 * synchronous, so a microtask queued from inside it runs once the whole flush
 * (patch + post-render enter hooks) is done.
 *
 * Without this the counters below would never reset and `batchLimit` would
 * permanently disable the animation after that many cumulative items.
 */
const openBatch = () => {
  if (batchOpen) return
  batchOpen = true
  queueMicrotask(() => {
    batchOpen = false
    boxes = null // also drops references to the detached leaving nodes
    enterCount = 0
    leaveCount = 0
  })
}

/**
 * Opt an element out of its transition. Inline style beats the transition
 * class, so the computed duration is 0 and Vue resolves on the next frame
 * instead of waiting for a transitionend. Cleaned up in the after-hooks.
 */
const skip = (el: Element) => {
  (el as HTMLElement).style.transition = 'none'
}
const unskip = (el: Element) => {
  (el as HTMLElement).style.removeProperty('transition')
}

/**
 * Undo the leave pinning. Needed when a leave is *cancelled* — an item removed
 * and re-added within the leave duration keeps the same element, which would
 * otherwise stay absolutely positioned for good.
 */
const unpin = (el: Element) => {
  const style = (el as HTMLElement).style
  for (const prop of ['position', 'top', 'left', 'width', 'height', 'margin', 'pointer-events', 'transition']) {
    style.removeProperty(prop)
  }
}

// Every hook below takes exactly one argument on purpose: Vue treats a two-arg
// hook as taking an explicit `done` callback and would wait forever for it.
const onBeforeLeave = (el: Element) => {
  openBatch()
  if (props.disabled || reducedMotion()) return
  if (++leaveCount > props.batchLimit) return skip(el)

  const node = el as HTMLElement
  const container = node.parentElement
  if (!container) return

  // First removal of this patch: snapshot the list while every card is still in
  // flow and nothing has been pinned yet. Vue unmounts before it moves survivors,
  // so this is the pre-update geometry for the whole batch.
  if (!boxes) measure(container)

  const box = boxes?.get(node)
  if (!box) return

  const style = node.style
  style.position = 'absolute'
  style.top = `${box.top}px`
  style.left = `${box.left}px`
  style.width = `${box.width}px`
  style.height = `${box.height}px`
  style.margin = '0' // space-y-* margins would offset an abspos box
  style.pointerEvents = 'none' // a bursting card must not swallow taps
}

// Stagger the first render only. Vue calls @appear exclusively for the initial
// pass and @enter for every later insertion, so cards arriving over SignalR or
// from infinite scroll come in without a delay. The index is the hook call
// order, which is the mount order — call sites never pass one.
let appearIndex = 0
const onAppear = (el: Element) => {
  if (!props.stagger) return
  const i = Math.min(appearIndex++, props.staggerLimit)
  ;(el as HTMLElement).style.setProperty('--bubble-index', String(i))
}
const onAfterAppear = (el: Element) => {
  (el as HTMLElement).style.removeProperty('--bubble-index')
}

const onEnter = (el: Element) => {
  openBatch()
  if (++enterCount > props.batchLimit) skip(el)
}
</script>

<template>
  <!-- `disabled` renders a plain container rather than passing :css="false" —
       with css: false Vue waits on a `done` callback the one-arg hooks above
       never call, which would strand leaving elements in the DOM. -->
  <component :is="tag" v-if="disabled" class="relative">
    <slot />
  </component>
  <TransitionGroup
    v-else
    :tag="tag"
    :name="name"
    :appear="appear"
    class="relative"
    @appear="onAppear"
    @after-appear="onAfterAppear"
    @appear-cancelled="onAfterAppear"
    @enter="onEnter"
    @after-enter="unskip"
    @enter-cancelled="unskip"
    @before-leave="onBeforeLeave"
    @leave-cancelled="unpin"
  >
    <slot />
  </TransitionGroup>
</template>
