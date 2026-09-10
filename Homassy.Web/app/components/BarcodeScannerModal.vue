<template>
  <AppDrawer
    :open="isScannerOpen"
    :title="t('barcodeScanner.title')"
    icon="i-lucide-scan-barcode"
    @update:open="handleUpdateOpen"
  >
    <div class="space-y-4">
      <!-- Mode Tabs -->
      <UTabs
        :items="scanTabs"
        :default-value="'barcode'"
        :model-value="scanMode"
        @update:model-value="handleTabChange"
      />

      <!-- Per-mode hint -->
      <p class="text-sm text-center text-muted">{{ activeDescription }}</p>

      <!-- Camera stage. The fixed aspect ratio is what lets the state panels
           (permission, no camera) fill a stable box even when there is no video
           to give the wrapper a height. -->
      <div
        ref="streamWrapperRef"
        class="relative w-full aspect-[4/3] overflow-hidden rounded-xl bg-black"
        :class="canCapture ? 'cursor-pointer' : ''"
        @click="handleCameraClick"
      >
        <QrcodeStream
          v-if="isScannerOpen"
          :key="streamKey"
          :paused="isPaused"
          :torch="torchEnabled"
          :constraints="{ facingMode }"
          :formats="activeFormats"
          :track="trackFunction"
          @detect="handleDetect"
          @camera-on="handleCameraOn"
          @error="handleCameraError"
        />

        <!-- Frozen frame from a tap-to-capture, held while it is being decoded -->
        <img
          v-if="frozenImage"
          :src="frozenImage"
          alt=""
          class="absolute inset-0 z-10 h-full w-full object-cover pointer-events-none"
        >

        <!-- Reticle: corner brackets that sweep while searching, then snap onto
             the code that was actually read and morph into a checkmark. -->
        <div
          v-if="showReticle"
          class="scan-reticle"
          :class="{ 'scan-reticle--found': scanState === 'found' }"
          :style="reticleStyle"
        >
          <span class="scan-reticle__corner scan-reticle__corner--tl" />
          <span class="scan-reticle__corner scan-reticle__corner--tr" />
          <span class="scan-reticle__corner scan-reticle__corner--bl" />
          <span class="scan-reticle__corner scan-reticle__corner--br" />

          <div v-if="scanState === 'searching' && !frozenImage" class="scan-reticle__beam" />

          <div v-if="scanState === 'found'" class="scan-reticle__check">
            <span class="scan-reticle__check-badge">
              <UIcon name="i-lucide-check" class="h-5 w-5" />
            </span>
          </div>
        </div>

        <!-- Capture / success flash -->
        <div
          v-if="isFlashing"
          class="absolute inset-0 z-20 pointer-events-none bg-white capture-flash"
        />

        <!-- Torch + camera switch, only where the hardware supports them -->
        <div
          v-if="showCameraControls"
          class="absolute right-2 top-2 z-30 flex flex-col gap-2"
        >
          <UButton
            v-if="torchSupported"
            :icon="torchEnabled ? 'i-lucide-flashlight-off' : 'i-lucide-flashlight'"
            color="neutral"
            variant="solid"
            size="sm"
            :aria-label="torchEnabled ? t('barcodeScanner.torchOff') : t('barcodeScanner.torchOn')"
            @click.stop="toggleTorch"
          />
          <UButton
            v-if="hasMultipleCameras"
            icon="i-lucide-switch-camera"
            color="neutral"
            variant="solid"
            size="sm"
            :aria-label="t('barcodeScanner.switchCamera')"
            @click.stop="switchCamera"
          />
        </div>

        <!-- Multi-detection candidate selection overlay -->
        <div
          v-if="detectedCandidates.length > 1"
          class="absolute inset-0 z-40 flex flex-col items-center justify-center gap-2 overflow-y-auto bg-black/70 p-4"
        >
          <p class="text-sm font-semibold text-white text-center">
            {{ t('barcodeScanner.multipleDetected') }}
          </p>
          <UButton
            v-for="candidate in detectedCandidates"
            :key="candidate"
            color="primary"
            variant="solid"
            class="w-full truncate"
            @click.stop="selectCandidate(candidate)"
          >
            {{ candidate }}
          </UButton>
          <UButton
            color="neutral"
            variant="outline"
            class="w-full mt-1"
            @click.stop="handleDismissCandidates"
          >
            {{ t('barcodeScanner.retry') }}
          </UButton>
        </div>

        <!-- Camera states that are not "there is a picture to look at" -->
        <div
          v-else-if="statePanel"
          class="absolute inset-0 z-40 flex flex-col items-center justify-center gap-3 bg-black/80 px-6 text-center"
        >
          <UIcon
            :name="statePanel.icon"
            class="h-9 w-9"
            :class="[statePanel.tone, statePanel.spin ? 'animate-spin' : '']"
          />
          <p class="text-sm font-semibold text-white">{{ statePanel.title }}</p>
          <p v-if="statePanel.hint" class="text-xs text-white/70">{{ statePanel.hint }}</p>
          <UButton
            v-if="statePanel.retry"
            color="neutral"
            variant="solid"
            size="sm"
            icon="i-lucide-rotate-ccw"
            :label="t('barcodeScanner.retry')"
            @click.stop="retryCamera"
          />
        </div>
      </div>

      <!-- Screen-reader running commentary on the states above -->
      <p class="sr-only" role="status" aria-live="polite">{{ stateAnnouncement }}</p>

      <!-- Zoom controls -->
      <div
        v-if="zoomSupported"
        class="flex items-center justify-center gap-3"
      >
        <UButton
          icon="i-lucide-minus"
          color="neutral"
          variant="outline"
          size="sm"
          :disabled="zoomLevel <= zoomMin"
          :aria-label="t('barcodeScanner.zoomOut')"
          @click="setZoom(zoomLevel - zoomStep)"
        />
        <span class="text-sm font-medium w-10 text-center tabular-nums">{{ zoomDisplay }}</span>
        <UButton
          icon="i-lucide-plus"
          color="neutral"
          variant="outline"
          size="sm"
          :disabled="zoomLevel >= zoomMax"
          :aria-label="t('barcodeScanner.zoomIn')"
          @click="setZoom(zoomLevel + zoomStep)"
        />
      </div>

      <!-- Instructions -->
      <p class="text-sm text-center text-gray-600 dark:text-gray-400">
        {{ t('barcodeScanner.instructions') }}
      </p>

      <!-- Error Message -->
      <div
        v-if="scanError"
        class="p-3 bg-red-50 dark:bg-red-900/20 rounded-lg"
      >
        <p class="text-sm text-red-600 dark:text-red-400">
          {{ scanError }}
        </p>
      </div>
    </div>

    <template #footer>
      <UButton
        class="ml-auto"
        :label="t('common.cancel')"
        color="neutral"
        variant="ghost"
        icon="i-lucide-x"
        @click="handleUpdateOpen(false)"
      />
    </template>
  </AppDrawer>
