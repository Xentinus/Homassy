<script setup lang="ts">
import { useAuthStore } from '~/stores/auth'
import type { VersionInfo } from '~/types/version'

// Boot splash shown only when running as an installed PWA (standalone display
// mode — enforced in CSS below). Logo + the loading ring around it render
// server-side so they paint during the pre-hydration auth window; the
// name/version are client-only (auth + version resolve after hydration) and are
// wrapped in <ClientOnly> to avoid a hydration mismatch.

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
      <div class="splash__badge">
        <div class="splash__ring" role="status" :aria-label="$t('common.loading')" />
        <img
          src="/favicon.svg"
          alt="Homassy"
          class="splash__logo"
          width="88"
          height="88"
        >
      </div>

      <ClientOnly>
        <p v-if="userName" class="splash__welcome">
          <span class="splash__welcome-label">{{ $t('splash.welcomeBack') }}</span>
          <span class="splash__name">{{ userName }}</span>
        </p>
      </ClientOnly>
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
  background-color: #2b2620;
  color: #f5eee2;
  transition:
    opacity 0.45s cubic-bezier(0.7, 0, 0.2, 1),
    transform 0.55s cubic-bezier(0.7, 0, 0.2, 1);
}

/* App content beneath the splash. It fades in as the curtain lifts; gated on
   standalone so a normal browser tab never fades. No transform here — the auth
   layout's bottom nav is position:fixed, and a transformed ancestor would
   become its containing block and misposition it during the animation. */
.app-shell {
  transition: opacity 0.5s cubic-bezier(0.16, 1, 0.3, 1) 0.1s;
}

:root.pwa-standalone:not([data-splash-ready]) .app-shell {
  opacity: 0;
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
/* Dismissal: the splash slides up out of the frame (curtain), revealing the
   app beneath. translateY is off-screen; opacity + pointer-events retire it.
   visibility is intentionally NOT set — it snaps and would kill the slide. */
:root[data-splash-ready] .splash {
  opacity: 0;
  transform: translateY(-102%);
  pointer-events: none;
}

.splash__inner {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1.25rem;
  padding: 2rem;
}

/* The loading ring is a rotating conic arc, masked into a thin band so the
   espresso background shows through its centre; the static logo sits on top. */
.splash__badge {
  position: relative;
  width: 128px;
  height: 128px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.splash__ring {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  background: conic-gradient(from 0deg, #f0e6d6 0deg 250deg, #413a31 250deg 360deg);
  -webkit-mask: radial-gradient(farthest-side, transparent calc(100% - 7px), #000 calc(100% - 7px));
  mask: radial-gradient(farthest-side, transparent calc(100% - 7px), #000 calc(100% - 7px));
  animation: splash-spin 0.9s linear infinite;
}

.splash__logo {
  width: 88px;
  height: 88px;
  filter: drop-shadow(0 2px 6px rgba(0, 0, 0, 0.25));
}

.splash__welcome {
  text-align: center;
  line-height: 1.3;
}

.splash__welcome-label {
  display: block;
  font-size: 0.8rem;
  color: #a58e74;
}

.splash__name {
  display: block;
  font-size: 1.35rem;
  font-weight: 600;
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
  .splash__ring {
    animation-duration: 2.4s;
  }

  /* No curtain slide — fall back to a plain fade for both layers. */
  .splash {
    transition: opacity 0.2s ease;
  }

  :root[data-splash-ready] .splash {
    transform: none;
  }

  .app-shell {
    transition: opacity 0.2s ease;
  }
}
</style>
