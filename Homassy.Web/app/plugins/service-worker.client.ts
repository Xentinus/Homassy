export default defineNuxtPlugin(() => {
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker
      .register('/sw-push.js')
      .catch((error) => {
        console.error('Service worker registration failed:', error)
      })
  }
})
