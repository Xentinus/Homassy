<template>
  <ul
    class="grid gap-2 list-none p-0 m-0"
    :style="{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }"
    :aria-label="$t('pages.household.presence.title')"
  >
    <li v-for="member in members" :key="member.publicId">
      <div
        class="h-full flex flex-col items-center gap-1.5 rounded-xl border px-1 py-2.5"
        :class="member.isShopping
          ? 'border-primary-300 dark:border-primary-700 bg-primary-50 dark:bg-primary-500/10'
          : 'border-default bg-default'"
      >
        <div class="relative">
          <UserAvatar
            :src="member.profilePictureUrl"
            :name="member.displayName"
            :public-id="member.publicId"
            :identity-color="member.identityColor"
            :size="34"
            :alt="member.displayName"
          />

          <!-- Online is the plain fact of being in this roster at all; the dot says which kind of
               online. A second device does not get a second dot — it is the same person. -->
          <span
            class="absolute -right-0.5 -bottom-0.5 h-2.5 w-2.5 rounded-full ring-2 ring-default"
            :class="member.isShopping ? 'bg-primary-500' : 'bg-emerald-500'"
            aria-hidden="true"
          />
        </div>

        <span class="text-xs font-semibold leading-tight text-center truncate max-w-full">
          {{ isSelf(member) ? $t('pages.household.presence.you') : firstName(member.displayName) }}
        </span>

        <span
          class="text-[10px] leading-tight text-center truncate max-w-full"
          :class="member.isShopping ? 'text-primary-600 dark:text-primary-400 font-bold' : 'text-muted'"
        >
          {{ statusLabel(member) }}
        </span>
      </div>
    </li>
  </ul>
</template>

<script setup lang="ts">
/**
 * Who in the household has the app open right now — the home screen's first row.
 *
 * Fed by `usePresenceSocket`, so a member is here because a socket of theirs is open, not because
 * they logged in once last week. The viewer is in the roster too (labelled "you"): the strip is a
 * picture of the household, and one that leaves you out reads like a list of other people.
 *
 * Shopping is the one status worth a colour of its own — it is the only one anybody acts on
 * ("they are at the shop, ask them to grab milk"), and it is the only state the client reports to
 * the server. The name beside it is the *list* being shopped, resolved server-side.
 *
 * Deliberately not `PresenceAvatars`: that one answers "who else is looking at this list" in a
 * corner of a header, as a dense overlapping stack with no names. This is the same idea given the
 * room to say who and what, which is what makes it worth the top of a screen.
 */
import type { FamilyPresenceMember } from '~/types/presence'

const props = defineProps<{
  members: FamilyPresenceMember[]
  /** The viewer's own public id, so their tile can say "you" instead of their name. */
  selfPublicId?: string | null
}>()

const { t } = useI18n()

/** Four across is the phone's comfortable fit; fewer members simply share the width. */
const columns = computed(() => Math.min(Math.max(props.members.length, 1), 4))

const isSelf = (member: FamilyPresenceMember) =>
  !!props.selfPublicId && member.publicId === props.selfPublicId

/** A household is on first-name terms, and a tile is about 70px wide. */
const firstName = (displayName: string) => displayName.trim().split(/\s+/)[0] || displayName

const statusLabel = (member: FamilyPresenceMember) => {
  if (member.isShopping) {
    return member.shoppingContext
      ? t('pages.household.presence.shoppingAt', { list: member.shoppingContext })
      : t('pages.household.presence.shopping')
  }

  return t('pages.household.presence.online')
}
</script>