</template>

<script setup lang="ts">
import { watch, nextTick, onBeforeUnmount, ref, computed, type CSSProperties } from 'vue'
import { QrcodeStream } from 'vue-qrcode-reader'
import { useBarcodeScanner } from '../composables/useBarcodeScanner'

interface Props {
  onBarcodeDetected: (barcode: string) => void
}

const props = defineProps<Props>()

const { t } = useI18n()
const {
  isScannerOpen,
  isPaused,
  scanError,
  startScanner,
  closeScanner,
  captureAndScan,
  handleDetect,
  handleCameraError,
  detectedCandidates,
  selectCandidate,
  dismissCandidates,
  torchEnabled,
  torchSupported,
  initTorchAndZoom,
  toggleTorch,
  zoomLevel,
  zoomMin,
  zoomMax,
  zoomStep,
  setZoom,
  scanState,
  successValue,
  markCameraReady,
  retryCamera,
  switchCamera,
  facingMode,
  streamNonce
} = useBarcodeScanner()
const { hasMultipleCameras } = useCameraAvailability()

const isFlashing = ref(false)
const frozenImage = ref<string | null>(null)
const videoRef = ref<HTMLVideoElement | null>(null)
const streamWrapperRef = ref<HTMLDivElement | null>(null)
const scanMode = ref<'barcode' | 'qrcode'>('barcode')

const scanTabs = computed(() => [
  { value: 'barcode', label: t('barcodeScanner.tabs.barcode') },
  { value: 'qrcode', label: t('barcodeScanner.tabs.qrcode') }
])

// The array literal has to keep its narrow element type: vue-qrcode-reader types `formats` as
// its own union of format names, and an inferred `string[]` does not fit it.
const activeFormats = computed<('linear_codes' | 'matrix_codes')[]>(() =>
  scanMode.value === 'barcode' ? ['linear_codes'] : ['matrix_codes']
)

const activeDescription = computed(() =>
  scanMode.value === 'barcode'
    ? t('barcodeScanner.description')
    : t('barcodeScanner.descriptionQr')
)

