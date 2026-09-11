<template>
  <div class="relative w-full overflow-hidden rounded-xl border border-default" :style="{ height }">
    <div ref="mapEl" class="h-full w-full" />

    <!-- The map canvas is created only after MapLibre has been fetched, so something has to hold
         the space until then; the skeleton is the same one every other lazy panel uses. -->
    <USkeleton v-if="!isReady" class="absolute inset-0 h-full w-full" />

    <p
      v-if="failed"
      class="absolute inset-0 flex items-center justify-center bg-elevated px-4 text-center text-xs text-muted"
    >
      {{ $t('map.unavailable') }}
    </p>
  </div>
</template>

<script setup lang="ts">
/**
 * An interactive map layer (#107), replacing the OpenStreetMap `export/embed.html` iframe.
 *
 * The iframe could show exactly one static pin and nothing inside it could be styled, animated or
 * clicked — so a multi-shop overview, clustering, or highlighting the shop you are standing next to
 * were all impossible, even though the shopping list already knows the user's position.
 *
 * Three deliberate choices:
 *
 * **Lazily loaded.** MapLibre is a few hundred kilobytes; it is imported inside `onMounted`, so a
 * session that never opens a map never pays for it. That is also why this is a component rather than
 * a plugin — nothing loads until one is rendered.
 *
 * **Keyless tiles.** CARTO's OpenStreetMap-derived raster basemaps, which need no account and offer
 * a light and a dark variant, so the map follows the app's theme instead of being the one white
 * rectangle in a dark screen. Attribution is required and is rendered by MapLibre's own control.
 *
 * **DOM markers, and clustering done here.** Every marker is a real element, so it is styled with
 * the app's own theme tokens and the "you are here" ring is an ordinary CSS animation. Clustering is
 * grid-based in screen space rather than MapLibre's built-in GeoJSON clustering, because that draws
 * its cluster counts as map text and would drag in a glyph server — a second keyless third party for
 * the sake of drawing a number.
 */
import type { Map as MapLibreMap, Marker as MapLibreMarker, StyleSpecification } from 'maplibre-gl'

export interface MapMarker {
  id: string
  lat: number
  lon: number
  /** Shown in the marker's tooltip and read by screen readers. */
  label: string
  /** Overrides the marker's fill, for a location that carries its own colour. */
  color?: string
  /** Draws the pulse ring — the shop the user is standing at, or otherwise the relevant one. */
  highlighted?: boolean
}

const props = withDefaults(defineProps<{
  markers: MapMarker[]
  /** The device's own position, when geolocation is on. Drawn as a distinct pulsing dot. */
  userPosition?: { lat: number, lon: number } | null
  /** CSS height of the map box. */
  height?: string
  /** Group nearby markers into a count bubble. Off for a single-location map. */
  cluster?: boolean
  /** Zoom used when there is only one point to show (a fit-to-bounds of one point is meaningless). */
  singleZoom?: number
}>(), {
  userPosition: null,
  height: '12rem',
  cluster: false,
  singleZoom: 15
})

const emit = defineEmits<{
  /** A single (non-cluster) marker was tapped. */
  'marker-click': [id: string]
}>()

const { t } = useI18n()
const colorMode = useColorMode()

const mapEl = ref<HTMLElement | null>(null)
const isReady = ref(false)
const failed = ref(false)

/** Screen-space radius that decides whether two markers are drawn as one bubble. */
const CLUSTER_RADIUS_PX = 46
/** Padding used when fitting the view to every marker. */
const FIT_PADDING = 48

let maplibre: typeof import('maplibre-gl') | null = null
let map: MapLibreMap | null = null
let markerInstances: MapLibreMarker[] = []
let userMarker: MapLibreMarker | null = null

/**
 * A raster style built inline rather than fetched: there is no style JSON to host, no extra request
 * before the first tile, and swapping light for dark is a one-property change.
 */
const buildStyle = (dark: boolean): StyleSpecification => {
  const variant = dark ? 'dark_all' : 'light_all'
  return {
    version: 8,
    sources: {
      basemap: {
        type: 'raster',
        tiles: ['a', 'b', 'c'].map(sub => `https://${sub}.basemaps.cartocdn.com/${variant}/{z}/{x}/{y}.png`),
        tileSize: 256,
        attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors, © <a href="https://carto.com/attributions">CARTO</a>'
      }
    },
    layers: [{ id: 'basemap', type: 'raster', source: 'basemap' }]
  }
}

// --- Markers ---------------------------------------------------------------

const clearMarkers = () => {
  for (const marker of markerInstances) marker.remove()
  markerInstances = []
}

