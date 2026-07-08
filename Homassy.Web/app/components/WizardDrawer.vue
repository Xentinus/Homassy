<template>
  <UDrawer
    :open="open"
    :dismissible="dismissible"
    :ui="{
      content: 'h-[94dvh] rounded-t-2xl overflow-hidden',
      container: 'flex flex-1 flex-col min-h-0 gap-0 p-0 overflow-hidden',
      header: 'shrink-0 border-b border-default p-4 sm:px-6',
      body: 'flex-1 min-h-0 overflow-y-auto p-4 sm:p-6',
      footer: 'shrink-0 flex flex-row items-center justify-between gap-2 border-t border-default p-4 sm:px-6'
    }"
    @update:open="(value) => emit('update:open', value)"
  >
    <template #header>
      <div ref="headerEl" class="w-full space-y-4" style="touch-action: none">
        <div class="flex items-center gap-3">
          <UIcon v-if="icon" :name="icon" class="h-7 w-7 shrink-0 text-primary-500" />
          <h1 class="text-xl sm:text-2xl font-semibold">{{ title }}</h1>
          <UButton
            class="ml-auto"
            icon="i-lucide-x"
            color="neutral"
            variant="ghost"
            :aria-label="t('common.close')"
            @click="emit('update:open', false)"
          />
        </div>

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
      </div>
    </template>

    <template #body>
      <slot :current-step="currentStep" />
    </template>

    <template #footer>
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
  </UDrawer>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'

/**
 * A step-by-step wizard rendered as a bottom sheet (UDrawer): a header with a
 * progress bar and the active step's label, a scrollable body for the step
 * content, and a fixed footer with Previous / Next / Finish.
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
}>(), {
  icon: undefined,
  canProceed: true,
  canGoBack: null,
  loading: false,
  finishDisabled: false,
  dismissible: false,
  finishLabel: undefined
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  previous: []
  next: []
  finish: []
}>()

const { t } = useI18n()

// Drag the header down to dismiss. Native vaul dismiss stays disabled
// (dismissible: false) so outside-tap / Esc keep the wizard from closing by
// accident; only this gesture and the close button dismiss it.
const headerEl = ref<HTMLElement | null>(null)
useDrawerDragToClose(headerEl, {
  onClose: () => emit('update:open', false),
  disabled: () => props.loading
})

const isLastStep = computed(() => props.currentStep >= props.steps.length - 1)
const resolvedCanGoBack = computed(() =>
  props.canGoBack === null ? props.currentStep > 0 : props.canGoBack
)
</script>