// Constraints are only read when the stream starts, so a camera switch (or a
// retry) has to remount the component rather than merely update a prop.
const streamKey = computed(() => `${scanMode.value}-${facingMode.value}-${streamNonce.value}`)

const zoomSupported = computed(() => zoomMin.value < zoomMax.value)

const zoomDisplay = computed(() => {
  const v = zoomLevel.value
  return Number.isInteger(v) ? `${v}x` : `${v.toFixed(1)}x`
})

const showReticle = computed(() =>
  (scanState.value === 'searching' || scanState.value === 'found') && detectedCandidates.value.length <= 1
)

const showCameraControls = computed(() =>
  scanState.value === 'searching' && detectedCandidates.value.length <= 1
)

const canCapture = computed(() => scanState.value === 'searching' && !isPaused.value)

/** The full-bleed panel for every state that has nothing to aim at. */
const statePanel = computed(() => {
  switch (scanState.value) {
    case 'requesting':
      return {
        icon: 'i-lucide-loader-2',
        spin: true,
        tone: 'text-white',
        title: t('barcodeScanner.states.requesting'),
        hint: t('barcodeScanner.states.requestingHint'),
        retry: false
      }
    case 'denied':
      return {
        icon: 'i-lucide-camera-off',
        spin: false,
        tone: 'text-red-400',
        title: t('barcodeScanner.errors.permissionDenied'),
        hint: t('barcodeScanner.errors.permissionDeniedHint'),
        retry: true
      }
    case 'no-camera':
      return {
        icon: 'i-lucide-video-off',
        spin: false,
        tone: 'text-white/70',
        title: t('barcodeScanner.errors.noCameraFound'),
        hint: '',
        retry: false
      }
    case 'error':
      return {
        icon: 'i-lucide-alert-triangle',
        spin: false,
        tone: 'text-amber-400',
        title: scanError.value || t('barcodeScanner.errors.scanFailed'),
        hint: '',
        retry: true
      }
    default:
      return null
  }
})

const stateAnnouncement = computed(() => {
  if (statePanel.value) return statePanel.value.title
  if (scanState.value === 'found') return t('barcodeScanner.states.found')
  return t('barcodeScanner.states.searching')
})

// ---------------------------------------------------------------------------
// Reticle geometry
//
// vue-qrcode-reader hands the `track` callback coordinates already mapped into
// the element's own pixel space (it sizes the tracking canvas to the wrapper's
// offset size and compensates for `object-fit: cover`), so a box recorded there
// can be used verbatim to position a DOM overlay.
// ---------------------------------------------------------------------------

interface Box { x: number, y: number, width: number, height: number }

const codeBoxes = new Map<string, Box>()
const foundBox = ref<Box | null>(null)
const stageSize = ref({ width: 0, height: 0 })
let resizeObserver: ResizeObserver | null = null

const measureStage = () => {
  const el = streamWrapperRef.value
  if (!el) return
  stageSize.value = { width: el.clientWidth, height: el.clientHeight }
}

watch(streamWrapperRef, (el) => {
  resizeObserver?.disconnect()
  resizeObserver = null
  if (!el || typeof ResizeObserver === 'undefined') return
  resizeObserver = new ResizeObserver(measureStage)
  resizeObserver.observe(el)
  measureStage()
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  resizeObserver = null
})

const RETICLE_PADDING = 10

const reticleStyle = computed<CSSProperties>(() => {
  const box = foundBox.value
  if (box) {
    return {
      left: `${box.x - RETICLE_PADDING}px`,
      top: `${box.y - RETICLE_PADDING}px`,
      width: `${box.width + RETICLE_PADDING * 2}px`,
      height: `${box.height + RETICLE_PADDING * 2}px`
    }
  }
  // Resting frame: a wide, shallow window — the shape of a product barcode.
  const { width, height } = stageSize.value
  const w = width * 0.8
  const h = Math.min(height * 0.46, w * 0.6)
  return {
    left: `${(width - w) / 2}px`,
    top: `${(height - h) / 2}px`,
    width: `${w}px`,
    height: `${h}px`
  }
})

// A decode freezes the stream, so the last frame the tracker drew is still on
// screen — which is exactly the frame the winning box came from.
watch(successValue, (value) => {
  if (!value) {
    foundBox.value = null
    return
  }
  foundBox.value = codeBoxes.get(value) ?? null
  flash()
})

const flash = () => {
  isFlashing.value = false
  // Restart the CSS animation rather than letting a second flash be swallowed.
  requestAnimationFrame(() => {
    isFlashing.value = true
    setTimeout(() => {
      isFlashing.value = false
    }, 300)
  })
}

