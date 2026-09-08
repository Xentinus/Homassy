import { computed, ref, onUnmounted, nextTick, type Ref } from 'vue'

// Global state shared across all instances
const isScannerOpen = ref(false)
const isScanning = ref(false)
const isPaused = ref(false)
const scanError = ref<string | null>(null)
const detectedBarcode = ref<string | null>(null)
const scanCallback = ref<((barcode: string) => void) | null>(null)
const detectedCandidates = ref<string[]>([])

/**
 * What the camera itself is doing, as opposed to what the scan loop is doing.
 * The overlay draws a different screen for each: a spinner while the browser is
 * still deciding, the fix-it hint when permission was refused, and so on.
 */
export type CameraState = 'idle' | 'requesting' | 'ready' | 'denied' | 'missing' | 'failed'
const cameraState = ref<CameraState>('idle')

// A decoded value is held on screen briefly before the sheet closes, so the
// reticle can snap onto the code and confirm what was read. Short enough not to
// feel like latency, long enough to register.
const SUCCESS_HOLD_MS = 450
const successValue = ref<string | null>(null)
let successTimer: ReturnType<typeof setTimeout> | null = null

// Which camera to open, and a nonce the modal keys the stream on so a retry or a
// camera switch remounts it (constraints are only read when the stream starts).
const facingMode = ref<'environment' | 'user'>('environment')
const streamNonce = ref(0)

// Buffer for stable detection: tracks how many frames each barcode appeared in
let bufferTimer: ReturnType<typeof setTimeout> | null = null
const candidateFrameCount = new Map<string, number>() // barcode → frames seen
let totalFrameCount = 0
const BUFFER_MS = 1000          // accumulation window (ms)
const STABILITY_THRESHOLD = 0.5 // barcode must appear in ≥50% of frames

// Torch / flashlight state
const torchEnabled = ref(false)
const torchSupported = ref(false)
let videoTrack: MediaStreamTrack | null = null

// Zoom state
const zoomLevel = ref(1)
const zoomMin = ref(1)
const zoomMax = ref(1)
const zoomStep = ref(0.5)

/**
 * Composable for barcode scanning using device camera
 * Handles camera permissions, scanning, and audio feedback
 * Uses shared state to allow communication between button and modal
 *
 * @returns {Object} Scanner state and control methods
 */
