<template>
  <!-- `data-tour="fab"` here as well as on `NavFab`: the first-run tour points at whichever
       of the two is the visible one at this width (see `useOnboardingTour.findTarget`). -->
  <div v-if="fabVisible" data-tour="fab" class="px-1 pb-2">
    <!-- One action: a plain primary button that runs it. -->
    <UButton
      v-if="actions.length === 1"
      block
      size="lg"
      color="primary"
      icon="i-lucide-plus"
      :label="actions[0]?.label"
      @click="run(actions[0]!)"
    />

    <!-- Several: the same chooser the FAB shows, as a menu under the button. -->
    <UDropdownMenu v-else :items="menuItems" :content="{ align: 'start' }" class="w-full">
      <UButton
        block
        size="lg"
        color="primary"
        icon="i-lucide-plus"
        trailing-icon="i-lucide-chevron-down"
        :label="t('fab.chooseAction')"
      />
    </UDropdownMenu>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { DropdownMenuItem } from '@nuxt/ui'
import type { FabAction } from '~/composables/useFabActions'

/**
 * The desktop sidebar's add button (#122).
 *
 * The same thing `NavFab` is on a phone, in the shape a desktop window wants: a primary
 * button in the sidebar instead of a circle floating over a bottom bar. It reads the same
 * `useFabActions` registration, so a page still declares what "add" means exactly once and
 * neither surface can offer something the other does not.
 */
const { actions, fabVisible } = useFab()
const { t } = useI18n()
const haptics = useHaptics()

const run = (action: FabAction) => {
  haptics.tap()
  action.handler()
}

const menuItems = computed<DropdownMenuItem[]>(() => actions.value.map(action => ({
  label: action.label,
  icon: action.icon || 'i-lucide-plus',
  onSelect: () => run(action)
})))
</script>