// ---------------------------------------------------------------------------
// Live tracking overlay
// ---------------------------------------------------------------------------

// Canvas cannot resolve `var(--ui-primary)`, so the tokens are read off the DOM
// once the stage exists and reused for every frame.
let palette = { primary: '#b9a386', muted: 'rgba(255,255,255,0.55)' }

const readPalette = () => {
  const el = streamWrapperRef.value
  if (!el) return
  const styles = getComputedStyle(el)
  palette = {
    primary: styles.getPropertyValue('--ui-primary').trim() || palette.primary,
    muted: 'rgba(255, 255, 255, 0.55)'
  }
}

const drawOutline = (ctx: CanvasRenderingContext2D, code: DetectedBarcode, primary: boolean) => {
  const { cornerPoints, boundingBox } = code

  ctx.strokeStyle = primary ? palette.primary : palette.muted
  ctx.lineWidth = primary ? 3 : 1.5
  ctx.lineJoin = 'round'

  if (cornerPoints && cornerPoints.length >= 3) {
    ctx.beginPath()
    cornerPoints.forEach((point, index) => {
      if (index === 0) ctx.moveTo(point.x, point.y)
      else ctx.lineTo(point.x, point.y)
    })
    ctx.closePath()
    ctx.stroke()
  } else if (boundingBox) {
    ctx.strokeRect(boundingBox.x, boundingBox.y, boundingBox.width, boundingBox.height)
  }
}

const drawLabel = (ctx: CanvasRenderingContext2D, code: DetectedBarcode, primary: boolean) => {
  const { boundingBox, rawValue } = code
  if (!boundingBox || !rawValue) return

  const text = rawValue.length > 24 ? `${rawValue.slice(0, 23)}…` : rawValue
  ctx.font = '600 13px system-ui, sans-serif'
  const padding = 5
  const textWidth = ctx.measureText(text).width
  const boxWidth = textWidth + padding * 2
  const boxHeight = 20
  const x = Math.max(2, Math.min(boundingBox.x, ctx.canvas.width - boxWidth - 2))
  // Above the code, unless it is already at the top edge.
  const y = boundingBox.y > boxHeight + 6 ? boundingBox.y - boxHeight - 4 : boundingBox.y + boundingBox.height + 4

  ctx.fillStyle = primary ? palette.primary : 'rgba(0, 0, 0, 0.6)'
  if (typeof ctx.roundRect === 'function') {
    ctx.beginPath()
    ctx.roundRect(x, y, boxWidth, boxHeight, 5)
    ctx.fill()
  } else {
    // roundRect is recent; square corners are a fine degradation.
    ctx.fillRect(x, y, boxWidth, boxHeight)
  }

  ctx.fillStyle = '#ffffff'
  ctx.textBaseline = 'middle'
  ctx.fillText(text, x + padding, y + boxHeight / 2)
}

/**
 * Draw every code the detector sees, labelled with its decoded value. With
 * several in frame the largest — the one the user is most likely aiming at, and
 * the one the stability buffer will almost always settle on — is drawn in the
 * primary colour and the rest are dimmed, so it is obvious which one is about to
 * be taken.
 */
const trackFunction = (detectedCodes: DetectedBarcode[], ctx: CanvasRenderingContext2D) => {
  codeBoxes.clear()
  if (detectedCodes.length === 0) return

  const area = (code: DetectedBarcode) => (code.boundingBox?.width ?? 0) * (code.boundingBox?.height ?? 0)
  const leader = detectedCodes.reduce((best, code) => (area(code) > area(best) ? code : best), detectedCodes[0]!)

  for (const code of detectedCodes) {
    if (code.boundingBox) {
      codeBoxes.set(code.rawValue, {
        x: code.boundingBox.x,
        y: code.boundingBox.y,
        width: code.boundingBox.width,
        height: code.boundingBox.height
      })
    }
    const primary = detectedCodes.length === 1 || code === leader
    drawOutline(ctx, code, primary)
    drawLabel(ctx, code, primary)
  }
}

const handleTabChange = (value: string | number) => {
  if (value === scanMode.value) return
  scanMode.value = value as 'barcode' | 'qrcode'
  // Clear any frozen state when switching tabs
  frozenImage.value = null
  dismissCandidates()
  // Reset zoom when tab changes (camera remounts)
  zoomLevel.value = 1
}

