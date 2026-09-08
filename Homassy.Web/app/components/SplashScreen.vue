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
        <!-- Inlined rather than <img src="/favicon.svg"> so the paths are
             animatable (an <img> is an opaque box to CSS) — and so the one
             screen every launch goes through costs no extra request. The three
             subpaths are the isometric mark's faces, in draw order. -->
        <svg
          class="splash__logo"
          viewBox="0 0 512 512"
          width="88"
          height="88"
          xmlns="http://www.w3.org/2000/svg"
          focusable="false"
        >
          <path class="splash__logo-face splash__logo-face--top" pathLength="1" d="M256 36 L69 146 L161 198 L256 143 L352 198 L443 145 Z" />
          <path class="splash__logo-face splash__logo-face--left" pathLength="1" d="M66 151 L65 368 L252 476 L252 370 L157 315 L157 205 Z" />
          <path class="splash__logo-face splash__logo-face--right" pathLength="1" d="M447 151 L355 205 L355 315 L260 370 L261 476 L447 368 Z" />
        </svg>
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
/* Colours come from Nuxt UI's semantic tokens, never hardcoded hex: the splash
   has to match whichever theme the app is in (light / dark / system). No
   `:root.dark` selectors are needed — the `--ui-*` variables flip themselves via
   the `light`/`dark` class, and @nuxtjs/color-mode puts that class on <html> from
   a blocking inline head script, i.e. before the first paint. So the splash is
   already the right theme on frame one, with no flash and no hydration diff. */
.splash {
  display: none;
  position: fixed;
  inset: 0;
  z-index: 2147483000;
  /* Exactly the app's own background (`bg-default` on <body>), so the handoff
     from splash to app is seamless in both themes. */
  background-color: var(--ui-bg);
  color: var(--ui-text-highlighted);
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

/* Dismissal (stamped on <html> by useSplashScreen().markReady()): the splash
   slides up out of frame (curtain), revealing the app beneath.
   visibility is restored in the END state — but only AFTER the slide (a 0s
   change delayed to the transform's duration) so it doesn't snap and cut the
   animation short. Restoring it matters on iOS standalone: a fixed inset:0
   element's background-color still tints the status-bar area even at opacity:0
   + off-screen transform, unless the element is actually hidden. The base
   `.splash` above does not transition visibility, so re-showing on resume
   (rearm removes the attribute) flips back to visible instantly. */
:root[data-splash-ready] .splash {
  opacity: 0;
  transform: translateY(-102%);
  pointer-events: none;
  visibility: hidden;
  transition:
    opacity 0.45s cubic-bezier(0.7, 0, 0.2, 1),
    transform 0.55s cubic-bezier(0.7, 0, 0.2, 1),
    visibility 0s linear 0.55s;
}

.splash__inner {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1.25rem;
  padding: 2rem;
}

/* The loading ring is a rotating conic arc, masked into a thin band so the
   page background shows through its centre; the static logo sits on top. */
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
  /* Arc = the mocha primary, track = an accented surface — both readable on the
     light and the dark background without a second rule. */
  background: conic-gradient(from 0deg, var(--ui-primary) 0deg 250deg, var(--ui-bg-accented) 250deg 360deg);
  -webkit-mask: radial-gradient(farthest-side, transparent calc(100% - 7px), #000 calc(100% - 7px));
  mask: radial-gradient(farthest-side, transparent calc(100% - 7px), #000 calc(100% - 7px));
  /* Spins from the start but only becomes visible once the mark is drawn, so the
     sequence reads logo → ring → name/version. `backwards` holds it at 0 through
     the delay and hands it back to its own (implicit) opacity afterwards. */
  animation:
    splash-spin 0.9s linear infinite,
    splash-rise 0.35s ease-out 0.62s backwards;
}

/* The mark draws itself in: each face's outline is traced by a stroke
   (`pathLength="1"` makes the dash maths independent of the real path length),
   then its fill comes up behind it. Everything is stroke-dashoffset / opacity /
   transform on SSR'd markup, so — exactly like the ring — it runs off the first
   painted frame and owes nothing to hydration. The splash never waits for it:
   the whole sequence lands well inside MIN_VISIBLE_MS, and dismissal cuts it
   off whenever the app is ready. */
.splash__logo {
  width: 88px;
  height: 88px;
  filter: drop-shadow(0 2px 6px rgba(0, 0, 0, 0.25));
  /* The stroke sits astride the path edge; without this it is clipped. */
  overflow: visible;
}

.splash__logo-face {
  fill: var(--ui-primary);
  stroke: var(--ui-primary);
  stroke-width: 10;
  stroke-linejoin: round;
  fill-opacity: 0;
  stroke-dasharray: 1;
  stroke-dashoffset: 1;
  animation:
    splash-logo-draw 0.5s cubic-bezier(0.65, 0, 0.35, 1) forwards,
    splash-logo-fill 0.35s ease-out forwards;
}

/* Per-face opacity is the mark's own shading (a lit isometric solid); the
   stagger is what makes it read as drawn rather than simply appearing. */
.splash__logo-face--top {
  opacity: 1;
  animation-delay: 0.05s, 0.5s;
}

.splash__logo-face--left {
  opacity: 0.74;
  animation-delay: 0.17s, 0.62s;
}

.splash__logo-face--right {
  opacity: 0.88;
  animation-delay: 0.29s, 0.74s;
}

.splash__welcome {
  text-align: center;
  line-height: 1.3;
  /* Client-only, so this starts when it mounts (after hydration) rather than at
     first paint — by which point the logo and ring are already up. */
  animation: splash-rise 0.4s ease-out backwards;
}

.splash__welcome-label {
  display: block;
  font-size: 0.8rem;
  color: var(--ui-text-muted);
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
  /* `backwards` (not `forwards`) so it settles back to the 0.6 declared above
     instead of being pinned at whatever the keyframe ends on. */
  animation: splash-rise 0.4s ease-out backwards;
}

@keyframes splash-spin {
  to {
    transform: rotate(360deg);
  }
}

@keyframes splash-logo-draw {
  to {
    stroke-dashoffset: 0;
  }
}

@keyframes splash-logo-fill {
  to {
    fill-opacity: 1;
  }
}

@keyframes splash-rise {
  from {
    opacity: 0;
    transform: translateY(6px);
  }
}

@media (prefers-reduced-motion: reduce) {
  /* Static logo — fully drawn and filled from the first frame. */
  .splash__logo-face {
    fill-opacity: 1;
    stroke-dashoffset: 0;
    animation: none;
  }

  .splash__welcome,
  .splash__version {
    animation: none;
  }

  .splash__ring {
    animation: splash-spin 2.4s linear infinite;
  }

  /* No curtain slide — fall back to a plain fade for both layers. */
  .splash {
    transition: opacity 0.2s ease;
  }

  :root[data-splash-ready] .splash {
    transform: none;
    transition: opacity 0.2s ease, visibility 0s linear 0.2s;
  }

  .app-shell {
    transition: opacity 0.2s ease;
  }
}
</style>
