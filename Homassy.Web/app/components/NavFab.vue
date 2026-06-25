<template>
  <div
    v-if="actions.length"
    class="absolute left-1/2 top-0 z-30 -translate-x-1/2 -translate-y-1/2"
  >
    <!-- Dimmed backdrop while the chooser is open (teleported so it sits below the nav) -->
    <Teleport to="body">
      <Transition name="fab-fade">
        <div
          v-if="open"
          class="fixed inset-0 z-40 bg-black/40 backdrop-blur-[2px]"
          @click="close"
        />
      </Transition>
    </Teleport>

    <!-- Speed-dial: heading + stacked options, anchored above the button -->
    <div class="absolute bottom-full left-1/2 mb-4 flex -translate-x-1/2 flex-col items-end gap-2.5">
      <Transition name="fab-fade">
        <span
          v-if="open"
          class="mr-1 whitespace-nowrap rounded-full bg-gray-900/90 px-3 py-1 text-xs font-medium text-white shadow-md dark:bg-white/90 dark:text-gray-900"
        >
          {{ $t('fab.chooseAction') }}
        </span>
      </Transition>

      <TransitionGroup
        tag="div"
        name="fab-option"
        class="flex flex-col items-end gap-2.5"
      >
        <button
          v-for="(action, index) in openActions"
          :key="action.label"
          type="button"
          class="group flex items-center gap-3 whitespace-nowrap"
          :style="{ transitionDelay: open ? `${(actions.length - 1 - index) * 45}ms` : '0ms' }"
          @click="select(action)"
        >
          <span class="rounded-lg bg-white px-3 py-1.5 text-sm font-medium text-gray-900 shadow-md ring-1 ring-black/5 dark:bg-gray-800 dark:text-gray-100 dark:ring-white/10">
            {{ action.label }}
          </span>
          <span class="flex h-11 w-11 items-center justify-center rounded-full bg-primary-500 text-white shadow-lg transition group-hover:scale-105 group-hover:bg-primary-600 group-active:scale-95">
            <UIcon :name="action.icon || 'i-lucide-plus'" class="h-5 w-5" />
          </span>
        </button>
      </TransitionGroup>
    </div>

    <!-- Main floating add button -->
    <button
      type="button"
      :aria-label="open ? $t('fab.close') : $t('fab.open')"
      :aria-expanded="open"
      class="flex h-14 w-14 items-center justify-center rounded-full bg-primary-500 text-white shadow-lg ring-4 ring-background transition duration-200 hover:scale-105 hover:bg-primary-600 active:scale-95"
      @click="onClick"
    >
      <UIcon
        name="i-lucide-plus"
        class="h-7 w-7 transition-transform duration-300"
        :class="open ? 'rotate-[135deg]' : ''"
      />
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import type { FabAction } from '~/composables/useFabActions'

const { actions } = useFab()
const route = useRoute()

const open = ref(false)

// Only render option buttons while open — keeps the closed state clean and lets
// the TransitionGroup animate enter/leave.
const openActions = computed<FabAction[]>(() => (open.value ? actions.value : []))

const close = () => {
  open.value = false
}

const onClick = () => {
  // Single action → run it straight away, no chooser.
  if (actions.value.length === 1) {
    actions.value[0]?.handler()
    return
  }
  open.value = !open.value
}

const select = (action: FabAction) => {
  open.value = false
  action.handler()
}

const onKeydown = (event: KeyboardEvent) => {
  if (event.key === 'Escape') close()
}

onMounted(() => window.addEventListener('keydown', onKeydown))
onUnmounted(() => window.removeEventListener('keydown', onKeydown))

// Close the chooser when navigating away or when the actions no longer warrant it.
watch(() => route.fullPath, close)
watch(() => actions.value.length, (len) => {
  if (len <= 1) open.value = false
})
</script>

<style scoped>
.fab-fade-enter-active,
.fab-fade-leave-active {
  transition: opacity 0.2s ease;
}
.fab-fade-enter-from,
.fab-fade-leave-to {
  opacity: 0;
}

.fab-option-enter-active {
  transition: opacity 0.3s ease, transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.fab-option-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}
.fab-option-enter-from,
.fab-option-leave-to {
  opacity: 0;
  transform: translateY(12px) scale(0.85);
}
</style>
