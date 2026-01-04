import { ref, onUnmounted } from 'vue'

// Global state shared across all instances
const isScannerOpen = ref(false)
const isScanning = ref(false)
const scanError = ref<string | null>(null)
const detectedBarcode = ref<string | null>(null)

let html5QrCode: any = null
let Html5Qrcode: any = null

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
      }

      html5QrCode = new Html5Qrcode(elementId)

      await html5QrCode.start(
        { facingMode: 'environment' }, // Use rear camera
        {
          fps: 10, // Frames per second (reduce CPU usage)
          qrbox: { width: 250, height: 150 }, // Scanning area optimized for barcodes
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
   * Stop the barcode scanner and release camera
   */
  const stopScanner = async () => {
    if (html5QrCode) {
      try {
        await html5QrCode.stop()
        html5QrCode.clear()
      } catch (err) {
        console.error('Error stopping scanner:', err)
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
    openScanner,
    closeScanner
  }
}