const createPinElement = (marker: MapMarker): HTMLElement => {
  const element = document.createElement('button')
  element.type = 'button'
  element.className = `map-pin${marker.highlighted ? ' map-pin--active' : ''}`
  element.title = marker.label
  element.setAttribute('aria-label', marker.label)
  if (marker.color) element.style.setProperty('--map-pin-color', marker.color)
  element.addEventListener('click', (event) => {
    event.stopPropagation()
    emit('marker-click', marker.id)
  })
  return element
}

const createClusterElement = (count: number, onClick: () => void): HTMLElement => {
  const element = document.createElement('button')
  element.type = 'button'
  element.className = 'map-cluster'
  element.textContent = String(count)
  element.setAttribute('aria-label', t('map.cluster', { count }))
  element.addEventListener('click', (event) => {
    event.stopPropagation()
    onClick()
  })
  return element
}

/**
 * Groups markers that land within `CLUSTER_RADIUS_PX` of one another at the current zoom, greedily:
 * each ungrouped marker starts a bubble and absorbs everything still free within the radius. A
 * highlighted marker never joins a bubble — the whole reason it is highlighted is to be visible.
 */
const buildGroups = (): { markers: MapMarker[] }[] => {
  if (!map) return []
  if (!props.cluster) return props.markers.map(marker => ({ markers: [marker] }))

  const points = props.markers.map(marker => ({
    marker,
    point: map!.project([marker.lon, marker.lat]),
    taken: false
  }))

  const groups: { markers: MapMarker[] }[] = []
  for (const candidate of points) {
    if (candidate.taken) continue
    candidate.taken = true

    if (candidate.marker.highlighted) {
      groups.push({ markers: [candidate.marker] })
      continue
    }

    const group = [candidate.marker]
    for (const other of points) {
      if (other.taken || other.marker.highlighted) continue
      const dx = other.point.x - candidate.point.x
      const dy = other.point.y - candidate.point.y
      if (dx * dx + dy * dy <= CLUSTER_RADIUS_PX * CLUSTER_RADIUS_PX) {
        other.taken = true
        group.push(other.marker)
      }
    }
    groups.push({ markers: group })
  }

  return groups
}

const renderMarkers = () => {
  if (!map || !maplibre) return
  clearMarkers()

  for (const group of buildGroups()) {
    const first = group.markers[0]
    if (!first) continue

    if (group.markers.length === 1) {
      markerInstances.push(
        new maplibre.Marker({ element: createPinElement(first), anchor: 'bottom' })
          .setLngLat([first.lon, first.lat])
          .addTo(map)
      )
      continue
    }

    // The bubble sits at the group's centroid, and tapping it zooms to the points inside — the one
    // affordance that makes a cluster a navigation aid rather than an obstacle.
    const lon = group.markers.reduce((sum, m) => sum + m.lon, 0) / group.markers.length
    const lat = group.markers.reduce((sum, m) => sum + m.lat, 0) / group.markers.length
    const members = group.markers
    const element = createClusterElement(members.length, () => zoomToMarkers(members))

    markerInstances.push(
      new maplibre.Marker({ element, anchor: 'center' })
        .setLngLat([lon, lat])
        .addTo(map)
    )
  }
}

const renderUserPosition = () => {
  if (!map || !maplibre) return

  if (!props.userPosition) {
    userMarker?.remove()
    userMarker = null
    return
  }

  const { lat, lon } = props.userPosition
  if (userMarker) {
    userMarker.setLngLat([lon, lat])
    return
  }

  const element = document.createElement('div')
  element.className = 'map-me'
  element.setAttribute('aria-label', t('map.youAreHere'))
  userMarker = new maplibre.Marker({ element, anchor: 'center' })
    .setLngLat([lon, lat])
    .addTo(map)
}

// --- Camera ----------------------------------------------------------------

const zoomToMarkers = (markers: MapMarker[]) => {
  if (!map || !maplibre || markers.length === 0) return

  const first = markers[0]!
  if (markers.length === 1) {
    map.easeTo({ center: [first.lon, first.lat], zoom: Math.max(map.getZoom() + 2, props.singleZoom) })
    return
  }

  const bounds = new maplibre.LngLatBounds([first.lon, first.lat], [first.lon, first.lat])
  for (const marker of markers) bounds.extend([marker.lon, marker.lat])
  map.fitBounds(bounds, { padding: FIT_PADDING, maxZoom: 17 })
}

/** Frames whatever there is to show: one point at a readable zoom, several as a fitted box. */
const fitToContent = (animate: boolean) => {
  if (!map || !maplibre || props.markers.length === 0) return

  const first = props.markers[0]!
  if (props.markers.length === 1) {
    map.jumpTo({ center: [first.lon, first.lat], zoom: props.singleZoom })
    return
  }

  const bounds = new maplibre.LngLatBounds([first.lon, first.lat], [first.lon, first.lat])
  for (const marker of props.markers) bounds.extend([marker.lon, marker.lat])
  map.fitBounds(bounds, { padding: FIT_PADDING, maxZoom: 16, animate })
}

