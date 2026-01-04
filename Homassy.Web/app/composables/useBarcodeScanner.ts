import { ref, onUnmounted } from 'vue'

// Global state shared across all instances
const isScannerOpen = ref(false)
const isScanning = ref(false)
const scanError = ref<string | null>(null)
const detectedBarcode = ref<string | null>(null)

let html5QrCode: any = null
let Html5Qrcode: any = null
let Html5QrcodeSupportedFormats: any = null

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
   * Request camera permission
   * @returns {Promise<boolean>} True if permission granted, false otherwise
   */
  const requestCameraPermission = async (): Promise<boolean> => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ video: true })
      // Stop the stream immediately after permission check
      stream.getTracks().forEach((track) => track.stop())
      return true
    } catch (err: any) {
      if (err.name === 'NotAllowedError') {
        scanError.value = t('barcodeScanner.errors.permissionDenied')
      } else if (err.name === 'NotFoundError') {
        scanError.value = t('barcodeScanner.errors.noCameraFound')
      } else {
        scanError.value = t('barcodeScanner.errors.cameraAccessFailed')
      }
      return false
    }
  }

  /**
   * Start the barcode scanner
   * @param {string} elementId - DOM element ID for camera preview
   * @param {Function} onSuccess - Callback function when barcode is detected
   */
  const startScanner = async (
    elementId: string,
    onSuccess: (barcode: string) => void
  ) => {
    try {
      scanError.value = null

      // Request camera permission first
      const hasPermission = await requestCameraPermission()
      if (!hasPermission) {
        isScanning.value = false
        return
      }

      isScanning.value = true

      // Dynamically import html5-qrcode (client-side only)
      if (!Html5Qrcode) {
        const module = await import('html5-qrcode')
        Html5Qrcode = module.Html5Qrcode
        Html5QrcodeSupportedFormats = module.Html5QrcodeSupportedFormats
      }

      html5QrCode = new Html5Qrcode(elementId)

      await html5QrCode.start(
        { facingMode: 'environment' }, // Use rear camera (simplified)
        {
          fps: 10, // Frames per second (slower for better focus)
          qrbox: (viewfinderWidth, viewfinderHeight) => {
            // Use 70% of the viewfinder width for scanning area
            const minEdgeSize = Math.min(viewfinderWidth, viewfinderHeight)
            const qrboxSize = Math.max(Math.floor(minEdgeSize * 0.7), 150) // Minimum 150px
            const qrboxHeight = Math.max(Math.floor(qrboxSize * 0.4), 100) // Minimum 100px, wider for barcodes
            return {
              width: qrboxSize,
              height: qrboxHeight // Wider rectangle for barcodes
            }
          },
          aspectRatio: 1.777778 // 16:9 aspect ratio
        },
        async (decodedText) => {
          // Success callback - barcode detected
          playBeep()
          detectedBarcode.value = decodedText

          // Stop scanner and close modal
          await stopScanner()
          isScannerOpen.value = false

          // Call the success callback
          onSuccess(decodedText)
        },
        (errorMessage) => {
          // Error callback - ignore scan failures during scanning
          // These are normal and happen continuously while no barcode is detected
        }
      )
    } catch (err: any) {
      scanError.value = err.message || t('barcodeScanner.errors.scanFailed')
      isScanning.value = false
    }
  }

  /**
   * Capture and scan current frame
   * Takes a snapshot of the current video feed and attempts to scan it
   */
  const captureAndScan = async (onSuccess: (barcode: string) => void, onFreeze?: (imageUrl: string) => void, onUnfreeze?: () => void) => {
    if (!html5QrCode) return

    try {
      // Get the video element from the scanner
      const videoElement = document.querySelector('#barcode-scanner-reader video') as HTMLVideoElement
      if (!videoElement) return

      // Pause the video to freeze the frame
      videoElement.pause()

      // Create a high-resolution canvas to capture the frame
      const canvas = document.createElement('canvas')
      // Use higher resolution if available
      const scaleFactor = 2 // 2x resolution for better barcode detection
      canvas.width = videoElement.videoWidth * scaleFactor
      canvas.height = videoElement.videoHeight * scaleFactor
      const ctx = canvas.getContext('2d')
      if (!ctx) return

      // Enable image smoothing for better quality
      ctx.imageSmoothingEnabled = true
      ctx.imageSmoothingQuality = 'high'

      // Draw current video frame to canvas with scaling
      ctx.drawImage(videoElement, 0, 0, canvas.width, canvas.height)

      // Get data URL for preview (use lower quality for preview)
      const previewCanvas = document.createElement('canvas')
      previewCanvas.width = videoElement.videoWidth
      previewCanvas.height = videoElement.videoHeight
      const previewCtx = previewCanvas.getContext('2d')
      if (previewCtx) {
        previewCtx.drawImage(videoElement, 0, 0)
        const imageUrl = previewCanvas.toDataURL('image/jpeg', 0.9)
        if (onFreeze) onFreeze(imageUrl)
      }

      // Convert high-res canvas to blob with maximum quality
      canvas.toBlob(async (blob) => {
        if (!blob) {
          // Resume video if capture failed
          videoElement.play()
          if (onUnfreeze) onUnfreeze()
          return
        }

        // Create file from blob
        const file = new File([blob], 'snapshot.jpg', { type: 'image/jpeg' })

        try {
          // IMPORTANT: Stop the camera scanner completely before scanning the file
          // html5-qrcode doesn't allow simultaneous camera and file scanning
          const wasScanning = html5QrCode.getState() === 2
          if (wasScanning) {
            await html5QrCode.stop()
          }

          // Scan the captured image with all formats enabled
          const decodedText = await html5QrCode.scanFile(file, true)

          // Success - barcode found
          playBeep()
          detectedBarcode.value = decodedText

          // Clear and close
          html5QrCode.clear()
          html5QrCode = null
          isScannerOpen.value = false
          isScanning.value = false
          onSuccess(decodedText)
        } catch (err) {
          // Log the error for debugging
          console.log('No barcode detected in captured frame:', err)

          // Restart the camera scanner if it was scanning
          try {
            await html5QrCode.start(
              { facingMode: 'environment' },
              {
                fps: 10,
                qrbox: (viewfinderWidth: number, viewfinderHeight: number) => {
                  const minEdgeSize = Math.min(viewfinderWidth, viewfinderHeight)
                  const qrboxSize = Math.max(Math.floor(minEdgeSize * 0.7), 150)
                  const qrboxHeight = Math.max(Math.floor(qrboxSize * 0.4), 100)
                  return {
                    width: qrboxSize,
                    height: qrboxHeight
                  }
                },
                aspectRatio: 1.777778
              },
              async (decodedText: string) => {
                playBeep()
                detectedBarcode.value = decodedText
                await stopScanner()
                isScannerOpen.value = false
                onSuccess(decodedText)
              },
              () => {
                // Error callback - ignore
              }
            )
          } catch (restartErr) {
            console.error('Error restarting scanner:', restartErr)
          }

          // No barcode found in snapshot - wait 2 seconds then resume video
          setTimeout(() => {
            videoElement.play()
            if (onUnfreeze) onUnfreeze()
          }, 2000)
        }
      }, 'image/jpeg', 1.0) // Maximum quality (1.0)
    } catch (err) {
      // Error during capture - continue live scanning
      console.error('Capture error:', err)
    }
  }

  /**
   * Stop the barcode scanner and release camera
   */
  const stopScanner = async () => {
    if (html5QrCode) {
      try {
        // Check if scanner is actually running before stopping
        const state = html5QrCode.getState()
        if (state === 2) { // 2 = SCANNING state
          await html5QrCode.stop()
        }
        html5QrCode.clear()
      } catch (err) {
        // Silently ignore stop errors as scanner might already be stopped
      }
      html5QrCode = null
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
    await stopScanner()
    isScannerOpen.value = false
    scanError.value = null
  }

  // Cleanup on component unmount
  onUnmounted(async () => {
    await stopScanner()
  })

  return {
    isScannerOpen,
    isScanning,
    scanError,
    detectedBarcode,
    startScanner,
    stopScanner,
    captureAndScan,
    openScanner,
    closeScanner
  }
}
