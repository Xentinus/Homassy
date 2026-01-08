import { ref, onMounted } from 'vue'

/**
 * Composable to check camera availability and permissions
 * 
 * Logic:
 * - If permission is 'denied' -> hide button (user blocked camera)
 * - If permission is 'prompt' -> show button (browser will request permission)
 * - If permission is 'granted' and camera exists -> show button
 * - If permission is 'granted' and no camera exists -> hide button
 * 
 * @returns {Object} Camera availability state
 */
export const useCameraAvailability = () => {
  const showCameraButton = ref(true)
  const isChecking = ref(true)

  /**
   * Check if at least one camera device is available
   */
  const hasCameraDevice = async (): Promise<boolean> => {
    try {
      if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) {
        return false
      }
      
      const devices = await navigator.mediaDevices.enumerateDevices()
      const cameras = devices.filter(device => device.kind === 'videoinput')
      return cameras.length > 0
    } catch (error) {
      console.error('Error checking camera devices:', error)
      return false
    }
  }

  /**
   * Check camera permission status without requesting it
   */
  const getCameraPermissionStatus = async (): Promise<'granted' | 'denied' | 'prompt' | 'unknown'> => {
    try {
      if (!navigator.permissions) {
        return 'unknown'
      }
      
      const permissionStatus = await navigator.permissions.query({ name: 'camera' as PermissionName })
      return permissionStatus.state
    } catch (error) {
      console.error('Error checking camera permission:', error)
      return 'unknown'
    }
  }

  /**
   * Determine if camera button should be shown
   * 
   * Show button if:
   * - Permission is 'prompt' (user hasn't been asked yet)
   * - Permission is 'granted' AND camera exists
   * 
   * Hide button if:
   * - Permission is 'denied' (user blocked camera)
   * - Permission is 'granted' AND no camera exists
   */
  const checkCameraAvailability = async () => {
    isChecking.value = true
    
    try {
      const permissionStatus = await getCameraPermissionStatus()
      
      // If permission is denied, hide the button
      if (permissionStatus === 'denied') {
        showCameraButton.value = false
        return
      }
      
      // If permission is prompt (not asked yet), show the button
      if (permissionStatus === 'prompt' || permissionStatus === 'unknown') {
        showCameraButton.value = true
        return
      }
      
      // Permission is granted, check if camera actually exists
      const hasCamera = await hasCameraDevice()
      
      // Only show button if camera exists
      showCameraButton.value = hasCamera
      
    } catch (error) {
      console.error('Error checking camera availability:', error)
      // On error, hide button to be safe
      showCameraButton.value = false
    } finally {
      isChecking.value = false
    }
  }

  onMounted(() => {
    checkCameraAvailability()
  })

  return {
    showCameraButton,
    isChecking,
    checkCameraAvailability
  }
}