export const useBarcodeScanner = () => {
  const { t } = useI18n()
  const haptics = useHaptics()

  /**
   * Play success beep sound using Web Audio API
   * Creates a pleasant two-tone "ding" sound
   */
  const playBeep = () => {
    try {
      // Use Web Audio API for better sound quality
      const AudioContextCtor = window.AudioContext ?? window.webkitAudioContext
      if (!AudioContextCtor) return
      const audioContext = new AudioContextCtor()

      // First tone (higher pitch)
      const oscillator1 = audioContext.createOscillator()
      const gainNode1 = audioContext.createGain()

      oscillator1.connect(gainNode1)
      gainNode1.connect(audioContext.destination)

      oscillator1.frequency.value = 800 // Hz
      oscillator1.type = 'sine'

      gainNode1.gain.setValueAtTime(0.3, audioContext.currentTime)
      gainNode1.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + 0.15)

      oscillator1.start(audioContext.currentTime)
      oscillator1.stop(audioContext.currentTime + 0.15)

      // Second tone (slightly lower pitch, delayed)
      const oscillator2 = audioContext.createOscillator()
      const gainNode2 = audioContext.createGain()

      oscillator2.connect(gainNode2)
      gainNode2.connect(audioContext.destination)

      oscillator2.frequency.value = 1000 // Hz
      oscillator2.type = 'sine'

      gainNode2.gain.setValueAtTime(0, audioContext.currentTime)
      gainNode2.gain.setValueAtTime(0.3, audioContext.currentTime + 0.05)
      gainNode2.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + 0.25)

      oscillator2.start(audioContext.currentTime + 0.05)
      oscillator2.stop(audioContext.currentTime + 0.25)
    } catch (error) {
      console.error('Failed to play beep sound:', error)
    }
  }

  /**
   * Start the barcode scanner
   * @param {Function} onSuccess - Callback function when barcode is detected
   */
  const startScanner = (onSuccess: (barcode: string) => void) => {
    scanError.value = null
    isScanning.value = true
    scanCallback.value = onSuccess
    isPaused.value = false
    // The browser has not handed us a track yet; @camera-on flips this to 'ready'.
    cameraState.value = 'requesting'
    successValue.value = null
    detectedCandidates.value = []
    candidateFrameCount.clear()
    totalFrameCount = 0
    if (bufferTimer) {
      clearTimeout(bufferTimer)
      bufferTimer = null
    }
  }

  /**
   * Haptic feedback on detection — goes through the shared vocabulary so it
   * follows the user's haptics switch and no-ops where vibration is unsupported.
   */
  const vibrateOnDetect = () => {
    haptics.success()
  }

  /** A scan attempt that decoded nothing (or blew up) gets the failure pattern. */
  const vibrateOnScanFailure = () => {
    haptics.error()
  }

  /**
   * Initialise torch and zoom capabilities from the camera-on event payload.
   * Torch is controlled via the :torch prop on QrcodeStream (library handles it).
   * Zoom still requires video track access for applyConstraints.
   *
   * @param capabilities - MediaTrackCapabilities emitted by @camera-on
   * @param containerEl  - The wrapper element containing the <video> tag (for zoom track lookup)
   */
  const initTorchAndZoom = async (capabilities: MediaTrackCapabilities | null | undefined, containerEl?: HTMLElement | null) => {
    if (!capabilities) return

    // Torch support: library controls it via :torch prop — we only need the flag
    torchSupported.value = capabilities.torch === true

    // Zoom support: read range from capabilities
    if (capabilities.zoom !== undefined && capabilities.zoom.max > capabilities.zoom.min) {
      zoomMin.value = capabilities.zoom.min
      zoomMax.value = capabilities.zoom.max
      zoomStep.value = capabilities.zoom.step ?? 0.5
      const defaultZoom = Math.min(capabilities.zoom.max, Math.max(capabilities.zoom.min, 2))
      zoomLevel.value = defaultZoom

      // Get video track for applyConstraints (zoom only)
      await nextTick()
      const root: ParentNode = containerEl ?? document
      const videoEl = root.querySelector('video') as HTMLVideoElement | null
      const stream = videoEl?.srcObject as MediaStream | null
      videoTrack = stream?.getVideoTracks()[0] ?? null

      // Apply default 2x zoom immediately
      if (videoTrack) {
        try {
          await videoTrack.applyConstraints({ advanced: [{ zoom: defaultZoom }] })
        } catch (err) {
          console.error('Default zoom failed:', err)
        }
      }
    }
  }

  /**
   * Toggle torch on/off.
   * Actual torch activation is handled by the :torch prop on QrcodeStream;
   * this method just flips the reactive flag.
   */
  const toggleTorch = () => {
    if (!torchSupported.value) return
    torchEnabled.value = !torchEnabled.value
  }

  /**
   * Set zoom level, clamped to device capabilities
   */
  const setZoom = async (value: number) => {
    if (!videoTrack) return
    const clamped = Math.min(zoomMax.value, Math.max(zoomMin.value, value))
    zoomLevel.value = clamped
    try {
      await videoTrack.applyConstraints({ advanced: [{ zoom: clamped }] })
    } catch (err) {
      console.error('Zoom failed:', err)
    }
  }

  /**
   * The single exit taken by every successful decode (live buffer, snapshot, or
   * a manual pick from the multi-code list): beep, buzz, freeze the frame on the
   * winning code so the overlay can snap its reticle onto it, then hand the value
   * to the caller and close.
   */
  const finishWithBarcode = (barcode: string) => {
    playBeep()
    vibrateOnDetect()
    detectedBarcode.value = barcode
    successValue.value = barcode
    // Freezes the stream, and keeps handleDetect from firing during the beat.
    isPaused.value = true

    // stopScanner() clears it, so take the reference before the timer runs.
    const callback = scanCallback.value

    if (successTimer) clearTimeout(successTimer)
    successTimer = setTimeout(() => {
      successTimer = null
      stopScanner()
      isScannerOpen.value = false
      if (callback) callback(barcode)
    }, SUCCESS_HOLD_MS)
  }

  /**
   * Commit buffered candidates: auto-select if only one, show list if multiple
   */
  const commitCandidates = () => {
    bufferTimer = null

    // Only keep barcodes seen in ≥50% of all frames (stable detections)
    const stableCandidates = Array.from(candidateFrameCount.entries())
      .filter(([, count]) => totalFrameCount > 0 && count / totalFrameCount >= STABILITY_THRESHOLD)
      .map(([barcode]) => barcode)

    candidateFrameCount.clear()
    totalFrameCount = 0

    // No stable candidates → noise only, restart window silently
    if (stableCandidates.length === 0) return

    if (stableCandidates.length === 1) {
      finishWithBarcode(stableCandidates[0]!)
    } else {
      // Multiple stable candidates – pause and let user choose
      isPaused.value = true
      detectedCandidates.value = stableCandidates
    }
  }

  /**
   * Handle barcode detection from live camera feed
   * @param {Array} detectedCodes - Array of detected barcodes
   */
  const handleDetect = (detectedCodes: DetectedBarcode[]) => {
    if (isPaused.value || !isScanning.value || detectedCodes.length === 0) return

    // Count how many frames each barcode value appears in
    totalFrameCount++
    for (const code of detectedCodes) {
      const val: string = code.rawValue
      candidateFrameCount.set(val, (candidateFrameCount.get(val) ?? 0) + 1)
    }

    // Fixed-length window: start timer only once per window
    if (!bufferTimer) {
      bufferTimer = setTimeout(commitCandidates, BUFFER_MS)
    }
  }

  /**
   * User selected one barcode from the multi-detection list
   */
  const selectCandidate = (barcode: string) => {
    detectedCandidates.value = []
    candidateFrameCount.clear()
    totalFrameCount = 0
    finishWithBarcode(barcode)
  }

  /**
   * Dismiss the candidate list and resume live scanning
   */
  const dismissCandidates = () => {
    detectedCandidates.value = []
    candidateFrameCount.clear()
    totalFrameCount = 0
    if (bufferTimer) {
      clearTimeout(bufferTimer)
      bufferTimer = null
    }
    isPaused.value = false
  }

  /**
   * Capture and scan current frame
   * Takes a snapshot of the current video feed and attempts to scan it
   */
  const captureAndScan = async (
    videoRef: Ref<HTMLVideoElement | null>,
    onFreeze?: (imageUrl: string) => void,
    onUnfreeze?: () => void
  ) => {
    if (!videoRef.value) return

    isPaused.value = true

    try {
      const video = videoRef.value

      // Create a high-resolution canvas to capture the frame
      const canvas = document.createElement('canvas')
      const scaleFactor = 2 // 2x resolution for better barcode detection
      canvas.width = video.videoWidth * scaleFactor
      canvas.height = video.videoHeight * scaleFactor
      const ctx = canvas.getContext('2d')
      if (!ctx) return

      // Enable image smoothing for better quality
      ctx.imageSmoothingEnabled = true
      ctx.imageSmoothingQuality = 'high'
      ctx.drawImage(video, 0, 0, canvas.width, canvas.height)

      // Get data URL for preview (use lower quality for preview)
      const previewCanvas = document.createElement('canvas')
      previewCanvas.width = video.videoWidth
      previewCanvas.height = video.videoHeight
      const previewCtx = previewCanvas.getContext('2d')
      if (previewCtx) {
        previewCtx.drawImage(video, 0, 0)
        const imageUrl = previewCanvas.toDataURL('image/jpeg', 0.9)
        if (onFreeze) onFreeze(imageUrl)
      }

      // Convert high-res canvas to blob with maximum quality
      canvas.toBlob(async (blob) => {
        if (!blob) {
          isPaused.value = false
          if (onUnfreeze) onUnfreeze()
          return
        }

        try {
          // Use BarcodeDetector API (polyfilled by vue-qrcode-reader, so the
          // optional global is present by the time the scanner is open)
          const BarcodeDetectorCtor = window.BarcodeDetector
          if (!BarcodeDetectorCtor) throw new Error('BarcodeDetector is unavailable')

          const barcodeDetector = new BarcodeDetectorCtor({
            formats: ['linear_codes', 'matrix_codes']
          })

          const detectedCodes = await barcodeDetector.detect(await createImageBitmap(blob))

          if (detectedCodes.length > 0) {
            if (detectedCodes.length === 1) {
              // Single result – auto-confirm
              finishWithBarcode(detectedCodes[0]!.rawValue)
            } else {
              // Multiple results – let user choose
              detectedCandidates.value = [...new Set(detectedCodes.map(code => code.rawValue))]
              // Keep isPaused = true; frozen image stays visible until user picks
            }
          } else {
            // No barcode found in snapshot - wait 2 seconds then resume video
            vibrateOnScanFailure()
            setTimeout(() => {
              isPaused.value = false
              if (onUnfreeze) onUnfreeze()
            }, 2000)
          }
        } catch (err) {
          console.error('Barcode detection error:', err)
          // Error during detection - wait 2 seconds then resume video
          vibrateOnScanFailure()
          setTimeout(() => {
            isPaused.value = false
            if (onUnfreeze) onUnfreeze()
          }, 2000)
        }
      }, 'image/jpeg', 1.0) // Maximum quality (1.0)
    } catch (err) {
      // Error during capture - resume immediately
      console.error('Capture error:', err)
      isPaused.value = false
      if (onUnfreeze) onUnfreeze()
    }
  }

  /**
   * Stop the barcode scanner and release camera
   */
  const stopScanner = () => {
    isScanning.value = false
    isPaused.value = false
    scanCallback.value = null
    cameraState.value = 'idle'
    successValue.value = null
    if (successTimer) {
      clearTimeout(successTimer)
      successTimer = null
    }
    detectedCandidates.value = []
    candidateFrameCount.clear()
    totalFrameCount = 0
    if (bufferTimer) {
      clearTimeout(bufferTimer)
      bufferTimer = null
    }
    // Reset torch (library cleans up via :torch prop going false)
    torchEnabled.value = false
    torchSupported.value = false
    videoTrack = null
    // Reset zoom
    zoomLevel.value = 1
    zoomMin.value = 1
    zoomMax.value = 1
  }

  /**
   * Handle camera errors
   * @param {Error} error - Camera error
   */
  const handleCameraError = (error: Error) => {
    if (error.name === 'NotAllowedError') {
      scanError.value = t('barcodeScanner.errors.permissionDenied')
      cameraState.value = 'denied'
    } else if (error.name === 'NotFoundError' || error.name === 'OverconstrainedError') {
      scanError.value = t('barcodeScanner.errors.noCameraFound')
      cameraState.value = 'missing'
    } else if (error.name === 'NotReadableError') {
      scanError.value = t('barcodeScanner.errors.cameraAccessFailed')
      cameraState.value = 'failed'
    } else {
      scanError.value = error.message || t('barcodeScanner.errors.scanFailed')
      cameraState.value = 'failed'
    }
    haptics.error()
    isScanning.value = false
  }

  /** The stream is live — called from the modal's `@camera-on`. */
  const markCameraReady = () => {
    cameraState.value = 'ready'
    scanError.value = null
    // handleCameraError turns scanning off; a retry or a camera switch that then
    // succeeds has to turn it back on, or the detector's results are ignored.
    if (scanCallback.value) isScanning.value = true
  }

  /**
   * Tear the stream down and open it again. A browser will not re-prompt for a
   * permission it has already been refused, but the user may have just changed
   * it in site settings — and it is the only way out of a transient
   * NotReadableError (camera held by another app).
   */
  const retryCamera = () => {
    scanError.value = null
    cameraState.value = 'requesting'
    streamNonce.value++
  }

  /** Flip between the back and front camera; the stream remounts on the new constraints. */
  const switchCamera = () => {
    facingMode.value = facingMode.value === 'environment' ? 'user' : 'environment'
    // Torch/zoom belong to the track that is about to be replaced.
    torchEnabled.value = false
    torchSupported.value = false
    videoTrack = null
    zoomLevel.value = 1
    zoomMin.value = 1
    zoomMax.value = 1
    cameraState.value = 'requesting'
    streamNonce.value++
  }

  /**
   * The one value the overlay switches on. Camera trouble outranks everything —
   * there is nothing to search for without a stream.
   */
  const scanState = computed<'requesting' | 'denied' | 'no-camera' | 'error' | 'searching' | 'found'>(() => {
    if (cameraState.value === 'denied') return 'denied'
    if (cameraState.value === 'missing') return 'no-camera'
    if (cameraState.value === 'failed') return 'error'
    if (successValue.value) return 'found'
    if (cameraState.value === 'ready') return 'searching'
    return 'requesting'
  })

  /**
   * Open the scanner modal
   */
  const openScanner = () => {
    isScannerOpen.value = true
  }

  /**
   * Close the scanner modal and cleanup
   */
  const closeScanner = async () => {
    stopScanner()
    isScannerOpen.value = false
    scanError.value = null
  }

  // Cleanup on component unmount
  onUnmounted(() => {
    stopScanner()
  })

  return {
    isScannerOpen,
    isScanning,
    isPaused,
    scanError,
    detectedBarcode,
    startScanner,
    stopScanner,
    captureAndScan,
    handleDetect,
    handleCameraError,
    openScanner,
    closeScanner,
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
    cameraState,
    scanState,
    successValue,
    markCameraReady,
    retryCamera,
    switchCamera,
    facingMode,
    streamNonce
  }
}
