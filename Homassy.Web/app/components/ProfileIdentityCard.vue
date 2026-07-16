<template>
  <div class="rounded-xl border border-default overflow-hidden bg-default">
    <div v-if="loading" class="flex items-center gap-4 px-4 py-4">
      <USkeleton class="h-16 w-16 rounded-full shrink-0" />
      <div class="flex-1 min-w-0 space-y-2">
        <USkeleton class="h-5 w-40" />
        <USkeleton class="h-4 w-28" />
      </div>
    </div>

    <button
      v-else
      type="button"
      class="w-full flex items-center gap-4 px-4 py-4 text-left transition-colors active:bg-elevated hover:bg-elevated/60 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500/50"
      @click="onSelect"
    >
      <div class="border-2 border-primary-500 rounded-full p-0.5 shrink-0">
        <UAvatar
          :src="avatarSrc"
          :alt="primaryName || 'User'"
          :text="avatarInitial"
          class="h-16 w-16 text-2xl"
        />
      </div>

      <div class="flex-1 min-w-0">
        <p class="text-lg font-semibold truncate">{{ primaryName }}</p>
        <p v-if="secondaryName" class="text-sm text-muted truncate">
          {{ secondaryName }}
        </p>
      </div>

      <UIcon name="i-lucide-chevron-right" class="h-4 w-4 shrink-0 text-dimmed" />
    </button>
  </div>
</template>

<script setup lang="ts">
/**
 * Tappable profile identity card: avatar on the left, primary name next to it
 * with the secondary (real) name underneath. Emits `select` to open the
 * "Edit profile" drawer. Mirrors the SettingsGroup/SettingsRow visual language.
 */
withDefaults(defineProps<{
  primaryName?: string
  secondaryName?: string | null
  avatarSrc?: string
  avatarInitial?: string
  loading?: boolean
}>(), {
  primaryName: undefined,
  secondaryName: undefined,
  avatarSrc: undefined,
  avatarInitial: undefined,
  loading: false
})

const emit = defineEmits<{ select: [] }>()

// Drop focus before the parent opens the edit-profile drawer, so the trigger
// isn't left focused inside the aria-hidden app root (browser a11y warning).
function onSelect(event: MouseEvent) {
  const el = event.currentTarget as HTMLElement | null
  el?.blur()
  emit('select')
}
</script>
