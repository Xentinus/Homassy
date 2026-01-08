import { ref, onUnmounted, type Ref } from 'vue'

// Global state shared across all instances
const isScannerOpen = ref(false)
const isScanning = ref(false)
const isPaused = ref(false)
const scanError = ref<string | null>(null)
const detectedBarcode = ref<string | null>(null)
const scanCallback = ref<((barcode: string) => void) | null>(null)

/**
 * Composable for barcode scanning using device camera
 * Handles camera permissions, scanning, and audio feedback
 * Uses shared state to allow communication between button and modal
 *
 * @returns {Object} Scanner state and control methods
 */
export const useBarcodeScanner = () => {
  const { t } = useI18n()

  /**
   * Play success beep sound using Web Audio API
   * Creates a pleasant two-tone "ding" sound
   */
  const playBeep = () => {
    try {
      // Use Web Audio API for better sound quality
      const audioContext = new (window.AudioContext || (window as any).webkitAudioContext)()

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
  }

  /**
   * Handle barcode detection from live camera feed
   * @param {Array} detectedCodes - Array of detected barcodes
   */
  const handleDetect = (detectedCodes: any[]) => {
    if (isPaused.value || !isScanning.value || detectedCodes.length === 0) return

    const barcode = detectedCodes[0].rawValue
    console.log('📷 Barcode detected:', barcode)
    console.log('📞 Callback exists:', !!scanCallback.value)

    playBeep()
    detectedBarcode.value = barcode

    // Store callback before stopping scanner (which clears it)
    const callback = scanCallback.value

    stopScanner()
    isScannerOpen.value = false

    if (callback) {
      console.log('✅ Calling callback with barcode:', barcode)
      callback(barcode)
    } else {
      console.error('❌ No callback registered!')
    }
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
          // Use BarcodeDetector API (polyfilled by vue-qrcode-reader)
          const barcodeDetector = new (window as any).BarcodeDetector({
            formats: ['linear_codes', 'matrix_codes']
          })

          const detectedCodes = await barcodeDetector.detect(await createImageBitmap(blob))

          if (detectedCodes.length > 0) {
            // Success - barcode found
            const barcode = detectedCodes[0].rawValue
            playBeep()
            detectedBarcode.value = barcode

            // Store callback before stopping scanner (which clears it)
            const callback = scanCallback.value

            stopScanner()
            isScannerOpen.value = false

            if (callback) {
              callback(barcode)
            }
          } else {
            // No barcode found in snapshot - wait 2 seconds then resume video
            setTimeout(() => {
              isPaused.value = false
              if (onUnfreeze) onUnfreeze()
            }, 2000)
          }
        } catch (err) {
          console.error('Barcode detection error:', err)
          // Error during detection - wait 2 seconds then resume video
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
  }

  /**
   * Handle camera errors
   * @param {Error} error - Camera error
   */
  const handleCameraError = (error: Error) => {
    if (error.name === 'NotAllowedError') {
      scanError.value = t('barcodeScanner.errors.permissionDenied')
    } else if (error.name === 'NotFoundError') {
      scanError.value = t('barcodeScanner.errors.noCameraFound')
    } else if (error.name === 'NotReadableError') {
      scanError.value = t('barcodeScanner.errors.cameraAccessFailed')
    } else {
      scanError.value = error.message || t('barcodeScanner.errors.scanFailed')
    }
    isScanning.value = false
  }

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
    closeScanner
  }
}
