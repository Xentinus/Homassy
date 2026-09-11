<template>
  <div v-if="hasAddress">
    <USkeleton v-if="isLoading" class="w-full h-48 rounded-xl" />
    <InteractiveMap
      v-else-if="coords"
      :markers="markers"
      :user-position="userPosition"
      height="12rem"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { MapMarker } from './InteractiveMap.vue'

/**
 * Shows one location on a map.
 *
 * Prefers stored coordinates (`latitude`/`longitude`) when provided and skips any network call;
 * otherwise falls back to geocoding the text address at runtime via Nominatim (free, keyless) — for
 * older locations saved before coordinates were stored. Renders nothing when there is neither
 * coordinates nor a resolvable address (the caller keeps the address text and any "open in maps"
 * link), which is the behaviour this component has always had and the one thing that must not
 * change: several screens rely on it to decide whether a map section appears at all.
 *
 * What did change (#107) is what is underneath: an `InteractiveMap` layer instead of OpenStreetMap's
 * `export/embed.html` iframe, which could only ever draw one unstyleable, unclickable pin.
 */
const props = defineProps<{
  address?: string
  city?: string
  postalCode?: string
  country?: string
  latitude?: number
  longitude?: number
  /** Name shown on the marker; falls back to a generic label. */
  name?: string
  /** Draws the pulse ring — this is the shop the user is currently standing at. */
  highlighted?: boolean
  /** The device's own position, when the caller is tracking it. */
  userPosition?: { lat: number, lon: number } | null
}>()

const { t } = useI18n()
const { geocode: geocodeAddress, buildAddressQuery } = useGeocoding()

const query = computed(() =>
  buildAddressQuery([props.address, props.postalCode, props.city, props.country])
)
const hasStoredCoords = computed(() =>
  typeof props.latitude === 'number' && typeof props.longitude === 'number'
)
const hasAddress = computed(() => query.value.length > 0 || hasStoredCoords.value)

const geocodedCoords = ref<{ lat: number, lon: number } | null>(null)
const isLoading = ref(false)

const coords = computed<{ lat: number, lon: number } | null>(() =>
  hasStoredCoords.value
    ? { lat: props.latitude as number, lon: props.longitude as number }
    : geocodedCoords.value
)

const markers = computed<MapMarker[]>(() => {
  if (!coords.value) return []
  return [{
    id: 'location',
    lat: coords.value.lat,
    lon: coords.value.lon,
    label: props.name || t('profile.shoppingLocations.mapTitle'),
    highlighted: props.highlighted
  }]
})

async function resolveCoords() {
  geocodedCoords.value = null

  // Stored coordinates win — no network call needed.
  if (hasStoredCoords.value) return
  if (!import.meta.client || query.value.length === 0) return

  const current = query.value
  isLoading.value = true
  try {
    const result = await geocodeAddress(current)
    // Ignore a stale response if the address changed while this request was in flight.
    if (current !== query.value) return
    geocodedCoords.value = result
  } finally {
    if (current === query.value) isLoading.value = false
  }
}

watch(
  () => [query.value, hasStoredCoords.value] as const,
  resolveCoords,
  { immediate: true }
)
</script>
