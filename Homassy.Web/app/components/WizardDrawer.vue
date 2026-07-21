<template>
  <AppDrawer
    :open="open"
    :title="title"
    :icon="icon"
    :dismissible="dismissible"
    :loading="loading"
    @update:open="(value) => emit('update:open', value)"
  >
    <template #header-extra>
      <!-- Progress bar with the current step's label at its end -->
      <div class="flex items-center gap-3">
        <UProgress
          :model-value="currentStep + 1"
          :max="steps.length"
          size="sm"
          class="flex-1"
        />
        <span class="shrink-0 text-sm font-medium text-muted">
          {{ steps[currentStep]?.label }}
        </span>
      </div>
    </template>

    <slot :current-step="currentStep" />

    <template #footer>
      <!-- When overrideFooter is set, the parent supplies its own buttons
           (e.g. a "Create" action that replaces the stepper for a sub-form). -->
      <slot v-if="overrideFooter" name="footer" />
      <template v-else>
        <UButton
          :label="t('common.previous')"
          color="neutral"
          variant="ghost"
          icon="i-lucide-arrow-left"
          :disabled="!resolvedCanGoBack"
          @click="emit('previous')"
        />
        <UButton
          v-if="!isLastStep"
          :label="t('common.next')"
          color="primary"
          trailing-icon="i-lucide-arrow-right"
          :disabled="!canProceed"
          @click="emit('next')"
        />
        <UButton
          v-else
          :label="finishLabel || t('common.finish')"
          color="primary"
          icon="i-lucide-check"
          :loading="loading"
          :disabled="finishDisabled"
          @click="emit('finish')"
        />
      </template>
    </template>
  </AppDrawer>
</template>

<script setup lang="ts">
import { computed } from 'vue'

/**
 * A step-by-step wizard rendered as a bottom sheet: a header with a progress
 * bar and the active step's label, a scrollable body for the step content, and
 * a fixed footer with Previous / Next / Finish. Built on `AppDrawer`, so it
 * shares the app-wide drawer chrome (size, ✕ close, drag-to-close).
 *
 * The parent owns `currentStep` and the per-step logic: this component only
 * renders the chrome and emits `previous` / `next` / `finish` — so it can be
 * reused by any multi-step flow. Gate the buttons with `canProceed` /
 * `canGoBack` / `loading`.
 */
interface WizardStep {
  /** Shown at the end of the progress bar while the step is active. */
  label: string
}

const props = withDefaults(defineProps<{
  /** Visibility (use with v-model:open). */
  open: boolean
  /** Header title. */
  title: string
  /** Ordered steps — drives the progress bar and the status label. */
  steps: WizardStep[]
  /** Zero-based index of the active step (controlled by the parent). */
  currentStep: number
  /** Optional Lucide/Heroicons icon shown before the title. */
  icon?: string
  /** Enables the Next button — the parent gates per-step validity. */
  canProceed?: boolean
  /** Enables Previous. When null (default) it falls back to `currentStep > 0`. */
  canGoBack?: boolean | null
  /** Loading state for the Finish button. */
  loading?: boolean
  /** Disables the last-step Finish button (Finish is NOT gated by canProceed). */
  finishDisabled?: boolean
  /** Allow dragging/clicking outside to dismiss (default false). */
  dismissible?: boolean
  /** Custom label for the last-step button (defaults to common.finish). */
  finishLabel?: string
  /** Replace the default Previous/Next/Finish footer with the `#footer` slot
   *  (e.g. a "Create" action for an inline sub-form). */
  overrideFooter?: boolean
}>(), {
  icon: undefined,
  canProceed: true,
  canGoBack: null,
  loading: false,
  finishDisabled: false,
  dismissible: false,
  finishLabel: undefined,
  overrideFooter: false
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  previous: []
  next: []
  finish: []
}>()

const { t } = useI18n()

const isLastStep = computed(() => props.currentStep >= props.steps.length - 1)
const resolvedCanGoBack = computed(() =>
  props.canGoBack === null ? props.currentStep > 0 : props.canGoBack
)
</script>