// --- Lifecycle -------------------------------------------------------------

onMounted(async () => {
  if (!mapEl.value) return

  try {
    // Both the library and its stylesheet are fetched here, never at module scope: this is the
    // whole of the "loaded lazily and only on views that show a map" requirement.
    maplibre = await import('maplibre-gl')
    await import('maplibre-gl/dist/maplibre-gl.css')

    map = new maplibre.Map({
      container: mapEl.value,
      style: buildStyle(colorMode.value === 'dark'),
      center: [props.markers[0]?.lon ?? 0, props.markers[0]?.lat ?? 0],
      zoom: props.singleZoom,
      attributionControl: { compact: true },
      // Nothing on these maps is oriented, and a stray two-finger twist on a phone is far more
      // often an accident than an intention.
      pitchWithRotate: false,
      dragRotate: false
    })

    map.touchZoomRotate.disableRotation()
    map.addControl(new maplibre.NavigationControl({ showCompass: false }), 'top-right')

    map.on('load', () => {
      isReady.value = true
      fitToContent(false)
      renderMarkers()
      renderUserPosition()
    })

    // Clusters are a function of zoom, so they are rebuilt when the camera settles — not on every
    // frame of a pan, where MapLibre already keeps each marker pinned to its coordinates.
    map.on('moveend', renderMarkers)
    map.on('zoomend', renderMarkers)
  } catch (error) {
    // A blocked CDN, an offline device, or a browser without WebGL. The caller still shows the
    // address and its "open in maps" link, so the screen keeps working without this.
    console.error('Failed to load the map:', error)
    failed.value = true
  }
})

watch(() => props.markers, () => {
  if (!map) return
  renderMarkers()
  fitToContent(true)
}, { deep: true })

watch(() => props.userPosition, renderUserPosition, { deep: true })

// Following the app's theme is the reason for building the style inline: swapping the basemap is a
// style reload, after which the markers (DOM, not style layers) have to be put back.
watch(() => colorMode.value, (mode) => {
  if (!map) return
  map.setStyle(buildStyle(mode === 'dark'))
  map.once('styledata', () => {
    renderMarkers()
    renderUserPosition()
  })
})

onBeforeUnmount(() => {
  clearMarkers()
  userMarker?.remove()
  userMarker = null
  map?.remove()
  map = null
})
</script>

<style>
/* Unscoped: these elements are created imperatively and handed to MapLibre, so they are outside the
   component's own scope attribute. Prefixed class names keep them out of everything else's way. */
.map-pin {
  --map-pin-color: var(--ui-primary);
  position: relative;
  width: 1.5rem;
  height: 1.5rem;
  border-radius: 9999px 9999px 9999px 2px;
  transform: rotate(45deg);
  background: var(--map-pin-color);
  border: 2px solid var(--ui-bg);
  box-shadow: 0 2px 6px rgb(0 0 0 / 0.3);
  cursor: pointer;
}

.map-pin:focus-visible {
  outline: 2px solid var(--ui-primary);
  outline-offset: 2px;
}

/* The pulse ring: the shop you are standing next to, which the iframe could never show. */
.map-pin--active::after {
  content: '';
  position: absolute;
  inset: -0.5rem;
  border-radius: 9999px;
  border: 2px solid var(--map-pin-color);
  animation: map-pulse 1.8s ease-out infinite;
}

.map-cluster {
  min-width: 2.25rem;
  height: 2.25rem;
  padding: 0 0.4rem;
  border-radius: 9999px;
  background: var(--ui-primary);
  color: var(--ui-bg);
  border: 2px solid var(--ui-bg);
  font-size: 0.8rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  box-shadow: 0 2px 8px rgb(0 0 0 / 0.3);
  cursor: pointer;
}

.map-me {
  position: relative;
  width: 0.9rem;
  height: 0.9rem;
  border-radius: 9999px;
  background: #2563eb;
  border: 2px solid #fff;
  box-shadow: 0 1px 4px rgb(0 0 0 / 0.4);
}

.map-me::after {
  content: '';
  position: absolute;
  inset: -0.65rem;
  border-radius: 9999px;
  border: 2px solid #2563eb;
  animation: map-pulse 2s ease-out infinite;
}

@keyframes map-pulse {
  0% { transform: scale(0.7); opacity: 0.9; }
  100% { transform: scale(1.5); opacity: 0; }
}

@media (prefers-reduced-motion: reduce) {
  .map-pin--active::after,
  .map-me::after {
    animation: none;
    opacity: 0.5;
  }
}
</style>
