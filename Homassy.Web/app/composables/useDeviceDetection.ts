import { ref, onMounted, onUnmounted } from 'vue'

/**
 * Composable for detecting mobile devices
 * Uses CSS media queries to detect touch-capable devices with coarse pointers
 *
 * @returns {Object} Object containing isMobile ref
 */
export const useDeviceDetection = () => {
  const isMobile = ref(false)

  onMounted(() => {
    // Detect touch-capable device with coarse pointer (mobile/tablet)
    const checkMobile = () => {
      // (hover: none) detects touch-only devices
      // (pointer: coarse) confirms touch input (not mouse)
      isMobile.value = window.matchMedia('(hover: none) and (pointer: coarse)').matches
    }

    // Initial check
    checkMobile()

    // Update on window resize
    window.addEventListener('resize', checkMobile)

    onUnmounted(() => {
      window.removeEventListener('resize', checkMobile)
    })
  })

  return {
    isMobile
  }
}
