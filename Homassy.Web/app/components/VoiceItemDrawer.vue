<template>
  <AppDrawer
    :open="open"
    :title="t('voice.title')"
    icon="i-lucide-mic"
    :loading="isSaving"
    :closable="!isSaving"
    @update:open="onOpenChange"
  >
    <div class="space-y-6">
      <!-- Hold-to-talk. A press-and-hold rather than a toggle because dictation has a natural
           end — you stop talking — and a toggle leaves the microphone open when you forget. -->
      <div class="flex flex-col items-center gap-3">
        <button
          ref="micEl"
          type="button"
          class="relative flex h-20 w-20 items-center justify-center rounded-full text-white shadow-lg transition-transform duration-200 select-none touch-none"
          :class="isListening ? 'scale-110 bg-error' : 'bg-primary-500 active:scale-95'"
          :aria-label="t('voice.holdToTalk')"
          :aria-pressed="isListening"
          @pointerdown="onPointerDown"
          @pointerup="onPointerUp"
          @pointercancel="onPointerCancel"
          @contextmenu.prevent
        >
          <span
            v-if="isListening"
            class="absolute inset-0 animate-ping rounded-full bg-error/40 motion-reduce:animate-none"
          />
          <UIcon name="i-lucide-mic" class="relative h-8 w-8" />
        </button>

        <p class="text-sm font-medium">
          {{ isListening ? t('voice.listening') : t('voice.holdToTalk') }}
        </p>

        <!-- The live transcript: what has been recognised, plus the words still being revised. -->
        <div
          v-if="isListening || spokenText"
          class="min-h-12 w-full rounded-xl border border-default bg-elevated px-3 py-2 text-center text-sm"
        >
          <span>{{ transcript }}</span>
          <span v-if="interim" class="text-muted"> {{ interim }}</span>
          <span v-if="!transcript && !interim" class="text-muted">{{ t('voice.speakNow') }}</span>
        </div>
      </div>

      <UAlert
        v-if="failure"
        :title="failureTitle"
        :description="failureHint"
        color="warning"
        variant="subtle"
        icon="i-lucide-triangle-alert"
      />

      <!-- The parsed rows. Nothing is created from them until the button at the bottom is
           pressed: a misheard word has to be one tap to fix, not a wrong item in the list. -->
      <div v-if="rows.length" class="space-y-3">
        <div class="flex items-center justify-between">
          <p class="text-sm font-semibold">{{ t('voice.review', { count: rows.length }) }}</p>
          <UButton
            :label="t('voice.clear')"
            size="xs"
            color="neutral"
            variant="ghost"
            icon="i-lucide-x"
            @click="clearRows"
          />
        </div>

        <div
          v-for="(row, index) in rows"
          :key="row.id"
          class="rounded-xl border border-default bg-default p-3 space-y-2"
        >
          <div class="flex items-center gap-2">
            <UBadge
              :label="badgeFor(row).label"
              :color="badgeFor(row).color"
              variant="subtle"
              size="sm"
              :icon="badgeFor(row).icon"
            />
            <span class="ml-auto truncate text-xs text-muted" :title="row.raw">{{ row.raw }}</span>
            <UButton
              icon="i-lucide-trash-2"
              color="neutral"
              variant="ghost"
              size="xs"
              :aria-label="t('common.delete')"
              @click="rows.splice(index, 1)"
            />
          </div>

          <UInput
            v-model="row.name"
            :placeholder="t('voice.namePlaceholder')"
            :disabled="row.match.kind === 'product'"
            class="w-full"
          />

          <!-- Only where a product is about to be created: the catalogue requires a brand, and
               guessing one without showing it would be exactly the silent commit this flow is
               built to avoid. -->
          <UInput
            v-if="needsProduct(row)"
            v-model="row.brand"
            :placeholder="t('voice.brandPlaceholder')"
            icon="i-lucide-tag"
            class="w-full"
          />

          <div class="flex gap-2">
            <UInputNumber
              v-model="row.quantity"
              :min="0.001"
              :step="1"
              class="w-32"
              :aria-label="t('common.quantity')"
            />
            <USelect
              v-model="row.unit"
              :items="unitOptions"
              class="flex-1"
              :aria-label="t('common.unit')"
            />
          </div>
        </div>
      </div>

      <!-- Said once, where it matters: the transcription is the browser's, not ours. -->
      <p class="flex items-start gap-2 text-xs text-muted">
        <UIcon name="i-lucide-info" class="mt-0.5 h-3.5 w-3.5 shrink-0" />
        {{ t('voice.privacyNote') }}
      </p>
    </div>

    <template #footer>
      <UButton
        :label="t('common.cancel')"
        color="neutral"
        variant="ghost"
        :disabled="isSaving"
        @click="close"
      />
      <UButton
        :label="t('voice.addAll', { count: rows.length })"
        color="primary"
        icon="i-lucide-check"
        :disabled="rows.length === 0"
        :loading="isSaving"
        @click="save"
      />
    </template>
  </AppDrawer>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Unit } from '~/types/enums'
