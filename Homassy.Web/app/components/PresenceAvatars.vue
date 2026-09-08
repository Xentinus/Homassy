<template>
  <TransitionGroup
    tag="div"
    name="bubble"
    class="flex items-center -space-x-2"
    role="group"
    :aria-label="$t('shoppingList.presence.othersHere', { count: members.length })"
  >
    <div
      v-for="member in visibleMembers"
      :key="member.publicId"
      class="relative shrink-0 rounded-full ring-2 ring-bg"
      :title="member.displayName"
    >
      <UChip
        :show="member.deviceCount > 1"
        :text="member.deviceCount"
        color="neutral"
        size="3xl"
      >
        <UserAvatar
          :src="member.profilePictureUrl"
          :name="member.displayName"
          :public-id="member.publicId"
          :identity-color="member.identityColor"
          :size="size"
          :alt="member.displayName"
        />
      </UChip>
    </div>

    <div
      v-if="overflowCount > 0"
      key="__overflow"
      class="relative shrink-0 flex items-center justify-center rounded-full ring-2 ring-bg bg-gray-200 dark:bg-gray-700"
      :style="{ width: `${size}px`, height: `${size}px` }"
      :title="$t('shoppingList.presence.overflow', { count: overflowCount })"
      :aria-label="$t('shoppingList.presence.overflow', { count: overflowCount })"
    >
      <span class="text-[10px] font-bold text-gray-700 dark:text-gray-200 leading-none tabular-nums">+{{ overflowCount }}</span>
    </div>
  </TransitionGroup>
</template>

<script setup lang="ts">
/**
 * Who else has this shopping list open right now: a small overlapping avatar stack fed by
 * `useShoppingListSocket`'s `presentMembers` (already excludes the current user — see that
 * composable). Rendered in the shopping-list page header, next to the title.
 *
 * Each avatar carries the member's identity ring + initials gradient (`UserAvatar`'s `publicId` /
 * `identityColor` props, backed by `useMemberColor`), so "who" is legible at a glance without a
 * second colour system. A member present on more than one device gets a small `UChip` badge —
 * the app's existing small-count-badge idiom (already used on this same page for the active
 * filter count) — rather than a new one invented for this task.
 *
 * Joins/leaves animate through the shared `bubble` transition (see AnimatedList.vue / main.css)
 * rather than a bespoke one, reusing the same `--bubble-*` tokens (and their
 * `prefers-reduced-motion` handling) the rest of the app already uses. This row is a plain
 * `<TransitionGroup>`, not `<AnimatedList>`: AnimatedList's extra leave-pinning logic exists for
 * CSS *grid* cards, whose cell can be reclaimed by grid auto-flow mid-leave; a flex row's slot
 * has no such reclaiming, so Vue's own built-in FLIP move-transition is enough on its own.
 */
import { computed } from 'vue'
import type { PresenceMember } from '~/types/realtime'

const props = withDefaults(defineProps<{
  members: PresenceMember[]
  /** Avatars shown before the rest collapse into a "+N" chip. */
  max?: number
  /** Rendered avatar size in CSS pixels (passed straight through to UserAvatar). */
  size?: number
}>(), {
  max: 4,
  size: 28
})

const visibleMembers = computed(() => props.members.slice(0, props.max))
const overflowCount = computed(() => Math.max(0, props.members.length - props.max))
</script>
