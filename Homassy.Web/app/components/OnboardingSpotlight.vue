<template>
  <Teleport to="body">
    <div
      v-if="isActive"
      class="onboarding-tour fixed inset-0 z-[100]"
      :class="{ 'onboarding-tour--still': reducedMotion }"
      role="dialog"
      aria-modal="true"
      :aria-labelledby="titleId"
      :aria-describedby="descriptionId"
    >
      <!-- The dim layer, with the target punched out of it. It swallows every pointer
           event on purpose: the tour is six taps long, and letting the user press the
           thing being pointed at mid-explanation just loses them. -->
      <div
        class="onboarding-tour__scrim absolute inset-0 bg-black/60"
        :style="scrimStyle"
      />

      <!-- The ring. A separate bordered box rather than a rounded clip-path, so the
           highlight reads as rounded without depending on `clip-path: path()`. -->
      <div
        v-if="layout"
        class="onboarding-tour__ring pointer-events-none absolute rounded-2xl ring-2 ring-primary-400"
        :style="ringStyle"
      />

      <!-- The card -->
      <div
        ref="cardRef"
        class="onboarding-tour__card absolute w-[min(22rem,calc(100vw-1.5rem))] rounded-xl border border-default bg-default p-4 shadow-xl"
        :style="cardStyle"
      >
        <p class="text-xs font-medium uppercase tracking-wide text-muted">
          {{ t('onboarding.stepCounter', { current: stepNumber, total: totalSteps }) }}
        </p>
        <p :id="titleId" class="mt-1 font-semibold text-highlighted">
          {{ t(`onboarding.steps.${currentStep?.key}.title`) }}
        </p>
        <p :id="descriptionId" class="mt-1 text-sm text-muted">
          {{ t(`onboarding.steps.${currentStep?.key}.description`) }}
        </p>

        <div class="mt-4 flex items-center justify-between gap-2">
          <UButton
            :label="t('onboarding.skip')"
            color="neutral"
            variant="ghost"
            size="sm"
            @click="onSkip"
          />
          <UButton
            :label="isLastStep ? t('onboarding.done') : t('onboarding.next')"
            color="primary"
            size="sm"
            :trailing-icon="isLastStep ? undefined : 'i-lucide-arrow-right'"
            @click="onNext"
          />
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
/**
 * The first-run tour's overlay (#98): a dimmed backdrop with a cut-out around the
 * current step's target and a tooltip card beside it.
 *
 * Mounted once, in the auth layout — not per page. The tour navigates between two
 * pages, and an overlay owned by a page would be unmounted underneath itself halfway
 * through. `useOnboardingTour` holds the state; this component only draws it.
 *
 * Teleported to `<body>` deliberately, which is the opposite of what `NavFab` needs:
 * `UApp` sets `isolation: isolate`, so a body-level overlay paints above the whole app
 * including the bottom nav — exactly right for a scrim that has to dim everything, and
 * for one that is supposed to swallow taps.
 *
 * Under `prefers-reduced-motion` the transitions between steps are dropped and each
 * step simply appears in place. The tour itself is unaffected — it is information, not
 * decoration.
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'
import { holeClipPath, spotlightLayout } from '~/utils/onboardingTour'

const { t } = useI18n()
const {
  isActive,
  targetRect,
  currentStep,
  stepNumber,
  totalSteps,
  isLastStep,
  next,
  skip,
  remeasure
} = useOnboardingTour()

const titleId = useId()
const descriptionId = useId()

const cardRef = ref<HTMLElement | null>(null)
/** Measured card height, so the layout can decide above/below from the real box. */
const cardHeight = ref(180)
const cardWidth = ref(352)
const viewport = ref({ width: 0, height: 0 })
const reducedMotion = ref(false)

const layout = computed(() => {
  const rect = targetRect.value
  if (!rect || !viewport.value.width) return null

  return spotlightLayout(
    { x: rect.x, y: rect.y, width: rect.width, height: rect.height },
    viewport.value,
    { width: cardWidth.value, height: cardHeight.value },
    currentStep.value?.padding
  )
})

/**
 * With no resolved target (a navigation is landing, or the step's element vanished)
 * the scrim dims everything and no hole is drawn. Better than a hole over whatever
 * happens to occupy the old element's coordinates.
 */
const scrimStyle = computed(() => (
  layout.value ? { clipPath: holeClipPath(layout.value.hole) } : {}
))

const ringStyle = computed(() => {
  const hole = layout.value?.hole
  if (!hole) return {}
  return {
    left: `${hole.x}px`,
    top: `${hole.y}px`,
    width: `${hole.width}px`,
    height: `${hole.height}px`
  }
})

const cardStyle = computed(() => {
  // Before the first measurement, park the card centred rather than at 0,0 — the
  // measure pass runs on the next frame and would otherwise show it jumping in.
  if (!layout.value) {
    return { left: '50%', top: '50%', transform: 'translate(-50%, -50%)' }
  }
  return { left: `${layout.value.cardLeft}px`, top: `${layout.value.cardTop}px` }
})

function measureViewport() {
  viewport.value = { width: window.innerWidth, height: window.innerHeight }
}

function measureCard() {
  const el = cardRef.value
  if (!el) return
  const rect = el.getBoundingClientRect()
  if (rect.height > 0) cardHeight.value = rect.height
  if (rect.width > 0) cardWidth.value = rect.width
}

function onViewportChange() {
  measureViewport()
  remeasure()
}

async function onNext() {
  await next()
}

async function onSkip() {
  await skip()
}

// The card's height changes with the step's text, and above/below is decided from it,
// so re-measure whenever the step changes rather than trusting the initial estimate.
watch([currentStep, isActive], async () => {
  if (!isActive.value) return
  await nextTick()
  measureCard()
})

onMounted(() => {
  measureViewport()
  reducedMotion.value = window.matchMedia('(prefers-reduced-motion: reduce)').matches
  window.addEventListener('resize', onViewportChange)
  // Passive + capture: the highlighted element can be inside a scrolling container,
  // and the hole has to follow it rather than being left behind.
  window.addEventListener('scroll', onViewportChange, { passive: true, capture: true })
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', onViewportChange)
  window.removeEventListener('scroll', onViewportChange, { capture: true })
})
</script>

<style scoped>
/* The hole and the ring glide between steps; the card fades and slides the short
   distance to its new anchor. `clip-path` is animatable as long as both ends are the
   same shape function with the same number of points, which `holeClipPath` guarantees. */
.onboarding-tour__scrim {
  transition: clip-path 320ms cubic-bezier(0.22, 1, 0.36, 1);
}

.onboarding-tour__ring {
  transition:
    left 320ms cubic-bezier(0.22, 1, 0.36, 1),
    top 320ms cubic-bezier(0.22, 1, 0.36, 1),
    width 320ms cubic-bezier(0.22, 1, 0.36, 1),
    height 320ms cubic-bezier(0.22, 1, 0.36, 1);
}

.onboarding-tour__card {
  transition:
    left 320ms cubic-bezier(0.22, 1, 0.36, 1),
    top 320ms cubic-bezier(0.22, 1, 0.36, 1);
}

.onboarding-tour--still .onboarding-tour__scrim,
.onboarding-tour--still .onboarding-tour__ring,
.onboarding-tour--still .onboarding-tour__card {
  transition: none;
}
</style>