import type { ProductInfo } from '~/types/product'
import type { CreateShoppingListItemEntry } from '~/types/shoppingList'
import { parseVoiceItems } from '~/utils/voiceItemParser'
import type { VoiceItemMatch } from '~/composables/useVoiceItemMatching'

/**
 * Dictate a few items and confirm them (#132).
 *
 * The flow is deliberately never silent: speech becomes a list of pre-filled, editable rows,
 * and nothing is written until the reader presses the button under them. Recognition being
 * imperfect is not a bug to design around — it is the reason the review step exists.
 *
 * It serves both add flows. On a shopping list an unmatched name becomes a free-text item,
 * which the list supports. Inventory has no free text — a stock row needs a product — so an
 * unmatched name creates the product first, which is why those rows are badged as new rather
 * than quietly turning into one.
 */
const props = defineProps<{
  open: boolean
  /** Which list the confirmed rows are written to. */
  target: 'shopping-list' | 'inventory'
  /** Required when `target` is `shopping-list`. */
  shoppingListPublicId?: string
  /** Optional for inventory: where the new stock goes. */
  storageLocationPublicId?: string
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  /** Fired after the rows have been written, with how many were created. */
  'created': [count: number]
}>()

const { t, locale } = useI18n()
const toast = useToast()
const { getProducts, createProduct, quickAddMultipleInventoryItems } = useProductsApi()
const { createMultipleShoppingListItems } = useShoppingListApi()
const { matchName } = useVoiceItemMatching()
const {
  isListening,
  transcript,
  interim,
  failure,
  start,
  stop,
  cancel,
  reset
} = useSpeechRecognition()

interface VoiceRow {
  id: string
  name: string
  /** Only used by a row that will create a product — the catalogue requires one. */
  brand: string
  quantity: number
  unit: Unit
  raw: string
  match: VoiceItemMatch
}

const rows = ref<VoiceRow[]>([])
const products = ref<ProductInfo[]>([])
const isSaving = ref(false)
const micEl = ref<HTMLButtonElement | null>(null)

const spokenText = computed(() => transcript.value || interim.value)

const unitOptions = computed(() =>
  Object.entries(Unit)
    .filter(([key]) => Number.isNaN(Number(key)))
    .map(([_key, value]) => ({ label: t(`enums.unit.${value}`), value: value as Unit }))
)

const failureTitle = computed(() => {
  if (failure.value === 'denied') return t('voice.errors.deniedTitle')
  if (failure.value === 'no-speech') return t('voice.errors.noSpeechTitle')
  if (failure.value === 'network') return t('voice.errors.networkTitle')
  return t('voice.errors.unknownTitle')
})

const failureHint = computed(() => {
  if (failure.value === 'denied') return t('voice.errors.deniedHint')
  if (failure.value === 'no-speech') return t('voice.errors.noSpeechHint')
  if (failure.value === 'network') return t('voice.errors.networkHint')
  return t('voice.errors.unknownHint')
})

/** True for a row that has to create a catalogue product before it can become stock. */
const needsProduct = (row: VoiceRow) =>
  props.target === 'inventory' && row.match.kind !== 'product'

const badgeFor = (row: VoiceRow) => {
  if (row.match.kind === 'product') {
    return { label: t('voice.badges.known'), color: 'success' as const, icon: 'i-lucide-package-check' }
  }
  if (row.match.kind === 'category') {
    return { label: t('voice.badges.category'), color: 'primary' as const, icon: 'i-lucide-tag' }
  }
  return {
    label: props.target === 'inventory' ? t('voice.badges.newProduct') : t('voice.badges.custom'),
    color: 'neutral' as const,
    icon: 'i-lucide-sparkles'
  }
}

/** The products this family already has, loaded once per opening so matching is local. */
const loadProducts = async () => {
  try {
    const response = await getProducts({ returnAll: true })
    if (response.success && response.data) products.value = response.data.items
  } catch {
    // Matching degrades to the category vocabulary and free text, which is still usable.
    products.value = []
  }
}

