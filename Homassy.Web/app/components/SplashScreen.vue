<script setup lang="ts">
import { useAuthStore } from '~/stores/auth'
import type { VersionInfo } from '~/types/version'

// Boot splash shown only when running as an installed PWA (standalone display
// mode — enforced in CSS below). Logo + spinner render server-side so they
// paint during the pre-hydration auth window; the name/version are client-only
// (auth + version resolve after hydration) and are wrapped in <ClientOnly> to
// avoid a hydration mismatch.

const authStore = useAuthStore()

const userName = computed(() => authStore.user?.displayName || authStore.user?.name || '')

// Reuse the exact version source + formatting from the profile page: the value
// comes from GET /api/Version (backend assembly version), short form in prod.
const isProduction = import.meta.env.PROD
const versionInfo = ref<VersionInfo | null>(null)
const displayVersion = computed(() => {
  if (!versionInfo.value) return ''
  return isProduction ? versionInfo.value.shortVersion : versionInfo.value.version
})

onMounted(async () => {
  try {
    const res = await useVersionApi().getVersion()
    if (res.success && res.data) versionInfo.value = res.data
  }
  catch {
    // Version is best-effort on the splash — ignore failures.
  }
})
</script>

<template>
  <div class="splash" aria-hidden="true">
    <div class="splash__inner">
      <img
        src="/favicon.svg"
        alt="Homassy"
        class="splash__logo"
        width="88"
        height="88"
      >

      <ClientOnly>
        <p v-if="userName" class="splash__welcome">
          <span class="splash__welcome-label">{{ $t('splash.welcomeBack') }}</span>
          <span class="splash__name">{{ userName }}</span>
        </p>
      </ClientOnly>

      <div class="splash__spinner" role="status" :aria-label="$t('common.loading')" />
    </div>

    <ClientOnly>
      <p v-if="displayVersion" class="splash__version">v{{ displayVersion }}</p>
    </ClientOnly>
  </div>
</template>

<!-- Global (unscoped) so the `.splash` class stays literal for the
     `:root[data-splash-ready]` dismissal selector and the standalone media query. -->
<style>
.splash {
  display: none;
  position: fixed;
  inset: 0;
  z-index: 2147483000;
  background-color: #c9b8a0;
  color: #2b2620;
  transition: opacity 0.3s ease;
}

/* Only ever visible when launched as an installed PWA. `pwa-standalone` is set
   by the inline head script (covers iOS, where the media query is unreliable);
   the media query covers Android/desktop installed PWAs. */
@media all and (display-mode: standalone) {
  .splash {
    display: flex;
    align-items: center;
    justify-content: center;
  }
}

:root.pwa-standalone .splash {
  display: flex;
  align-items: center;
  justify-content: center;
}

/* Dismissal: stamped on <html> by useSplashScreen().markReady(). */
:root[data-splash-ready] .splash {
  opacity: 0;
  pointer-events: none;
  visibility: hidden;
}

.splash__inner {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1.25rem;
  padding: 2rem;
}

.splash__logo {
  width: 88px;
  height: 88px;
  filter: drop-shadow(0 2px 6px rgba(0, 0, 0, 0.15));
}

.splash__welcome {
  text-align: center;
  line-height: 1.3;
}

.splash__welcome-label {
  display: block;
  font-size: 0.8rem;
  opacity: 0.7;
}

.splash__name {
  display: block;
  font-size: 1.35rem;
  font-weight: 600;
}

.splash__spinner {
  width: 34px;
  height: 34px;
  border-radius: 50%;
  border: 3px solid rgba(43, 38, 32, 0.25);
  border-top-color: #2b2620;
  animation: splash-spin 0.8s linear infinite;
}

.splash__version {
  position: fixed;
  bottom: max(1.5rem, env(safe-area-inset-bottom));
  left: 0;
  right: 0;
  text-align: center;
  font-size: 0.75rem;
  opacity: 0.6;
}

@keyframes splash-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .splash__spinner {
    animation-duration: 1.6s;
  }
}
</style>
