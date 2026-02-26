import { ref, onMounted, onBeforeUnmount } from 'vue'

const THRESHOLD = 70
const MAX_PULL = 110
const DAMPING = 0.45

export function usePullToRefresh(onRefresh: () => Promise<void> | void) {
  const pullDistance = ref(0)
  const isPulling = ref(false)
  const isRefreshing = ref(false)
  const isReady = ref(false)

  let startY = 0
  let startedAtTop = false

  const onTouchStart = (e: TouchEvent) => {
    if (isRefreshing.value) return
    startY = e.touches[0]!.clientY
    startedAtTop = window.scrollY === 0
    isPulling.value = false
  }

  const onTouchMove = (e: TouchEvent) => {
    if (isRefreshing.value || !startedAtTop) return

    const currentY = e.touches[0]!.clientY
    const delta = currentY - startY

    if (delta <= 0) {
      pullDistance.value = 0
      isPulling.value = false
      isReady.value = false
      return
    }

    const damped = Math.min(delta * DAMPING, MAX_PULL)
    pullDistance.value = damped
    isPulling.value = damped > 4
    isReady.value = damped >= THRESHOLD * DAMPING

    if (damped > 4) {
      e.preventDefault()
    }
  }

  const onTouchEnd = async () => {
    if (isRefreshing.value || !startedAtTop) return

    if (isReady.value) {
      isRefreshing.value = true
      isPulling.value = false
      pullDistance.value = 0
      isReady.value = false

      try {
        await onRefresh()
      } finally {
        isRefreshing.value = false
      }
    } else {
      pullDistance.value = 0
      isPulling.value = false
      isReady.value = false
    }

    startedAtTop = false
  }

  onMounted(() => {
    document.addEventListener('touchstart', onTouchStart, { passive: true })
    document.addEventListener('touchmove', onTouchMove, { passive: false })
    document.addEventListener('touchend', onTouchEnd, { passive: true })
  })

  onBeforeUnmount(() => {
    document.removeEventListener('touchstart', onTouchStart)
    document.removeEventListener('touchmove', onTouchMove)
    document.removeEventListener('touchend', onTouchEnd)
  })

  return {
    pullDistance,
    isPulling,
    isRefreshing,
    isReady
  }
}