// Watch for the scanner sheet opening and start the scanner
watch(isScannerOpen, async (isOpen: boolean) => {
  if (isOpen) {
    // Clear frozen image when opening scanner
    frozenImage.value = null
    foundBox.value = null
    await nextTick()
    measureStage()
    startScanner(props.onBarcodeDetected)
  }
})

const handleUpdateOpen = async (value: boolean) => {
  if (!value) {
    // Sheet is being closed
    await closeScanner()
    frozenImage.value = null
    foundBox.value = null
    codeBoxes.clear()
    videoRef.value = null
    scanMode.value = 'barcode'
    zoomLevel.value = 1
  }
}

const handleCameraOn = async (capabilities: MediaTrackCapabilities) => {
  markCameraReady()
  measureStage()
  readPalette()
  await initTorchAndZoom(capabilities, streamWrapperRef.value)
}

const handleDismissCandidates = () => {
  frozenImage.value = null
  dismissCandidates()
}

const handleCameraClick = () => {
  if (!canCapture.value) return

  flash()

  // Get video element from within the QrcodeStream wrapper (v5 renders a plain <video> tag)
  const video = streamWrapperRef.value?.querySelector('video') as HTMLVideoElement | null
  if (!video) return

  videoRef.value = video

  captureAndScan(
    videoRef,
    (imageUrl) => {
      // On freeze - show the captured image
      frozenImage.value = imageUrl
    },
    () => {
      // On unfreeze - resume live video
      frozenImage.value = null
    }
  )
}
</script>

<style scoped>
/* Corner brackets rather than a full rectangle: they read as an aiming frame
   without covering the code, and they are what snaps onto the decoded box. */
.scan-reticle {
  position: absolute;
  z-index: 20;
  pointer-events: none;
  transition:
    left 380ms cubic-bezier(0.34, 1.1, 0.64, 1),
    top 380ms cubic-bezier(0.34, 1.1, 0.64, 1),
    width 380ms cubic-bezier(0.34, 1.1, 0.64, 1),
    height 380ms cubic-bezier(0.34, 1.1, 0.64, 1);
}

.scan-reticle__corner {
  position: absolute;
  width: 22px;
  height: 22px;
  border: 3px solid var(--ui-primary);
  transition: border-color 200ms ease;
}

.scan-reticle__corner--tl {
  top: 0;
  left: 0;
  border-right: 0;
  border-bottom: 0;
  border-top-left-radius: 12px;
}

.scan-reticle__corner--tr {
  top: 0;
  right: 0;
  border-left: 0;
  border-bottom: 0;
  border-top-right-radius: 12px;
}

.scan-reticle__corner--bl {
  bottom: 0;
  left: 0;
  border-right: 0;
  border-top: 0;
  border-bottom-left-radius: 12px;
}

.scan-reticle__corner--br {
  bottom: 0;
  right: 0;
  border-left: 0;
  border-top: 0;
  border-bottom-right-radius: 12px;
}

.scan-reticle--found .scan-reticle__corner {
  border-color: var(--ui-success);
}

.scan-reticle__beam {
  position: absolute;
  left: 6%;
  right: 6%;
  height: 2px;
  border-radius: 999px;
  background: linear-gradient(90deg, transparent, var(--ui-primary), transparent);
  animation: scan-beam 2.1s cubic-bezier(0.45, 0, 0.55, 1) infinite;
}

.scan-reticle__check {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
}

.scan-reticle__check-badge {
  display: grid;
  place-items: center;
  padding: 0.35rem;
  border-radius: 999px;
  background: var(--ui-success);
  color: #fff;
  animation: scan-check-pop 320ms cubic-bezier(0.34, 1.56, 0.64, 1) both;
}

@keyframes scan-beam {
  0% {
    top: 4%;
    opacity: 0;
  }
  12% {
    opacity: 1;
  }
  88% {
    opacity: 1;
  }
  100% {
    top: 96%;
    opacity: 0;
  }
}

@keyframes scan-check-pop {
  from {
    opacity: 0;
    transform: scale(0.4);
  }
}

/* Camera capture flash animation */
.capture-flash {
  animation: flash 0.3s ease-out;
}

@keyframes flash {
  0% {
    opacity: 0;
  }
  50% {
    opacity: 0.8;
  }
  100% {
    opacity: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .scan-reticle {
    transition: none;
  }

  /* No sweep — the frame just sits there; a static line still marks the aim. */
  .scan-reticle__beam {
    top: 50%;
    animation: none;
  }

  .scan-reticle__check-badge {
    animation: none;
  }

  .capture-flash {
    animation: none;
    opacity: 0;
  }
}
</style>