const buildRows = () => {
  const parsed = parseVoiceItems(transcript.value, String(locale.value))

  rows.value = parsed.map((item, index) => {
    const match = matchName(item.name, products.value)

    return {
      id: `${Date.now()}-${index}`,
      name: match.label,
      brand: match.product?.brand ?? t('voice.unknownBrand'),
      quantity: item.quantity,
      // A matched product carries its own unit, and the API takes the product's unit for a
      // product-linked row anyway — so only an unmatched name needs the spoken one.
      unit: item.unit ?? match.product?.unit ?? Unit.Piece,
      raw: item.raw,
      match
    }
  })
}

// --- The hold gesture --------------------------------------------------------------------
// Pointer capture keeps the events coming to the button once the finger leaves it, which is
// what makes "release outside to cancel" possible at all: without it the browser retargets the
// release to whatever is under the finger and the button never hears about it.

const onPointerDown = (event: PointerEvent) => {
  micEl.value?.setPointerCapture(event.pointerId)
  reset()
  rows.value = []
  start()
}

const releasedInside = (event: PointerEvent) => {
  const rect = micEl.value?.getBoundingClientRect()
  if (!rect) return true
  return event.clientX >= rect.left && event.clientX <= rect.right
    && event.clientY >= rect.top && event.clientY <= rect.bottom
}

const onPointerUp = (event: PointerEvent) => {
  micEl.value?.releasePointerCapture(event.pointerId)

  if (releasedInside(event)) stop()
  else cancel()
}

const onPointerCancel = (event: PointerEvent) => {
  micEl.value?.releasePointerCapture(event.pointerId)
  cancel()
}

// The engine finishes asynchronously: the last final result can land after `stop()` returns, so
// the rows are built when listening actually ends rather than from inside the release handler.
watch(isListening, (listening, wasListening) => {
  if (wasListening && !listening && transcript.value) buildRows()
})

watch(() => props.open, (open) => {
  if (open) {
    reset()
    rows.value = []
    loadProducts()
  } else {
    cancel()
  }
})

const clearRows = () => {
  rows.value = []
  reset()
}

const close = () => emit('update:open', false)

const onOpenChange = (value: boolean) => {
  if (!value) close()
}

// --- Writing the rows --------------------------------------------------------------------

const saveToShoppingList = async () => {
  if (!props.shoppingListPublicId) return 0

  const items: CreateShoppingListItemEntry[] = rows.value.map(row => (
    row.match.kind === 'product' && row.match.product
      ? { productPublicId: row.match.product.publicId, quantity: row.quantity }
      : { customName: row.name, quantity: row.quantity, unit: row.unit }
  ))

  const response = await createMultipleShoppingListItems({
    shoppingListPublicId: props.shoppingListPublicId,
    items
  })

  return response.success ? items.length : 0
}

const saveToInventory = async () => {
  const entries: { productPublicId: string, quantity: number }[] = []

  for (const row of rows.value) {
    if (row.match.kind === 'product' && row.match.product) {
      entries.push({ productPublicId: row.match.product.publicId, quantity: row.quantity })
      continue
    }

    // No product yet. The row was badged as new, so this is what the reader confirmed — the
    // matched category is carried over when there was one, which is the whole point of looking
    // the name up in the category vocabulary.
    const created = await createProduct({
      name: row.name,
      brand: row.brand.trim() || t('voice.unknownBrand'),
      unit: row.unit,
      category: row.match.category ?? null,
      isEatable: true
    }, { showErrorToast: false })

    if (created.success && created.data) {
      entries.push({ productPublicId: created.data.publicId, quantity: row.quantity })
    }
  }

  if (entries.length === 0) return 0

  const response = await quickAddMultipleInventoryItems({
    items: entries,
    storageLocationPublicId: props.storageLocationPublicId
  })

  return response.success ? entries.length : 0
}

const save = async () => {
  if (rows.value.length === 0) return

  isSaving.value = true
  try {
    const created = props.target === 'shopping-list'
      ? await saveToShoppingList()
      : await saveToInventory()

    if (created === 0) return

    toast.add({
      title: t('toast.success'),
      description: t('voice.added', { count: created }),
      color: 'success',
      icon: 'i-heroicons-check-circle'
    })

    emit('created', created)
    close()
  } finally {
    isSaving.value = false
  }
}
</script>
