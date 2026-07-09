<template>
  <component
    :is="tag"
    :to="to || undefined"
    :type="isButton ? 'button' : undefined"
    :disabled="isButton && (disabled || loading) ? true : undefined"
    class="w-full flex items-center gap-3 px-4 py-3 min-h-12 text-left transition-colors"
    :class="interactive
      ? 'active:bg-elevated hover:bg-elevated/60 disabled:opacity-50 disabled:pointer-events-none focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500/50'
      : ''"
    @click="onClick"
  >
    <slot name="leading">
      <UIcon
        v-if="icon"
        :name="icon"
        class="h-5 w-5 shrink-0"
        :class="variant === 'danger' ? 'text-error' : 'text-primary-500'"
      />
    </slot>

    <div class="flex-1 min-w-0">
      <p class="truncate font-medium" :class="{ 'text-error': variant === 'danger' }">
        {{ label }}
      </p>
      <p v-if="description" class="text-xs text-muted truncate">
        {{ description }}
      </p>
    </div>

    <slot name="trailing">
      <UIcon
        v-if="loading"
        name="i-lucide-loader-2"
        class="h-4 w-4 shrink-0 animate-spin text-muted"
      />
      <template v-else>
        <span v-if="value" class="text-sm text-muted truncate max-w-[45%]">{{ value }}</span>
        <UIcon
          v-if="chevron"
          name="i-lucide-chevron-right"
          class="h-4 w-4 shrink-0 text-dimmed"
        />
      </template>
    </slot>
  </component>
</template>

<script setup lang="ts">
import { computed } from 'vue'

/**
 * A single settings row. Renders a NuxtLink when `to` is set (navigates), else
 * a real <button> (emits `select`) for keyboard/focus/aria. Superset of the old
 * ButtonCard. Use the #leading / #trailing slots to override the icon or the
 * trailing value+chevron (e.g. a switch or a segmented control).
 */
const props = withDefaults(defineProps<{
  label: string
  description?: string
  icon?: string
  /** Trailing inline value (e.g. the current language). */
  value?: string
  /** When set, the row is a link to this route. */
  to?: string
  variant?: 'default' | 'danger'
  /** Show the trailing chevron. Defaults to true. */
  chevron?: boolean
  /** Show a trailing spinner (e.g. while saving) instead of value+chevron. */
  loading?: boolean
  disabled?: boolean
  /** Non-interactive row (renders a <div>) — for rows whose control lives in #trailing. */
  static?: boolean
}>(), {
  description: undefined,
  icon: undefined,
  value: undefined,
  to: undefined,
  variant: 'default',
  chevron: true,
  loading: false,
  disabled: false,
  static: false
})

const emit = defineEmits<{ select: [] }>()

const NuxtLink = resolveComponent('NuxtLink')
const isButton = computed(() => !props.to && !props.static)
const interactive = computed(() => !props.static)
const tag = computed(() => (props.to ? NuxtLink : props.static ? 'div' : 'button'))

function onClick(event: MouseEvent) {
  if (props.to || props.static) return
  if (props.disabled || props.loading) {
    event.preventDefault()
    return
  }
  emit('select')
}
</script>
