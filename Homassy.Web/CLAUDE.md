# Homassy.Web – CLAUDE.md

## Overview

Homassy.Web is the **frontend application** of the Homassy platform. It is a **Nuxt 4 / Vue 3** single-page application with PWA support, targeting mobile-first usage. It communicates with `Homassy.API` for business data and directly with **Ory Kratos** for authentication flows (login, registration, verification, recovery, WebAuthn/passkeys).

### Key Architectural Decisions

- **Nuxt 4 + Vue 3 Composition API** – file-based routing, auto-imports, SSR disabled for auth-sensitive pages
- **Ory Kratos** – session-based auth using httpOnly cookies (`ory_kratos_session`); no JWT on the frontend
- **`$api` plugin** – a `$fetch` wrapper (`credentials: 'include'`) for all calls to `Homassy.API`, with automatic 401 → `/auth/login` handling
- **SignalR realtime** – `@microsoft/signalr` client (`useShoppingListSocket`) keeps the open shopping list in sync; writes still go through REST, the server broadcasts changes back
- **Pinia** – global state management (currently single `auth` store)
- **Nuxt UI v4** – component library with custom `mocha` color palette
- **PWA** – `@vite-pwa/nuxt`, auto-update, push notification support via `sw-push.js`
- **i18n** – 3 locales (English, Hungarian, German), browser auto-detection, cookie persistence

---

## Technology Stack

| Category | Package / Version |
|---|---|
| Framework | Nuxt 4.2.2, Vue 3.5.25 |
| Language | TypeScript 5.9.3 |
| UI Components | @nuxt/ui 4.3.0 (Nuxt UI v4) |
| State | @pinia/nuxt 0.11.3 |
| Auth | @ory/client 1.22.23 |
| i18n | @nuxtjs/i18n 10.2.1 |
| PWA | @vite-pwa/nuxt 1.1.0 |
| API proxy | nuxt-api-party 3.4.2 |
| Realtime | @microsoft/signalr 10.0.0 |
| Image | @nuxt/image 2.0.0, browser-image-compression 2.0.2 |
| Icons | @iconify-json/heroicons, @iconify-json/lucide |
| Barcode | vue-qrcode-reader 5.7.3 |
| Cropper | vue-advanced-cropper 2.8.9 |
| Calendar | vue-cal 4.10.2 |
| Maps | maplibre-gl 6.9.0 (lazily imported — see Maps) |
| WebAuthn | @simplewebauthn/browser 13.2.2 |
| Date | @internationalized/date 3.10.1 |
| Linting | @nuxt/eslint 1.12.1, eslint 9.39.2 |
| Node runtime | Node.js 22 (Alpine Docker) |

---

## Project Structure

```
Homassy.Web/
├── app/
│   ├── app.vue                 Root component (SEO meta, locale, layout outlet)
│   ├── app.config.ts           Nuxt UI color config (primary: 'mocha')
│   ├── assets/
│   │   └── css/main.css        Global styles
│   ├── components/             Shared UI components (auto-imported)
│   │   ├── auth/               Auth flow components
│   │   ├── landing/            Public landing page: device frame, rendered app screens, showcase bands
│   │   ├── security/           Security/WebAuthn components
│   │   └── *.vue               Cards, modals, buttons, etc.
│   ├── composables/
│   │   ├── api/                API composables (one per controller)
│   │   │   ├── index.ts        Re-exports all API composables
│   │   │   ├── useProductsApi.ts
│   │   │   ├── useShoppingListApi.ts
│   │   │   ├── useLocationsApi.ts
│   │   │   ├── useFamilyApi.ts
│   │   │   ├── useUserApi.ts
│   │   │   ├── useSelectValueApi.ts
│   │   │   ├── useSearchApi.ts     Global search: the command palette's one endpoint
│   │   │   ├── useOpenFoodFactsApi.ts
│   │   │   ├── useProgressApi.ts
│   │   │   ├── useAutomationApi.ts
│   │   │   ├── useCalendarApi.ts
│   │   │   ├── useNotificationsApi.ts  Notification centre: list, unread count, read, dismiss
│   │   │   ├── useStatisticsApi.ts
│   │   │   ├── useHealthApi.ts
│   │   │   ├── useErrorCodesApi.ts
│   │   │   └── useVersionApi.ts
│   │   ├── index.ts            Re-exports all composables
│   │   ├── useApiClient.ts     Wrapper: $api + toast error handling
│   │   ├── useBreakpoint.ts    Reactive "is this a desktop-width window" (the lg crossing)
│   │   ├── useCommandPalette.ts  Palette state: open flag, query, results, recent searches
│   │   ├── useSearchHandoff.ts   Receives ?search= / ?select= on a list page
│   │   ├── useScrollReveal.ts  One-shot reveal-on-scroll, for the landing page
│   │   ├── useSpeechRecognition.ts  Web Speech API wrapper: support, transcript, failures
│   │   ├── useVoiceItemMatching.ts  A dictated name → product, category, or free text
│   │   ├── useKratos.ts        Ory Kratos FrontendApi flows
│   │   ├── useBarcodeScanner.ts
│   │   ├── useCameraAvailability.ts
│   │   ├── useDateFormat.ts
│   │   ├── useDeviceDetection.ts
│   │   ├── useDragReorder.ts   The drag gesture behind manual ordering (lift, auto-scroll, drop)
│   │   ├── useEnumLabel.ts
│   │   ├── useEventBus.ts
│   │   ├── useExpirationStatus.ts  The expiration ramp: date → level → tokens (see below)
│   │   ├── useExpirationCheck.ts
│   │   ├── useFabActions.ts    Shared state for the layout floating action button
│   │   ├── useGeocoding.ts     Address → coordinates via Nominatim (OpenStreetMap, keyless)
│   │   ├── useGeolocation.ts   Browser Geolocation wrapper (permission + getCurrentPosition + watch)
│   │   ├── useHaptics.ts       The app's vibration vocabulary + the user's on/off switch
│   │   ├── useImageCrop.ts
│   │   ├── useInputDateLocale.ts
│   │   ├── useAppBadge.ts      The expiring-items count on the app icon + the tab title
│   │   ├── useDeepLinkAction.ts  `?action=` → a one-shot call on the page that owns it
│   │   ├── useMediaUrl.ts      API-relative media path → loadable URL (see Images below)
│   │   ├── useNotificationCenter.ts  The inbox, its unread count and the drawer's open flag
│   │   ├── useOnboardingTour.ts  Drives the first-run spotlight tour
│   │   ├── usePullToRefresh.ts
│   │   ├── usePushNotifications.ts
│   │   ├── useReorderableList.ts  Manual order + optimistic reorder for a list of rows
│   │   ├── useShoppingMode.ts   Which lists are mid-shop (module-scoped, not persisted)
│   │   ├── useWakeLock.ts       Screen Wake Lock, re-acquired after backgrounding
│   │   ├── useShoppingListSocket.ts  SignalR realtime client for shopping lists
│   │   ├── useInventorySocket.ts  SignalR realtime client for the Készletek (inventory) grid
│   │   ├── useShareTarget.ts   Reads (and clears) what another app shared into Homassy
│   │   ├── useSwipeActions.ts  Swipe-to-action gestures on cards (left/right + threshold commit)
│   │   └── useWebAuthn.ts
│   ├── layouts/
│   │   ├── auth.vue            Authenticated layout – bottom nav bar
│   │   └── public.vue          Public layout – no nav
│   ├── middleware/
│   │   └── auth.ts             Route guard: validates Kratos session client-side
│   ├── pages/
│   │   ├── index.vue           Root redirect
│   │   ├── activity.vue        Activity feed
│   │   ├── calendar.vue        Monthly calendar of expirations & shopping deadlines
│   │   ├── share.vue           Web Share Target landing page (see PWA below)
│   │   ├── auth/
│   │   │   ├── login.vue
│   │   │   ├── register.vue
│   │   │   ├── verify.vue
│   │   │   └── recovery.vue
│   │   ├── products/
│   │   │   ├── index.vue
│   │   │   ├── add-product.vue
│   │   │   └── [publicId].vue  Product detail (dynamic route)
│   │   ├── profile/
│   │   │   ├── index.vue
│   │   │   ├── family.vue
│   │   │   ├── create-family.vue
│   │   │   ├── join-family.vue
│   │   │   ├── notifications.vue
│   │   │   ├── products.vue
│   │   │   ├── security.vue
│   │   │   ├── settings.vue
│   │   │   ├── shopping-locations.vue
│   │   │   ├── storage-locations.vue
│   │   │   └── automation/
│   │   │       ├── index.vue          Automation rules list (filterable)
│   │   │       ├── create.vue         Create automation rule (stepper wizard)
│   │   │       └── [publicId].vue     Automation rule detail / edit / delete
│   │   └── shopping-lists/
│   │       ├── index.vue
│   │       └── add-product.vue
│   ├── plugins/
│   │   ├── api.ts              Provides $api ($fetch wrapper with 401 → /auth/login)
│   │   ├── app-badge.client.ts Installs the document-title prefix (see useAppBadge)
│   │   ├── auth.ts             On startup: loadFromCookies + setupVisibilityListener
│   │   ├── i18n.ts
│   │   ├── qrcode-reader.client.ts  Client-only QR code plugin
│   │   └── version-check.client.ts  Client-only version check
│   ├── stores/
│   │   └── auth.ts             Pinia: session, user, isAuthenticated, initialize()
│   ├── types/                  TypeScript type definitions
│   │   ├── auth.ts
│   │   ├── common.ts
│   │   ├── product.ts
│   │   ├── shoppingList.ts
│   │   ├── location.ts
│   │   ├── family.ts
│   │   ├── user.ts
│   │   ├── enums.ts
│   │   └── ...
│   └── utils/
│       ├── enumMappers.ts
│       ├── errorCodes.ts
│       ├── geoUtils.ts           Haversine distanceMeters + NEARBY_RADIUS_METERS
│       ├── manualOrder.ts        The pure ordering rules behind drag-and-drop reordering
│       ├── voiceItemParser.ts    Dictated sentence → quantity + unit + name, per locale
│       └── stringUtils.ts
├── i18n/
│   └── locales/
│       ├── en.json             English translations
│       ├── hu.json             Hungarian translations
│       └── de.json             German translations
├── public/                     Static assets (icons, favicons, service-worker scripts)
│   ├── shortcuts/              Manifest app-shortcut icons (generated — see scripts/)
│   ├── sw-push.js              Push display + app badge + the "a push arrived" page message
│   └── sw-share.js             Answers the share target's POST navigation
├── Dockerfile                  Multi-stage: development / build / production
├── nuxt.config.ts
├── i18n.config.ts
├── eslint.config.mjs
└── package.json
```

---

## Authentication

### Architecture

Authentication is **Ory Kratos session-based**. Sessions are stored as an httpOnly cookie (`ory_kratos_session`) set by Kratos. The frontend never handles raw passwords or JWTs.

```
Browser ──cookie──► Kratos (public, :4433)
Browser ──cookie──► Homassy.API (validates session via Kratos Admin API)
```

### `useKratos` Composable

Wraps `@ory/client` `FrontendApi`. Handles all Kratos UI flows:

| Method | Description |
|---|---|
| `getSession()` | Fetch current session (returns `null` on 401) |
| `initLoginFlow()` / `submitLoginFlow()` | Password login |
| `initRegistrationFlow()` / `submitRegistrationFlow()` | Registration |
| `initVerificationFlow()` / `submitVerificationFlow()` | Email verification |
| `initRecoveryFlow()` / `submitRecoveryFlow()` | Account recovery |
| `initSettingsFlow()` / `submitSettingsFlow()` | Profile/password settings |
| `initWebAuthnFlow()` / `submitWebAuthn*()` | Passkey management |
| `logout()` | Kratos logout (invalidates server session) |

All calls use `withCredentials: true` (cookie forwarding).

### `useAuthStore` (Pinia)

```typescript
// State
session: Session | null
user: UserInfo | null           // Mapped from Kratos identity traits
isLoading: boolean
initialized: boolean

// Getters
isAuthenticated                 // session.active === true && user !== null
kratosIdentityId               // session.identity.id
sessionExpiresAt               // session.expires_at

// Actions
initialize()                   // Fetch Kratos session on first load
loadFromCookies()              // Called by auth plugin on startup
refreshSession()               // Re-fetch session (called on visibility change)
clearAuthData()                // Clears local state (does NOT call Kratos logout)
isSessionValid()               // Checks expiry
syncLanguageLocale(language)   // Syncs homassy_locale cookie
```

### Auth Middleware (`app/middleware/auth.ts`)

Applied to all pages using the `auth` layout. Logic:
- **SSR**: always allows render (cookies are httpOnly, not accessible server-side)
- **Client-side**: calls `authStore.initialize()` if not yet done, then checks `session.active` and expiry; redirects to `/auth/login` if invalid

### Auth Plugin (`app/plugins/auth.ts`)

Runs on app startup:
1. `authStore.loadFromCookies()` — attempts to restore session from existing cookies
2. `authStore.setupVisibilityListener()` — refreshes session when tab becomes visible again (client only)

---

## API Communication

### `$api` Plugin (`app/plugins/api.ts`)

A `$fetch` wrapper registered as `$api`. Sends `credentials: 'include'` (Kratos session cookie). On `401` response:
1. Clears local auth state
2. Redirects to `/auth/login`

### `useApiClient` Composable

Wraps `$api` with automatic toast notifications:

```typescript
const { request } = useApiClient()

const result = await request<Product[]>('/api/v1.0/product', {
  method: 'GET',
  showErrorToast: true,          // default: true
  showSuccessToast: false,       // default: false
  successMessage: 'Saved!'       // used when showSuccessToast: true
})
// result: ApiResponse<Product[]>
```

For `FormData` bodies, `Content-Type` is NOT set manually (browser sets it with boundary automatically).

### API Composables (`app/composables/api/`)

One composable per API controller. All use `useApiClient` internally:

| Composable | Endpoints |
|---|---|
| `useProductsApi` | Products CRUD, expiration counts |
| `useShoppingListApi` | Shopping lists and items, deadline counts |
| `useLocationsApi` | Storage + shopping locations |
| `useFamilyApi` | Family management |
| `useUserApi` | User profile, preferences |
| `useSelectValueApi` | Select value lists |
| `useOpenFoodFactsApi` | Barcode product lookup |
| `useProgressApi` | Inventory progress |
| `useAutomationApi` | Automation rules CRUD, enable/disable, manual execution |
| `useCalendarApi` | Calendar events (expirations & deadlines) for a date range |
| `useNotificationsApi` | Notification centre: paged inbox, unread count, read/dismiss |
| `useStatisticsApi` | Global platform statistics |
| `useHealthApi` | API health check |
| `useErrorCodesApi` | Error code descriptions |
| `useVersionApi` | API version info |

### Who reports a failed request

A failure is reported **once**, and which side does it depends on whether the API answered at all:

| Case | `useApiClient.request` | Client toast | Caller |
|---|---|---|---|
| HTTP error status | returns a failure-shaped `ApiResponse` | shows the only toast | handles it on `else`, **does not toast** |
| Request never reached the API | rethrows | silent | its `catch` shows the only toast |

So do not toast from an `else` branch after an API call. Pass the message you would have shown as `errorMessage` instead, and the client puts it in the single toast:

```ts
const res = await createProduct(payload, { errorMessage: t('pages.addProduct.form.saveFailed') })
if (res.success && res.data) { ... }
else formRef.value?.setErrors(toFormErrors(res.validationErrors, Object.keys(form.value)))
```

Toast description precedence: the response's localized `errorCodes` → `errorMessage` → a generic message. `showErrorToast: false` opts out entirely, for callers that own their own reporting (`useFamilyApi`, `useProgressApi`, the push and test-notification calls). A 401 never toasts — the `$api` plugin is already redirecting to the login page.

### Two error body shapes

The API answers its own errors with the `ApiResponse` envelope and a localized `errorCodes` array. **Model validation is different:** `[Required]`, `[StringLength]`, `[RegularExpression]` and JSON deserialization failures never reach `GlobalExceptionMiddleware`, so MVC answers them itself with `ValidationProblemDetails` — no `errorCodes`, only an `errors` map whose keys come in two spellings:

```
"Name"        a DataAnnotations failure, keyed by the PascalCase CLR property
"$.category"  a JSON deserialization failure, keyed by a JSON path
"request"     emitted alongside the latter, and not a form field at all
```

`useApiClient` normalizes those onto camelCase field names in `ApiResponse.validationErrors` (a client-side field, not something the server sends) and logs the server's raw English text with `console.warn`. `useApiFormErrors().toFormErrors` maps them onto `UForm.setErrors`, keeping only the keys the form has a field for and using the localized `common.invalidValue` — the server's own prose is English and the UI runs in three languages.

The spellings are pinned by `ProductControllerTests.CreateProduct_InvalidRequest_ReturnsValidationProblemDetailsKeyedByField`; the normalization depends on them, so change that test if the API's behaviour ever moves.

### Realtime (SignalR)

`useShoppingListSocket` maintains a single app-wide SignalR connection to `${apiBase}/hubs/shopping-list` (`withCredentials: true`, automatic reconnect, client-only).

- `joinList(publicId, showPurchased)` joins the list's SignalR group and returns the current snapshot (`DetailedShoppingListInfo`) — no separate REST fetch needed; falls back to REST when the socket is down
- Server events: `ItemUpserted`, `ItemDeleted`, `ListUpdated`, `ListDeleted` — the shopping list page patches its local state from these
- All writes still go through `useShoppingListApi` (REST); the API broadcasts the change back to the group, so the acting client's own changes are also reflected via the socket

`useInventorySocket` does the same for the Készletek (inventory) grid, connecting to `${apiBase}/hubs/inventory`.

- Groups are identity-derived server-side (per-family + per-user), so there is no per-resource join — `joinInventory()` just returns the light `InventoryGridProductInfo[]` snapshot (only the fields the cards need); falls back to REST on SSR / socket down
- Server events: `InventoryUpserted` / `InventoryDeleted` (item-level, carry the parent product), `ProductUpdated` / `ProductFavoriteChanged` / `ProductDeleted` (product-level). The grid (`products/index.vue`) patches `allProducts` in place — inserting a card on the first in-scope item and removing it when the last one is gone. The product detail page (`products/[publicId].vue`) treats a matching event as a trigger to refetch that single product (the light event lacks storage/purchase/consumption detail)
- Writes still go through `useProductsApi` (REST); the API broadcasts the change back, and automation-driven changes from `Homassy.Notifications` are relayed via the API's internal broadcast endpoint, so all clients (including automations) stay in sync

### Swipe Actions

`useSwipeActions(el, options)` adds swipe gestures to an element (used by `ShoppingListItemCard`): swipe left/right past a threshold (`max(40% width, 56px)`) commits `onSwipeLeft`/`onSwipeRight`. Pointer-event based with axis lock (vertical drags fall through to native scroll / pull-to-refresh), damped overshoot, haptic tick at the threshold, and a `suppressClick` flag consumers must check in their click handler. Requires `touch-action: pan-y` on the wrapper and works with touch, pen, and mouse.

---

## Images from the API

Uploaded pictures are **served**, not embedded. The API's DTOs carry a path (`profilePictureUrl`),
never base64 — base64 is ~33% larger than the bytes, cannot be cached by the browser or the
service worker, and was re-downloaded with every payload that mentioned its user.

| Piece | Role |
|---|---|
| `app/composables/useMediaUrl.ts` | `mediaUrl(path)` — prefixes `runtimeConfig.public.apiBase`; passes absolute / `data:` / `blob:` URLs through |
| `app/components/UserAvatar.vue` | the app's one user avatar: picture, or a deterministic gradient + initials |
| `app/components/ProductImage.vue` | the app's one product picture: fills its parent, falls back to a category icon |
| `app/utils/productCategoryVisuals.ts` | icon + hue per `ProductCategoryGroup`, for that fallback (hand-written; `productCategoryGroups.ts` next to it is generated) |
| `nuxt.config.ts` → `image.providers.none` | the passthrough `@nuxt/image` provider these components render through |
| `nuxt.config.ts` → `pwa.workbox.runtimeCaching` `remote-images` | CacheFirst for the image endpoints |

Easy to get wrong:

- **`crossorigin="use-credentials"` is required.** The endpoints are behind `[Authorize]`, and in development the API is a different origin — without it the browser omits the Kratos session cookie and every avatar 401s. In production (same origin behind the reverse proxy) the attribute is a no-op.
- **`provider="none"`, never the default `ipx`.** IPX would fetch the image from the Nitro server, which has no session cookie. The API already serves a purpose-sized rendition per `?size=`, so there is nothing for IPX to do.
- **Never append a cache-buster.** The URL already ends in `?v=<content hash>`, so a changed picture is a new URL; adding `?t=Date.now()` would defeat both the browser cache and the `remote-images` service-worker cache.
- **The placeholder renders underneath the image, always.** It is both the loading state and the permanent fallback, which is what keeps a list from flashing empty circles. Its colour is generated from the name (there is no theme token for "a colour per user") at a lightness that works in both themes.
- Avatars are **no longer a Kratos trait**. `traitsToUserInfo` does not set one; `fetchUserFromBackend()` is what fills `profilePictureUrl` in.
- `ProductImage` renders `object-contain`, matching the server's bounded (not cropped) product thumbnail, so nothing is clipped off a tall bottle. `UserAvatar` is the cropped-square case.
- The category placeholder's two theme variants are **CSS**, not a computed value: the colour mode is unknown during SSR, so branching on it in script is a hydration mismatch. The hue goes in as a `--cat-hue` custom property and light/dark lightness comes from a `dark:` variant.
- `ProductFormDrawer` keeps a single `imagePreview` src that is the stored image's URL most of the time and a `data:` URI in the moment between cropping and the upload finishing — `useMediaUrl` passes the latter through untouched.
- **The family picture follows the same rules** (`FamilyDetailsResponse.familyPictureUrl`): set from `FamilyDrawer`, where tapping the circle picks one file (`accept="image/*"`, camera and gallery in one control), crops it square in `ImageCropper` — the crop screen *is* the preview — compresses it client-side and posts base64. The drawer holds the cropped `data:` URL as an optimistic preview until the next fetch, for the same reason `ProductFormDrawer` does. `FamilyChatBubble` renders the same URL, so the bubble and the drawer cannot show different pictures.

---

## Haptics

`useHaptics()` is the **only** place the app is allowed to call `navigator.vibrate`. Call sites pick a *meaning* from a fixed vocabulary, never a duration, so the whole app can be retuned from one table:

| Pattern | Means | Used by |
|---|---|---|
| `tap` | light acknowledgement | FAB press, pull-to-refresh release, un-purchase |
| `select` | a threshold or detent was crossed | swipe threshold, drawer snap, pull-to-refresh arming, FAB chooser pick |
| `impact` | a gesture committed | swipe action firing |
| `success` | an operation completed | purchase confirmed, barcode decoded |
| `warning` | something destructive happened | every delete confirm |
| `error` | an operation failed | scan found nothing, camera error |

It feature-detects the Vibration API (**absent on iOS Safari**, where every call is a silent no-op and the setting row is hidden) and honours a device-local switch in the profile Preferences group, persisted in `localStorage` under `homassy_haptics`. With nothing stored yet the default follows `prefers-reduced-motion`: `reduce` starts it off.

All of its state is module-scoped, so `useHaptics()` is safe to call from plain functions as well as from `setup`.

---

## Self-updating relative timestamps

`useRelativeTime(date, { thresholdMs })` (in `useDateFormat.ts`) and the `RelativeTime` component that wraps it replace the four hand-rolled "x minutes ago" helpers the app used to carry. Built on `Intl.RelativeTimeFormat`, so all three locales get grammatical phrasing — including "yesterday"/"tegnap"/"gestern" — and future dates read as naturally as past ones.

- **One ticker for the whole app**, never one per component: each caller registers the timestamp it cares about, and a single `setTimeout` chain re-reads the clock every 30 s while anything on screen is under an hour old, every minute after that, and **stops entirely** once nothing left can change.
- It holds no timer while `document.hidden`, and re-reads the clock on `visibilitychange` and `pageshow`, so returning to a backgrounded PWA never shows a value frozen at the moment it was left.
- Past `thresholdMs` (default 7 days) the label gives way to the absolute date; the full date **and time** is always on the `title` attribute.
- The visible label renders inside `<ClientOnly>` with the absolute date as the fallback. A server-rendered relative phrase would disagree with the client's the moment a minute ticked over between the two.

Used by `ActivityCard`, `ProductHistoryList`, `FamilyDrawer`, `DataExternalCalendarCard` and the calendar day list. The calendar passes `absolute-format="time"` with a 24 h threshold: the day panel header already names the date, so "3 days ago" would say less there than "14:32".

---

## Grouped grid + fast-scroll index (`pages/products/index.vue`)

The inventory grid can group its cards under sticky section headers, with a right-edge index rail
for jumping between them. `SectionIndexRail` owns the rail; the page owns the grouping.

- **Group by** is a filter-drawer chip group (`none` / `name` / `category`), persisted with the
  rest of the filters in the `productsFilters` localStorage entry. `none` is the flat,
  urgency-ordered list the page has always shown.
- **Storage location is deliberately not offered**, though the issue that asked for this listed it:
  it is not in the grid's payload (`InventoryGridItemInfo` omits it on purpose) and a product with
  items in several locations has no single one to group under. Category grouping needed one field
  added — `InventoryGridProductInfo.Category` — and maps it client-side onto a
  `ProductCategoryGroup` the API neither knows nor stores.
- **Grouping is computed over `filteredProducts`, never the rendered slice.** The rail lists every
  section, so it has to be able to point at a group the `IntersectionObserver` paging has not
  reached; a rail built from what happens to be on screen would grow as you scrolled. `jumpToSection`
  therefore raises `currentPage` far enough to include the target before scrolling to it — which
  does mean jumping to the last group of a very long list renders everything above it. That is the
  cost of keeping the incremental renderer instead of virtualizing.
- Section headers count the **whole** group, not the rendered part of it, so a header does not
  count up as you scroll into it.
- Each section is its own `AnimatedList`. A header inside one would join the cards' FLIP animation
  — the same reason the shopping list splits its "buy here" section out.
- The scroll is a `window.scrollTo` with the header height subtracted, not `scrollIntoView`: the
  app header is fixed, so a section scrolled to the top of the viewport would sit under it.
- `SectionIndexRail` knows nothing about products. It reports the picked key and leaves revealing
  and scrolling to the page. It resolves the tick under the pointer from its own measured geometry
  (not a fixed row height), ticks `useHaptics().select()` once per detent crossed rather than per
  pointer move, and hides itself below `minSections` — the page passes the same constant it uses to
  widen the content gutter, so the rail overlays the gutter rather than a card.

**Not done here:** the same treatment for the shopping list. Aisle order now exists (see
*Drag-and-drop reordering* below), but as an ordering mode rather than a grouping — there are no
section headers to index, so there is nothing for the rail to point at.

---

## The expiration ramp

`app/composables/useExpirationStatus.ts` is the single mapping from "when does this expire" to a
severity level, plus one table of what each level looks like. Every surface that says anything
about expiry reads from it, so they cannot drift apart:

| Level | When | Tone |
|---|---|---|
| `none` | no date at all | dimmed, calendar icon |
| `ok` | more than 14 days | dimmed, calendar icon |
| `soon` | within 14 days | `warning`, clock |
| `critical` | within 3 days | `error`, alarm clock |
| `expired` | past | `error` (stronger surface), alert circle |

- Colours are Nuxt UI **semantic tokens** (`text-warning`, `border-error/50`, `bg-error/10`,
  `text-dimmed`), not palette shades — both themes work with no `dark:` variant per class, and
  `StockRing` can stroke with `currentColor` and inherit whatever the caller set.
- Consumers: `InventoryItemRow` (surface, border, icon), `DetailedProductCard` (border, via
  `worstExpirationLevel` across the product's items), `ExpirationChip` (label + colour), and the
  nav badge in `layouts/auth.vue`. Each of those used to carry its own `red-400` / `amber-600` /
  `primary-400` literals, and the nav badge was permanently red whether milk went off today or in
  a fortnight — which is why `GET /product/inventory/expiration-count` now also returns
  `expiredCount`.
- `ExpirationChip` phrases the label through `useRelativeTime`, so all three locales get
  grammatical output and "tomorrow" rather than "in 1 day". Past the 14-day window it gives way to
  the absolute date; the exact date and time is always on the `title`.
- **Known mismatch:** the client ramp's window is 14 days (matching the long-standing
  `isExpiringWithinTwoWeeks`), while the API's count uses `ProductSettings:ExpiringSoonThresholdDays`.
  With those set differently, a card can be amber for an item the badge does not count.

### `StockRing`

A circular gauge drawn around whatever is slotted into it, showing how much of an item is left
**against what was bought** (`PurchaseInfo.OriginalQuantity`).

- **No reference amount, no gauge.** An item with no purchase info has no denominator, and rather
  than picking one the ring becomes a flat badge — the slot reads the same, but nothing on screen
  claims a proportion. On a grid card the denominator is only used when *every* item in the unit
  group has one, for the same reason.
- `InventoryGridItemInfo` carries `originalQuantity` for this. It is the one piece of purchase
  info in that otherwise deliberately light payload, and the realtime broadcast paths fill it too —
  without that the ring would vanish from a card every time an automation touched it.
- The fill animates in on mount (single frame, plus a timeout because `requestAnimationFrame` does
  not fire in a hidden tab); under `prefers-reduced-motion` it is simply there.

---

## Image lightbox (`ImageLightbox`)

One full-screen viewer for every image the app shows large. It replaced two ad-hoc overlays — the
inventory drawer and the shopping-list card each kept their own `isImageOverlayOpen`, their own
markup, and neither had zoom, pan or a dismiss gesture.

```vue
<ImageLightbox v-model:open="isOpen" :images="images" :origin="thumbnailEl" />
```

- **`images` is an array** of `{ thumb, full, alt }`, even where there is one. Paging and the dots
  are already there, so a product gallery is a change at the call site and nowhere else.
- **`origin` is the element the viewer was opened from.** Its box is where the shared-element
  transition starts and where it returns to. `ProductInfoPanel` therefore emits `image-click` with
  its thumbnail element rather than emitting nothing. Without an origin the lightbox just fades.
- Only the visible page's `full` variant is rendered, so a gallery loads one large image. The
  `thumb` sits underneath as the placeholder — it was just on screen, so it is in the browser
  cache — and is what the open transition appears to grow.

**Gesture arbitration** (all pointer events, so touch, pen and mouse behave the same):

| Input | Does |
|---|---|
| two pointers | pinch zoom (always wins — no one-finger gesture a second finger could continue) |
| one pointer, zoomed in | pan, clamped to the image's own edges |
| one pointer, at 1x | axis-locked after 8px: down dismisses, sideways pages |
| double tap | toggles fit ↔ 2.5x, centred on the tap |
| wheel / trackpad pinch | zoom about the cursor |
| `Escape` / `←` / `→` | close, previous, next |

**Easy to regress, all deliberate:**

- The open transition needs **two animation frames** between setting the origin transform and
  releasing it — one frame gets coalesced with the style that set it and no transition runs. There
  is a 120 ms timeout alongside, because `requestAnimationFrame` does not fire in a hidden tab and
  without it a lightbox opened in one stays pinned at zero opacity.
- Focus goes to the close button found **through the DOM**, not through a component ref: a Nuxt UI
  `UButton`'s `$el` is not reliably an element (its root is a `Primitive`, so it can be a comment
  anchor), and calling `.querySelector` on one throws — which aborted the whole open sequence.
- `setPointerCapture` is wrapped in a `try`: it throws once the pointer is no longer active, and a
  throw there abandons the gesture.
- A dismiss flick closes **without** the shared-element transition: the image is already on its way
  off screen, and pulling it back to the thumbnail first would look like a bounce.
- The backdrop's opacity ramps with the drag; the blur is skipped under `prefers-reduced-motion`
  along with the transitions, but the opacity ramp stays — it is the feedback, not decoration.

---

## Drawers (`AppDrawer` + `useDrawerDragToClose`)

### What a drawer becomes at each width (#122)

Below `lg` an `AppDrawer` is always the bottom sheet described in the rest of this section. Above
`lg` its `desktop` prop decides:

| `desktop` | Above `lg` | For |
|---|---|---|
| `panel` (default) | Right-hand side panel, still modal — overlay and focus trap intact | Forms, wizards, filters |
| `detail` | Right-hand side panel with **no** overlay and no scroll lock, so the list it opened from stays readable and clickable | The four overview drawers — this is the master/detail shape |
| `sheet` | Stays a bottom sheet | The rare drawer that is the right shape at any width |

One prop rather than a second component: a desktop-only drawer would be a second copy of this
chrome to keep in step with this one. A side panel has no drag gesture and no snap points — the
composable's whole vocabulary is vertical, and there is no equivalent worth inventing for a panel
pinned to the right edge of a desktop window with a mouse. Its ✕ is the exit.

### The bottom sheet

`AppDrawer` is the single source of truth for drawer chrome; `useDrawerDragToClose(headerEl, options)` owns every gesture on it. vaul's native dismiss stays off (`dismissible: false`), so this composable and the ✕ button are the only exits.

Listeners live on the **content** element (`[data-slot="content"]`), not the header, because a drag may legitimately start in the body — but only once the sheet is below its top snap. The composable also reaches for `[data-slot="overlay"]`, `[data-slot="handle"]` and `[data-slot="body"]`, all of which Nuxt UI's `UDrawer` stamps for it.

**Two modes:**

- **Plain (the default, and what almost every drawer uses).** Drag the header down; release past `max(25% height, 120px)` and it slides out and closes, otherwise it snaps back. Displacement decides — deliberately no velocity rule here, because flick-to-close on ~30 existing drawers would be a surprise.
- **Snapping (`:snap-points="[0.5, 1]"`).** Only the four overview drawers (inventory, shopping list, shopping location, storage location). A snap point is the fraction of the sheet's own height left visible, so `0.5` on a 94dvh sheet shows ~47dvh. The sheet still *opens* full height; the lower points are places a drag can rest instead of closing.

Snapping specifics:

- Release behaviour is velocity-aware: a flick (≥ 0.5 px/ms, having moved ≥ 24px) moves **one snap in the direction thrown, counted from where the gesture began** — so a hard throw down from full lands on the half snap and takes a second throw to dismiss. Anything gentler settles on the nearest snap. Either way, a sheet dragged clean past the lowest snap closes; velocity cannot pull back a sheet that is already most of the way out.
- Below the top snap the body gets `overflow-y: hidden` + `touch-action: none`, so a vertical drag anywhere moves the sheet; at the top snap the body scrolls again and only the header drags. That is the whole of the gesture arbitration — there is no "drag the sheet when the body is scrolled to 0" rule, which would need non-passive listeners.
- The backdrop's opacity and blur interpolate with the drag rather than toggling, and the handle swells and cants over while releasing would land the sheet somewhere new. Each detent crossed ticks `useHaptics().select()`.
- Under `prefers-reduced-motion` the settle is instant and the blur ramp is skipped (the opacity ramp stays — it is information, not decoration).

**Footer-less sheets only.** A footer is pinned to the bottom of the content, so below the top snap it is dragged off-screen with it.

---

## Layouts

### `auth.vue` (authenticated)

Used for all protected pages. Below `lg` it features a **fixed bottom navigation bar** with:
- Home (Products)
- Shopping Lists
- Activity
- Profile

The bottom nav shows a **red badge** for expiring products (from `getExpirationCount()`) and overdue shopping list items (from `getDeadlineCount()`). Counts are refreshed on mount and via `useEventBus`.

The expiring-items count is also handed to `useAppBadge()` (see below), so the nav badge, the
browser-tab title and the installed app's icon all show the same number. The layout is also where
the first-run tour starts from (`maybeAutoStart()`) and where `OnboardingSpotlight` is mounted —
it is the one component that outlives the tour's own navigation.

### Above `lg`: the sidebar (#122)

The bottom bar is `lg:hidden`, and a persistent left sidebar takes over — the same items, the
same badges, the same avatar entry. `UMain` swaps `pb-32` for `lg:ml-64`; a margin, not padding,
because the pages have horizontal padding of their own.

The FAB becomes a primary button at the top of the sidebar (`NavSidebarAction`), reading the
same `useFabActions` registration: one action runs on click, several open the same chooser as a
menu. A page therefore still declares what "add" means exactly once, and neither surface can
offer something the other does not.

The navigation now exists **twice**, one copy always `display: none`. That is why
`useOnboardingTour.findTarget` returns the first *visible* element carrying a `data-tour` marker
rather than the first one in the DOM: both copies carry the same markers, and a zero rect is a
hole nobody can see.

### `public.vue`

Used for `/auth/*` pages and the landing page. Minimal layout without navigation.

---

## Internationalization (i18n)

- **Locales**: English (`en`), Hungarian (`hu`), German (`de`)
- **Strategy**: `no_prefix` — locale is NOT in the URL
- **Detection**: browser language → `homassy_locale` cookie → fallback `en`
- **Lazy loading**: locale files loaded on demand
- **Location**: `i18n/locales/{en,hu,de}.json`
- **Config**: `i18n.config.ts` → `legacy: false` (Composition API mode)

Language setting from the user's profile (`UserInfo.language`) is synced to the `homassy_locale` cookie on login via `authStore.syncLanguageLocale()`.

---

## PWA

- **Auto-update** on new deployments
- **Manifest**: standalone display, `#c9b8a0` theme color, `#ffffff` background color
- **Service Worker**: `workbox`-powered
  - Pages: `NetworkFirst`, 1-day cache
  - Static assets: `CacheFirst`, 30-day cache
  - Push notifications: `/sw-push.js` (imported into SW)
  - Share target: `/sw-share.js` (imported into SW)

### App shortcuts

Four `shortcuts` entries in the manifest — Shopping list, Add item, Scan barcode, Calendar. The
two that are actions rather than destinations carry `?action=`, and `useDeepLinkAction` turns that
into the drawer/FAB action on arrival:

```ts
useDeepLinkAction({
  add: () => { isAddInventoryOpen.value = true },
  scan: () => openScanner()
})
```

- **It strips the parameter** (`router.replace`) before running the handler. A shortcut is an
  instruction, not page state: without this, navigating back to the page or pulling to refresh
  reopens the drawer.
- **An unknown action is ignored** and still stripped. Shortcuts live in the manifest of an
  *already installed* app, so an old install can ask for an action a new build has renamed —
  landing on the right page with nothing open beats an error.
- **The shopping-list arrival parks its intent** when no list is selected yet and fires it from a
  watcher once `loadShoppingLists()` picks one. Otherwise a shortcut into a cold start races the
  list fetch and silently does nothing.
- Icons are generated by `scripts/generate-shortcut-icons.mjs` from the same Lucide glyphs the UI
  uses, at 96×96 (what Android's launcher asks for) plus 192×192. The PNGs are committed; the
  script is the record of where they came from. None of the four paths falls under
  `navigateFallbackDenylist`.

### Share target (`/share`)

`share_target` is `POST` + `multipart/form-data`, because that is the only enctype that can carry
a file — and a POST navigation cannot be answered by the page. So `public/sw-share.js` intercepts
it, stashes the payload in a cache of its own, and `303`s to the plain `/share` route.

| Piece | Role |
|---|---|
| `public/sw-share.js` | the `fetch` handler; one payload at a time, 12 MB cap per file |
| `app/composables/useShareTarget.ts` | reads it — **destructively** — and materialises the files |
| `app/pages/share.vue` | the landing page: shows what arrived, offers where it should go |
| `app/utils/shareText.ts` | `shareNameFrom` (share → a name field) and `safeReturnTo` |

- **The cache, not the URL.** A file has no query-string form, and shared text is the user's own
  content with no business in a history entry.
- **The read is destructive.** A share is a one-time intent; a payload that survived would replay
  someone's photo into a fresh form on the next visit. A payload older than 10 minutes is dropped
  unread — the cache is not a durable inbox.
- **`/share` keeps the product case and hands off the shopping-list case** to
  `/shopping-lists?action=add-custom`, which already owns list selection, the add-item wizard and
  the realtime group. The product case cannot be handed over: a shared image is a `File`, which
  cannot travel through a navigation. The name rides in a module-scoped handoff ref rather than
  the query string.
- `ProductFormDrawer` therefore takes a `pendingImage` and uploads it right after `createProduct`
  succeeds — uploading needs a product id, which is also why its image *controls* stay edit-only
  while the thumbnail now previews what is waiting.

### Deep links through the auth gate

The auth middleware carries `to.fullPath` over as `return_to`. The login page had always honoured
that parameter and nothing ever sent it, so every unauthenticated launch threw the intended route
away — fine for a bookmark, fatal for a shortcut or a share, which *is* an unauthenticated launch.
`safeReturnTo` validates it (one leading slash, never two, never back into `/auth/`), because a
parameter that is now written on every gated navigation is reachable by anyone who can hand the
user a login link.

---

## The app-icon badge and the tab title (`useAppBadge`)

One number on the two surfaces that live *outside* the running page — and that number is a **sum of
sources the user chooses between**: overdue shopping-list items, unread family chat messages, and
expiring products. `layouts/auth.vue` publishes all three (the first two it already fetches for the
nav badges, the third rides on `useFamilyChat`'s unread count), so the icon, the tab title and the
in-app badges cannot disagree about the same number.

**The defaults are overdue items and unread messages, not expirations.** The first two are somebody
waiting on you; a product that expires in ten days is a fact about the cupboard, and a permanent
number on the home screen turns it into a nag — a badge that never reaches zero stops meaning
anything. Each source has its own switch in the profile's Preferences group.

**The switches are device-local** (`localStorage`, key `homassy_badge_sources`), like haptics and the
theme rather than like the notification preferences: the icon badge only exists on a device the app
is installed on, so "badge this phone with the shopping list" is a statement about the phone. The
rows are only rendered where badging exists at all — a switch with no surface to act on is worse
than no switch. Counts are published whether or not they currently count, so flipping a switch on
does not have to wait for the next fetch.

**The two surfaces are gated differently, on purpose:**

- The **title prefix** (`(3) Homassy`) is the browser-tab equivalent of the nav badge, so it
  follows the same rule the nav badge does — always shown. It is only visible to someone who
  already has the app open.
- The **icon badge** persists on the home screen with the app closed, which makes it a
  notification rather than page chrome. It is suppressed entirely for a user who turned push off
  (`pushNotificationsEnabled`): badging someone who declined to be reminded would be exactly the
  reminder they declined, in another place. That gate is separate from the per-source switches —
  one says whether the badge may exist, the others say what it counts.

The title goes through `useHead` from `plugins/app-badge.client.ts`, not from a page or layout — a
`useHead` registered in a component is torn down with it, which would drop the prefix
mid-navigation. Client-only: during SSR the count is always 0 (the fetch is an authenticated
client call), so a server-rendered prefix could only ever be a hydration mismatch.

`sw-push.js` sets the badge too, so the icon is right without the app being opened — but only when
the push payload carries a `badgeCount`. Two senders know one: the weekly summary (the expiring
count it is already about) and a family chat message, whose worker computes the recipient's own
number server-side — unread messages plus shopping-list items due or overdue, the default pair
this badge counts (`Homassy.Notifications/Services/AppBadgeCount.cs`). It cannot see the
device-local switches above, so a device that turned one of those sources off carries a slightly
high number until the app is next opened and the client recomputes. A payload without a
`badgeCount` leaves the badge alone rather than incrementing, which would drift the moment two
devices received the same push.

---

## First-run spotlight tour (`useOnboardingTour` + `OnboardingSpotlight`)

Six steps through the app's own chrome rather than a slideshow: the bottom nav as a whole, the
inventory, the `+` FAB and what it adds, the barcode scanner, shared shopping lists, and the
profile where family and settings live. Targets are found by `data-tour` attributes, so adding a
wrapper `div` or renaming a class does not break the tour.

| Piece | Role |
|---|---|
| `app/utils/onboardingTour.ts` | the step list + the pure geometry (unit-tested) |
| `app/composables/useOnboardingTour.ts` | state, navigation, target resolution, persistence |
| `app/components/OnboardingSpotlight.vue` | the scrim, the cut-out and the tooltip card |
| `data-tour="…"` | `nav-bar`, `nav-calendar`/`nav-products`/`nav-shopping-lists`/`nav-profile`, `fab`, `scanner` |

- **Almost everything it points at is in the persistent bottom nav**, which is on screen wherever
  the user happens to be, so exactly one navigation happens mid-tour: the FAB does not exist on
  the calendar, and the step that explains it moves the user to the page the scanner step needs
  anyway.
- **A step whose target never appears is skipped** rather than dimming the screen around nothing —
  the camera button only exists on a device with a camera. `waitForTarget` polls on animation
  frames (an element has to be *laid out* before its rect is usable) with a `setTimeout` alongside,
  because `requestAnimationFrame` does not fire in a hidden tab.
- **The overlay is mounted in the auth layout, not per page**, because the tour outlives its own
  navigation — and it is teleported to `<body>`, which is the opposite of what `NavFab` needs and
  for the same reason: `UApp` sets `isolation: isolate`, so a body-level overlay paints above
  everything including the nav. Exactly right for a scrim that has to dim the whole app and
  swallow taps.
- **Completion is `UserProfile.OnboardingCompletedAt`**, not a localStorage key, so it follows the
  user to a new phone. It rides back on `GET /auth/me`, a payload the app already fetches at boot.
  `localStorage` (`homassy_onboarding_done`) is kept as a same-device echo: a failed write must not
  mean the tour reopens on the next navigation. Skipping and finishing record the same thing.
- **The flag is written when the tour is *shown*, not when it ends.** Finishing and skipping are
  two of the three ways out of a tour; the third — putting the phone down — is the commonest, and
  it used to write nothing, so the tour restarted from step 1 on every launch forever. What the
  flag gates is auto-start, whose question is "has this user been shown the tour", and that is
  answered when the first card appears. `markSeen()` is idempotent, writes the local echo first,
  and **reverts its optimistic store patch if the request fails** so the end of the tour retries.
  The consequence, accepted deliberately: a tour abandoned at step 2 is not offered again, and
  "Replay the tour" is how it comes back.
- **"Replay the tour"** in the profile's Preferences group starts it again and deliberately leaves
  the flag set — watching it now is not a request to be ambushed by it on the next launch. So
  nothing in the app sends `completed: false` any more; `PUT /User/onboarding` keeps the capability.
- The card prefers to sit below its hole and flips above when there is no room — the everyday case,
  since the nav is at the bottom — and clamps into the viewport rather than hanging off the edge
  when it fits on neither side. Under `prefers-reduced-motion` the transitions are dropped and
  each step simply appears.
- **The cut-out's `clip-path` is one contour with a degenerate bridge, not two rings.** `polygon()`
  draws a single closed path, so listing the viewport's corners followed by the hole's does not
  give an outer ring and an inner one — it gives a shape with a diagonal running from the
  viewport's bottom-left corner to the hole, and even-odd over that dims a *wedge* of the screen
  with the highlighted element outside it. (That shipped, and is what "the tour's lighting is
  inside out" was.) `holeClipPath` therefore runs in along the viewport's left edge at the hole's
  top, round the hole, and back out along the same line: traced twice, the bridge encloses no area
  and is invisible, and the hole is wound the opposite way to the rectangle so `nonzero` and
  `evenodd` agree. The point count is constant whatever the rect, which is what lets the hole
  glide between steps. `tests/unit/onboardingTour.spec.ts` pins it by asserting every edge is
  axis-aligned — a diagonal edge *is* the bug.

---

## Family chat (the bubble and the panel)

The family's conversation, reachable from anywhere without a nav slot or a route: a chat head that
floats over the app (#145) and the panel it opens into (#146).

| Piece | Role |
|---|---|
| `app/components/FamilyChatBubble.vue` | the draggable chat head, mounted in `layouts/auth.vue` |
| `app/composables/useFamilyChatBubble.ts` | its shared flags: dismissed-for-session, panel open, the live anchor rect |
| `app/composables/useOverlayPresence.ts` | "is a drawer or modal open" — answered from the DOM, not a register |
| `app/components/FamilyChatPanel.vue` | bottom sheet on mobile, anchored card on desktop |
| `app/components/FamilyChatStream.vue` | the message list: day separators, paging, scroll behaviour |
| `app/components/FamilyChatMessageGroup.vue` | one sender run — avatar and name once, then their messages |
| `app/components/FamilyChatComposer.vue` | the input row (Enter sends, Shift+Enter newlines) |
| `app/composables/useFamilyChat.ts` | the stream state, optimistic send, paging, delete |
| `app/composables/useFamilyChatSocket.ts` | the `/hubs/family-chat` client |
| `app/utils/familyChat.ts` | the pure grouping rules (unit-tested) |
| `i18n/locales/*.json` → `familyChat` | all three locales |

- **The bubble is teleported to `<body>`**, for the reason `NavFab` is not: `UApp` sets
  `isolation: isolate`, and a floating surface has to paint above the app's own stacking context.
  It is mounted in the layout so it survives navigation, and `ClientOnly` because its position
  comes from `localStorage` and its family from an authenticated fetch — a server-rendered bubble
  could only ever mismatch on hydration.
- **Its gesture follows `useDrawerDragToClose`'s conventions**: `touch-action: none`, a slop
  threshold before a press becomes a drag (so a tap is still a tap), live `transform` while
  dragging, an explicit release easing. On release it snaps to the nearer edge; the position is
  stored as **viewport fractions**, so rotation and resize keep it sensible, and every restore is
  re-clamped rather than trusted.
- **It hides itself for three different reasons**: no family (nothing to open), a drawer or modal
  open — *except its own panel*, which on mobile is a drawer and whose handle the bubble is — and
  dismissed this session. Dismissal is session-scoped with a settings row as the way back, because
  the bubble is how the chat is reached at all.
- **The panel comes out of the bubble, on every screen size.** Opening it snaps the bubble to its
  nearest corner and the panel hangs off that corner — below it in the top half of the screen, above
  it in the bottom half — with `transform-origin` at the bubble's centre so it scales out of the
  circle that was tapped. The anchor is published as the corner the bubble is *going to*, not where
  it is: measuring an element mid-transition would open the panel against the old position and then
  jump it. This replaced a mobile bottom sheet, which brought `AppDrawer`'s drag-to-close, backdrop
  and focus trap for free but always arrived from the bottom edge wherever the chat head was — so
  Esc, the focus trap and tap-outside-to-close are implemented in the panel instead of inherited.
- **The bubble holds the hub group, not the panel.** It joins on mount and leaves on unmount;
  closing the panel no longer leaves the group. Everything the bubble shows about the conversation —
  the watcher count, the typing pulse, a message arriving into the unread badge — is a live event,
  and a client outside the group receives none of them (which is why the typing pulse never fired
  before). Joining is **not** watching: the attention flag that suppresses notifications is still
  only set while the panel is open and visible.
- **The watcher count is the server's set**, never derived locally: the same flag decides whether a
  message notifies, so a locally guessed count could tell one member that another is reading while
  the server is sending that member a push. It is green (`emerald`, not `success` — this app aliases
  `success` to its mocha primary, which is the bubble's own background).
- **Attaching things**: the composer's picker reads the same `SelectValue` lists every other picker
  in the app reads, which is what makes an attached reference valid by construction — the server
  validates against that same list and resolves its own label. Chips render under the message text,
  carry the target's *current* name, and only link when the target still resolves.
  The product tab reads `SelectValueType.ProductCatalog`, not `SelectValueType.Product`: the latter
  lists what the family has stock of, and the commonest thing to say about a product in a family
  chat is "buy this" - which is exactly the product nobody has at home.
- **The chat enums are numeric, like every other enum the API exchanges** (`FamilyChatMessageKind`,
  `FamilyChatReferenceKind` in `app/types/enums.ts`). There is no `JsonStringEnumConverter`
  registered on the API, so a string union reads nicely and matches nothing the server sends or
  accepts - which is what made attaching a shop or a product fail, and image messages never render
  as images.
- **Optimistic send reconciles on a correlation id, never on content.** The sender receives their
  own `MessageCreated` broadcast like everyone else; the id the client generated before sending is
  echoed back, which is what stops the message rendering twice. A failed send stays on screen as a
  failed bubble with retry and discard — it holds the text the user wrote.
- **Scroll rules follow from newest-at-the-bottom**: "load older" fires at the top and the previous
  scroll height is restored after the prepend (otherwise the stream jumps backwards exactly as the
  reader walks back through it), a send scrolls to the bottom, and an arriving message only does so
  if the reader was already there — otherwise the jump-to-latest pill says something arrived.
- **Windowing is a render cap, not a virtual scroller** (`renderLimit` in `FamilyChatStream`). What
  hurts is a year of history mounted at once after several "load older" pages, not the rows on
  screen; a cap keeps scroll anchoring and text selection working, which a virtualiser over
  variable-height rows would have to reimplement.
- **Sender colour is the family identity colour (#114)** through `useMemberColor`, never a palette
  invented for the chat: a member is one colour everywhere, and a second scheme would make the same
  person two different people.
- Day separators go through the same `groupByDay` / `formatDayBucketDate` pair the activity
  timeline and the notification centre use, so "Today" means the same thing in all three.
- **Message text renders as text nodes, never `v-html`.** The API deliberately does not sanitize
  the body (it would reject `<`, `>` and "5 < 10"), so this is where that safety is actually paid
  for.
- **Links are the one piece of markup derived from user input** (`app/utils/linkify.ts`, unit
  tested). It returns typed *segments*, never HTML, so the template still renders text nodes and
  only the link segments become anchors — `target="_blank" rel="noopener noreferrer nofollow"`. The
  label always shows the full host and only ever shortens the path, because a rewritten label is
  how a spoofed link works; the concatenated segments are the original message, character for
  character.
- **Pictures**: picked with one `accept="image/*"` input (a phone offers camera and gallery from
  it), cropped in the existing `ImageCropper` — the crop screen *is* the preview — compressed
  client-side, then posted as base64. The optimistic bubble shows the local `data:` URL, so the
  sender sees their own photo immediately, and the box is sized from the stored dimensions so the
  stream never reflows as images load. Tapping opens the existing `ImageLightbox`.
- **Typing is reported from the draft's value, not from keydown** — at keydown the model still
  holds the previous value, so the first character would not count and the last deletion would;
  watching the value also covers paste and dictation. The signal is throttled to one call every
  ~2s while there is content, and "stopped" is sent on send, on blur with an empty composer, and
  after ~4s of silence. None of it is load-bearing: the server flag expires by itself, so a lost
  call costs a few seconds of stale indicator rather than a permanent one.
- **The indicator is pinned above the composer and fades**, never expands the layout — a line that
  pushed the message list around every couple of seconds would move what the reader is reading.
  Three phrasings (one name, two names, "several people"), and while the panel is closed the
  bubble carries a pulse instead: the bubble is 56px, and "who" is what opening it answers.
- **There is no byte-level upload progress**, deliberately: the upload is one JSON POST through the
  shared API client, which reports none, and the async job pipeline that does report it costs a
  second round trip plus a poll loop. A failed picture keeps its preview, so retry re-sends the
  same image rather than asking the user to find it again.
- **"Actively watching" is reported honestly, and it is not "connected"** (#149). The socket is an
  app-wide singleton and a backgrounded tab keeps a WebSocket alive, so the client tells the server
  it is watching only while the panel is open *and* the document is visible: `SetChatActive(true)`
  on open, `false` on close, `visibilitychange` either way, a heartbeat to hold the server's TTL
  open, a re-report in `onreconnected` (per-connection state dies with the connection), and an idle
  timeout so a panel left open on a desk stops suppressing notifications. Over-reporting it would
  swallow notifications for somebody who is not there.
- **The badge clears on visibility, never on mount.** `FamilyChatStream` emits `seenLatest` when the
  newest message is actually on screen *and* the document is visible; a panel opened in a background
  tab, or one scrolled back through history, has shown the reader nothing. The count itself always
  comes from the server (the loaded stream is one page deep) and is re-answered by every read.
- **The count is re-read from the server whenever this client can have missed a broadcast**: on
  mount, after an automatic reconnect (the connection was down for every message it was down for),
  when the app returns to the foreground with the panel closed (a backgrounded tab's socket may
  have been suspended), and when a push arrives while a tab is open. Counting arrivals locally is
  right only while the socket is actually delivering them, and it is exactly the cases where it is
  not that the badge is the only thing on screen.
- The visibility listener is therefore attached by `join` (the bubble mounting) rather than by
  `open`, and taken down by `leave` rather than by `close` - with the panel shut its job is keeping
  the badge honest, which is when it matters most.
- A chat notification deep-links to `/calendar?action=open-chat`; the **auth layout** consumes it
  and opens the panel over whatever page is showing, rather than navigating — the chat is a panel,
  not a route. Handled in the layout rather than through `useDeepLinkAction`, which is per page.

---

## Notification centre (`useNotificationCenter`)

The inbox behind the bell in `AppHeader`. It replaces nothing: `NotificationSettingsDrawer`
(renamed from `NotificationsDrawer`) is the *preferences* panel and always was, despite the name.

| Piece | Role |
|---|---|
| `app/composables/useNotificationCenter.ts` | the list, the unread count, the drawer's open flag |
| `app/components/NotificationCenterDrawer.vue` | day-grouped list, "mark all read", load more |
| `app/components/NotificationRow.vue` | one row: icon, wording, relative time, swipe to dismiss |
| `app/utils/notificationTemplate.ts` | a stored row → its i18n keys and icon (unit-tested) |
| `i18n/locales/*.json` → `notifications.types` | one `title` + `body` pair per notification type |

- **Rows carry a type and parameters, never prose.** The wording is composed at read time, so a
  user who switches language does not find a month of another language's sentences in their inbox,
  and fixing a typo in a notification's wording fixes it retroactively. The i18n keys are the API's
  `NotificationType` member *names* (`notifications.types.ShoppingListItemsAdded.title`) —
  PascalCase keys are unusual here and deliberate: the alternative, the enum's numeric value, gives
  locale files full of `notifications.types.13.title`.
- **An unknown type renders as "something happened, and when"** rather than a blank. That is
  reachable in normal operation, not in theory: an installed PWA keeps running the bundle it was
  installed with, so a client can be older than the deploy that added a type — and that row should
  still be visible and markable.
- **The unread count is never derived from the loaded list.** The list is one page deep and the
  count is over the whole inbox. Every endpoint that changes read state answers with the new
  number, and that number is what is stored.
- **Grouped by day through the same `groupByDay`** the activity timeline uses; the locale
  formatting half of its heading logic lives in `formatDayBucketDate` in that util, shared by both
  feeds. One `AnimatedList` per day section — a header inside a `TransitionGroup` would join the
  rows' FLIP animation.
- **A push arriving while the app is open** reaches the page as a service-worker message carrying
  no content: the row is already stored, and the page fetches it, so there is only ever one
  description of a notification. New rows are prepended rather than replacing the list, which
  would discard the pages the reader had already scrolled through.
- The calendar reminder is the one type with four body templates instead of one, because its
  wording branches on all-day and on at-the-start — the same branch the server makes when it words
  the push. Its lead time is phrased by `reminderLeadTimeLabel`, the same helper the
  external-calendar settings form uses.

---

## Theming (light / dark / system)

There is **no hand-written theme provider**. `@nuxt/ui` registers `@nuxtjs/color-mode` internally with `classSuffix: ''`, so the mode is a `light` / `dark` class on `<html>` — the same class Tailwind's `dark:` variant matches. The preference (`'light' | 'dark' | 'system'`, default `system`) is **device-local**: `localStorage`, key `nuxt-color-mode`. There is no server-side user preference, so the theme does not follow a user across devices.

The UI is a segmented control in `app/pages/profile/index.vue` (`themeOptions`) that assigns straight to `colorMode.preference`. It must stay inside `<ClientOnly>` — the preference is unknown during SSR, so branching on it there is a hydration mismatch. `system` reacts live: color-mode listens to `prefers-color-scheme` changes.

Colours always come from Nuxt UI's semantic tokens (`--ui-bg`, `--ui-text*`, `--ui-primary`, …), which flip themselves via the class. Only the `mocha` primary palette is project-owned (`app/assets/css/main.css`). **Never hardcode a hex where a token exists** — a literal cannot follow the theme.

### The two places the theme has to be pushed out by hand

1. **`<html>` background + `color-scheme`** (`app/assets/css/main.css`). Nuxt UI only styles `<body>`; iOS standalone samples the *root* background for the status-bar strip, so without this the top strip stays light in dark mode.
2. **The `theme-color` meta** (`app/plugins/theme-color.client.ts`). Client-only, driven by a `watch` on `colorMode.value`, and it reads the value back from the root element's computed background so it can never drift from `--ui-bg`. It goes through `useHead` with `tagPriority: 'high'`: both the static tag in `nuxt.config.ts` and the manifest-derived one `@vite-pwa/nuxt` injects are unhead-managed, so a plain `meta.content = …` write gets reverted on the next flush. Reads are **synchronous, never `requestAnimationFrame`** — a backgrounded or non-compositing webview does not run animation frames.

`apple-mobile-web-app-status-bar-style` stays `default`, and there is deliberately **no `viewport-fit=cover`** (so `env(safe-area-inset-*)` resolves to `0px` on iOS). Switching to `black-translucent` + `viewport-fit=cover` would give pixel control of the strip but hands the status-bar glyph colour to iOS and requires a safe-area audit of every top-fixed element.

---

## Boot splash

`app/components/SplashScreen.vue` — a CSS overlay, **standalone-PWA only**, that covers the pre-hydration auth window. Not documented in the structure tree above; the moving parts:

| File | Role |
|---|---|
| `app/components/SplashScreen.vue` | the overlay (logo + loading ring SSR'd, name/version `<ClientOnly>`) |
| ↳ the logo | an **inline** SVG, not `<img src="/favicon.svg">` — an `<img>` is an opaque box to CSS |
| `app/composables/useSplashScreen.ts` | `markReady()` / `rearm()`, `MIN_VISIBLE_MS` floor |
| `app/plugins/auth.ts` | owns the primary dismissal after the Kratos session resolves (6 s safety net) |
| `app/pages/calendar.vue` | dismisses on a normal relaunch, after its first data load |
| `app/plugins/splash-resume.client.ts` | re-shows it on a warm resume after ≥10 min backgrounded |
| `nuxt.config.ts` | inline head script adding `.pwa-standalone` (iOS `navigator.standalone`) |

It is theme-aware purely through tokens — `--ui-bg` matches the app background exactly, so the handoff is seamless in both themes, and no `:root.dark` selector is needed because color-mode sets the class from a blocking head script before first paint.

The mark draws itself in: the three faces of the isometric logo are traced by a stroke (`pathLength="1"` makes the dash maths independent of each path's real length), staggered, and each fill then comes up behind its outline; the ring fades in after them, and the `<ClientOnly>` name/version rise in on mount. It is all `stroke-dashoffset` / `opacity` / `transform` on SSR'd markup, so — like the ring — it runs off the first painted frame with no help from hydration, and the whole sequence lands well inside `MIN_VISIBLE_MS`. **The splash never extends its own life for the animation:** dismissal cuts it off whenever the app is ready. Under `prefers-reduced-motion` the logo is simply there, fully drawn.

**Easy to regress, all deliberate:**
- `visibility: hidden` on `:root[data-splash-ready] .splash`, delayed `visibility 0s linear 0.55s` — iOS keeps sampling a `fixed; inset: 0` element's background for the status bar even at `opacity: 0` and translated off-screen.
- Dual standalone gate: `@media (display-mode: standalone)` **and** `:root.pwa-standalone` (iOS does not reliably match the media query).
- The `<style>` is unscoped on purpose, so `.splash` stays a literal class name for those selectors.
- No `transform` on `.app-shell` — a transformed ancestor would become the containing block for the auth layout's `position: fixed` bottom nav.
- Dismissal toggles an attribute on `<html>`, not a reactive `v-if`, so it works even before Vue mounts.

The manifest's `background_color` takes a single value and cannot be theme-aware; it is tuned to the light background, so a dark-theme launch can show a brief light flash on Android's generated launch screen. There are no `apple-touch-startup-image` tags, so iOS shows a blank frame until the SSR HTML paints.

---

## Barcode scanner overlay

`BarcodeScannerModal.vue` + `useBarcodeScanner.ts`. The composable owns a `cameraState` (`idle` → `requesting` → `ready`, or `denied` / `missing` / `failed`) which it folds together with the scan buffer into one `scanState` the overlay switches on: `requesting`, `denied`, `no-camera`, `error`, `searching`, `found`. Camera trouble outranks everything — there is nothing to search for without a stream — and each of those states gets its own full-bleed panel, with a fix-it hint and a retry for the two that a user can act on.

- The camera stage has a **fixed aspect ratio**: without it the wrapper collapses in exactly the states (permission denied, no camera) that have no video to give it a height.
- Every successful decode — live buffer, tap-to-capture snapshot, or a manual pick from the multi-code list — goes through one `finishWithBarcode()`, which beeps, buzzes, freezes the stream and holds the result for `SUCCESS_HOLD_MS` before handing it to the caller. That beat is what the reticle's snap-and-checkmark needs; it is deliberately short enough not to read as latency.
- The reticle is a DOM overlay, not canvas: vue-qrcode-reader hands the `track` callback coordinates **already mapped into element pixel space** (it sizes the tracking canvas to the wrapper's offset size and compensates for `object-fit: cover`), so a box recorded there positions a DOM element verbatim. The tracker records every code's box each frame; on success the reticle animates onto the winning one.
- `track` draws each detected code's outline and its decoded value, with the largest one — the one the user is aiming at, and the one the stability buffer will settle on — in the primary colour and the rest dimmed. **Canvas cannot resolve `var(--ui-primary)`**: the tokens are read off the DOM with `getComputedStyle` on `@camera-on` and reused per frame. (The old code passed `rgb(var(--color-primary-500))` straight to `strokeStyle`, which the canvas silently ignored.)
- Torch and camera-switch buttons appear only where the track supports them (`capabilities.torch`, and `useCameraAvailability().hasMultipleCameras`). Constraints are read once when a stream starts, so switching camera — and retrying after an error — bumps a nonce in the stream's `key` to force a remount. `markCameraReady()` re-arms `isScanning` on the way back up, since `handleCameraError` turned it off.

---

## Drag-and-drop reordering

Four lists can be put in the user's own order — the shopping list's items, storage locations,
shopping locations and automation rules. Order lives on the row as a `sortOrder` the API owns; the
client never derives it from array position, because the rows arrive from a fetch, from a socket
event and from local upserts and array position survives none of those.

| Piece | Role |
|---|---|
| `app/utils/manualOrder.ts` | the pure rules: sort, plan an optimistic write, fold in an event (unit-tested) |
| `app/composables/useDragReorder.ts` | the gesture: long-press lift, auto-scroll, drop, `Escape`, arrow keys |
| `app/composables/useReorderableList.ts` | ties the two together with the optimistic write and its revert |
| `app/components/ReorderHandle.vue` | the grab affordance (also the keyboard entry point) |
| `data-reorder-key` | what the drag composable scans a container for |

- **`sortOrder` is a sparse gapped integer, not an index** (see `SparseOrdering` on the API side), so
  moving one row usually writes one row. Everything untouched holds **0**, which is why every list
  passes a `baseSort` — that is the order it had before this feature existed, and it is what a list
  nobody has dragged still shows.
- **The dragged card is a `cloneNode` pinned to the viewport, not the card itself.** Every one of
  these lists is inside `AnimatedList`, whose `TransitionGroup` animates moves by writing `transform`
  on each child; a card following the finger through its own transform would be fighting that FLIP
  for the same property. The real card stays in place at 40% opacity as the drop placeholder, so it
  and its neighbours glide with the existing `--bubble-move` transition.
- **Clustering of the gesture, not of the data:** the drop target is the row under the pointer, or
  the nearest by centre distance — these lists are grids on wider screens, where "the row below" is
  not a single direction.
- **A drag under an active filter only ever sends what the user can see.** Each page hands the
  composable a `filter` predicate, and `planOptimisticOrder` reuses the visible rows' *own* positions
  rather than renumbering by index, so rows the filter is hiding stay where they were relative to
  them. The API accepts that subset and documents the same caveat.
- **Touch lifts on a long press (350 ms), the mouse lifts immediately.** A list that lifted on the
  first pixel of a touch drag could not be scrolled. `ReorderHandle` sets `touch-action: none` in CSS
  so it wins over the `pan-y` the surrounding card needs for its swipe gesture.
- **Arrow keys on the handle move a row one place**, because a drag has no keyboard equivalent to
  fall back on.
- **Realtime:** `ItemsReordered` (shopping list) and `StorageLocationsReordered` /
  `ShoppingLocationsReordered` / `AutomationsReordered` (master data) carry only the rows that moved.
  They are ignored while a local drag is in progress — the finger owns the on-screen order until it
  lifts, and the server's answer wins from there.
- On the shopping list this is a **mode** (`sortMode`, per list, persisted in
  `shoppingListsSortModes`), alongside the urgency-then-name ordering rather than replacing it. In
  manual mode the "buy here" split is not rendered: dragging between two independently ordered
  sections has no meaning, and the point of the mode is that the order on screen is the one the user
  set.

---

## Shopping mode (in-store)

`ShoppingModeView` + `ShoppingModeRow`, opened from the basket button on the shopping list. The
planning view is built for deciding what to buy; this is the same list with everything removed
except what the next thirty seconds need — what is left, how much of it, and a target big enough to
hit while walking. Everything else is one tap away on the row.

- **It owns no data.** Ticking a row emits the same `purchase-requested` the planning card emits, so
  the page runs the identical optimistic path (and the same undo window) and the two views cannot
  disagree about what has been written.
- **Ordering is the aisle order with "buy here" lifted on top** — the manual order from the section
  above, and the same proximity set the planning view sections off.
- `useWakeLock` keeps the screen on while the mode is open. The lock is re-acquired on
  `visibilitychange`, because backgrounding the tab releases it and nothing tells the page to ask
  again; it is released on exit and on unmount, since a lock left held is the worst possible bug for
  a feature whose whole point is the battery. Absent on Firefox and older iOS, where the screen
  simply behaves normally.
- **Which lists are mid-shop lives in `useShoppingMode`, module-scoped and deliberately not
  persisted.** It has to survive leaving the page (answering a notification mid-shop must not drop
  you back into the planning view) but not a reload — someone who got home to a cold start should not
  land in a shop screen.
- Finishing the list plays a short celebration and then closes the mode; the *view* owns that timing,
  so the page closing the screen cannot cut it off. A list emptied by deleting rather than buying
  closes immediately — nothing was accomplished, so there is nothing to celebrate.

---

## Maps (`InteractiveMap`)

MapLibre GL over keyless CARTO raster basemaps, replacing the OpenStreetMap `export/embed.html`
iframe. The iframe could draw exactly one static pin and nothing inside it could be styled, animated
or clicked — so a multi-shop overview, clustering, or highlighting the shop you are standing next to
were all impossible, even though the shopping list already knew the user's position.

- **Lazily loaded.** Both the library and its stylesheet are imported inside `onMounted`, so a
  session that never opens a map never pays for them. That is also why it is a component and not a
  plugin. Its chunk and CSS are separate in the build output — check that when touching the imports.
- **Light and dark basemap variants** (`light_all` / `dark_all`), swapped on `colorMode`. The style
  is built inline rather than fetched: nothing to host, no extra request, and a theme swap is a
  one-property change. A `setStyle` drops nothing but the tiles, so the markers are re-added on
  `styledata`.
- **Markers are DOM elements**, styled with the app's own tokens, which is what lets the "you are
  here" ring be an ordinary CSS animation. Clustering is grid-based in screen space rather than
  MapLibre's GeoJSON clustering, because that draws its counts as map text and would need a glyph
  server — a second keyless third party for the sake of drawing a number. Clusters are rebuilt on
  `moveend`/`zoomend`, not per frame: MapLibre already keeps each marker pinned while panning.
- `LocationMap` keeps its old contract — stored coordinates preferred, Nominatim geocoding as the
  fallback, and **nothing rendered at all** when there are neither coordinates nor a resolvable
  address. Several screens rely on that last behaviour to decide whether a map section appears.
- The shopping-locations page adds the multi-shop map: every shop at once, clustered, tapping a
  marker opens the existing overview drawer, with a pulse ring on the nearest. It reads the device
  position **only when the browser already reports permission as granted** — a list of shops is not a
  reason to put a permission prompt in front of anyone.
- Tiles get their own service-worker cache (`map-tiles`, 300 entries, 7 days), ahead of the generic
  `static-assets` rule: they are `.png` and would otherwise let one panned map evict the app's icons
  and fonts.
- A failure to load (blocked CDN, no WebGL, offline) shows a one-line notice; the caller still has
  the address and its "open in maps" link.

---

## Shopping-list proximity ("you are here")

On the shopping-list page a locate button (shown when the open list has location-bound items) requests the device position via `useGeolocation` and highlights items to buy at a nearby store:

- **Auto-start**: once the open list has location-bound items, `maybeAutoStartLocation()` starts tracking on its own — but only when `getPermissionStatus()` already reports `'granted'`, so nothing is ever prompted without a tap (a `'prompt'` / `'denied'` state is left alone). It runs silently: `enableLocation({ silent: true })` suppresses both toasts. The locate button still reflects and toggles the state, and the `autoLocate` preference (persisted in the `shoppingListsFilters` localStorage entry, toggled in the filter drawer's Properties group) switches the behaviour off.
- **"Buy here" section**: while the user is inside a store's radius (`isAtAnyStore`), `filteredItems` is split into `hereItems` (`isItemBuyableHere` = exact store **or** same-type store) and `restItems`, rendered as two grids with the "buy here" one pinned to the top under a store-icon header naming the nearby store(s) (`currentLocationNames`) and the pending count. Two `AnimatedList`s rather than one grid with a spanning header — a header child would join the TransitionGroup's FLIP animation. The cards keep their own blue / cyan-dashed distinction inside the section.
- **"Buy here" filter**: `locationFilter` takes an extra `'here'` value, offered in the location chip group only while `isAtAnyStore` is true, and reset to `'all'` by a watcher when the user leaves every radius. It flows through `activeFilterCount` / `activeFilters` / `clearAllFilters` like the other location values.
- Shopping locations store `latitude`/`longitude` (geocoded once on save in `ShoppingLocationFormDrawer` via `useGeocoding`); locations without stored coords are geocoded at runtime as a fallback. `LocationMap` prefers stored coords.
- Locations also carry `storeTypes` — a **multi-select** of the localized `StoreType` enum (`app/types/enums.ts`, labels `enums.storeType.*` in all three locale files). A location can have several (e.g. OBI = hardware + garden). Shown as badges on `DataShoppingLocationCard` / `ShoppingLocationOverviewDrawer`; edited in `ShoppingLocationFormDrawer` and the inline create form in `AddShoppingListItemModal` (via `formatStoreType` from `useEnumLabel`).
- The page loads **all** saved shopping locations (`useLocationsApi().getShoppingLocations`), so proximity is matched against every store you own — not only ones on the open list. Items whose shopping location is within `NEARBY_RADIUS_METERS` (`utils/geoUtils.ts`, haversine) get a blue border + "buy here" chip (`ShoppingListItemCard` `atCurrentLocation` prop), plus a "you are here" banner.
- **"Similar store here"**: the type(s) of the store(s) you're currently at form `currentStoreTypes`; any list item assigned to a *different* store that shares a type (`isItemSimilarTypeHere` in `index.vue`) gets a distinct **cyan dashed** border + chip (`ShoppingListItemCard` `similarTypeAtCurrentLocation` prop). `Other` is excluded from matching. So standing in an Auchan flags your Tesco items, and an OBI flags both hardware-store and garden-centre items.
- Editing a list item (`ShoppingListItemCard` edit modal) can reassign its shopping location or clear it — clearing sends `clearShoppingLocation: true` (a null id means "no change" server-side).
- While the page is open, `watchPosition` fires a **foreground-only** local notification (via the SW registration, reusing the existing Notification permission) on arriving at a store with items to buy. There is **no background geolocation/geofencing** in a PWA — a closed-app "you arrived" notification would need a native (Capacitor) wrapper.

---

## Global command palette (#111)

`CommandPalette` is mounted **once**, in `layouts/auth.vue`, next to the chat panel and for the
same reasons: it is opened from three places, it has to survive navigation, and its `Ctrl/Cmd+K`
listener must exist exactly once. Opened from the header's search button, from that chord, or
from `useCommandPalette().open()`. It is a `UModal` at every width, `fullscreen` below `lg` —
that is the "full-height sheet with the keyboard focused" a phone wants, without a second
component to keep in step.

Inside it, a `UCommandPalette` with **`preserve-group-order`** and `ignoreFilter: true` on every
server-answered group: the ranking is the server's (`SearchFunctions`), and letting fuse re-score
rows it never filtered would fight it. The navigation and action groups are left to fuse, so
typing "cal" reaches the calendar without a request. Each server group ends in a "show all N in …"
row; a group whose `hasMore` is set says "show all" without a number, because the server stopped
counting at its scan cap.

- `useCommandPalette` holds the state (open flag, query, results, per-device recent searches in
  `localStorage`). Requests are **debounced and sequenced** — every request carries a sequence
  number and only the newest may write the results, so a slow early answer cannot land under a
  newer query. Debouncing alone does not prevent that.
- Highlighting is the existing `useSearchHighlight`, fed the term that was actually typed.
- A hit navigates by kind. Two of the six have no page of their own: an inventory item opens its
  product, and a list or a location is opened by its own list page through `?select=`.
- `useSearchHandoff` is the receiving end of both that and the "show all" row: a list page reads
  `?search=` / `?select=` off the route, strips them with `router.replace` (so a reload or a
  back-navigation does not re-apply a filter the reader has since cleared), and applies them.
  Pass `ready` when opening something needs the page's own data to have arrived — `select` waits
  for it, `search` never does.

---

## Voice input (#132)

Hold the microphone in `VoiceItemDrawer`, say "two litres of milk, bread and six eggs", and get
three pre-filled rows to confirm. Offered as a FAB action on the products and shopping-list
pages, next to the other add actions.

Three pieces, deliberately separate:

- **`utils/voiceItemParser.ts`** — the grammar, as a pure function over a string. Splits an
  utterance on punctuation and the locale's own conjunctions, then reads each fragment as
  `[quantity] [unit] name`, with a trailing-quantity fallback (`milk 2 litres`) for the way people
  read a written list aloud. Spoken numbers and unit words are tabled per locale for all three.
  Being pure is the point: it is the only part of a microphone feature that can be tested, and
  `tests/unit/voiceItemParser.spec.ts` is where its behaviour is pinned. A comma between two
  digits is a decimal separator, not a list separator.
- **`useVoiceItemMatching`** — what a name refers to, on three rungs: an existing product first,
  then the 947-word localized `enums.productCategory` vocabulary (indexed once per locale), then
  the words themselves. Hungarian object and plural endings are trimmed before matching, because
  "két liter tejet" otherwise matches nothing at all.
- **`useSpeechRecognition`** — the Web Speech API, prefixed and unprefixed, `continuous` (a list
  is read out with pauses in it, and a single-shot recognizer stops at the first one). Reports
  only the failures the UI says something different about: `denied`, `no-speech`, `network`.

**Nothing is ever written silently.** Speech becomes editable rows badged with what each matched,
and a row that will create a catalogue product says so and shows the brand it would use. On a
shopping list an unmatched name becomes a free-text item; inventory has no free text, so those
rows create the product first.

Where the API does not exist (Firefox has no implementation at all) the FAB action and the drawer
are **both** gated on `isSupported`, so nothing offers a microphone the browser cannot open. The
drawer states in as many words that the transcription is the browser's and may leave the device.

The hold gesture uses **pointer capture**: without it the browser retargets the release to
whatever is under the finger, and "release outside to cancel" cannot work at all.

---

## The public landing page (#123)

`pages/index.vue` under `layouts/public.vue`: a hero with a device-framed app screen, four
showcase bands (inventory, shopping list, scanner, calendar), the live `HomepageStats`, then the
feature grid and the CTA.

- The screens are **rendered in markup** (`components/landing/AppScreen.vue`), not shipped as
  bitmaps. They are built from the app's own semantic tokens and the real expiration ramp, so
  they follow the visitor's theme by themselves, cost no image bytes, and cannot go stale against
  a redesign. They are a likeness, not a capture — the data in them is invented.
- `LandingDeviceFrame` owns the aspect ratio, so whatever is inside occupies the same box and
  nothing shifts. It already accepts `light` / `dark` sources for the day real screenshots exist;
  pass those instead of the slot and nothing else about the page changes.
- `useScrollReveal` is `opacity` + `transform` only, on sections whose space is already reserved,
  with the observer disconnected once it has fired and the whole effect skipped under
  `prefers-reduced-motion`.
- Metadata is per locale (`meta.home.*`) with an Open Graph / Twitter card image at
  `public/og-image.png`. Regenerate it with `npm run generate:og-image`; the card image has to be
  an **absolute** URL, which is what `NUXT_PUBLIC_SITE_URL` is for.
- The page **renders during SSR**. It used to gate everything on a session check that can only run
  on the client (the Kratos cookie is httpOnly), which meant the server returned an empty div —
  nothing for a crawler or a link preview to read. The redirect for an already-authenticated
  visitor happens over the rendered page instead.

---

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `NUXT_PUBLIC_API_BASE` | `http://localhost:5226` | Homassy.API base URL (production: `https://homassy.kellner.dev` — same origin, the reverse proxy routes `/api/v*` + `/hubs/*` to the API; other `/api/*` paths such as `@nuxt/icon`'s `/api/_nuxt_icon/*` stay on the Nuxt server) |
| `NUXT_PUBLIC_KRATOS_URL` | `http://localhost:4433` | Kratos public URL (production: `https://homassy.kellner.dev/kratos`) |
| `NUXT_PUBLIC_SITE_URL` | `http://localhost:3000` | Where this deployment is reachable from the outside. Used only for absolute URLs a crawler cannot resolve itself — the landing page's Open Graph / Twitter card image (production: `https://homassy.kellner.dev`) |

In Docker, these are passed as build args and compiled into the static bundle. Set them at build time, not at runtime. In production both point at the single public domain served by the Caddy reverse proxy (`Homassy.Proxy/Caddyfile`).

---

## Docker / Build

```dockerfile
# Development (hot-reload)
FROM node:22-alpine AS development
# npm ci → nuxt dev --host 0.0.0.0 on :3000

# Build
FROM node:22-alpine AS build
# node --max-old-space-size=4096 (memory-limited builds)
# npm run build → .output/ (Nitro server bundle)

# Production
# Serves .output/ with node .output/server/index.mjs on :3000
```

Build-time env (single-domain reverse-proxy setup):
```bash
docker build \
  --build-arg NUXT_PUBLIC_API_BASE=https://app.example.com \
  --build-arg NUXT_PUBLIC_KRATOS_URL=https://app.example.com/kratos \
  -t homassy-web .
```

---

## Dev Commands

```bash
# Install dependencies
npm ci

# Start dev server (hot-reload, port 3000)
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview

# Lint
npx eslint .

# Type check
npx nuxi typecheck

# Regenerate the ProductCategory enum + its three locale blocks from the API enum
npm run sync:product-category

# Regenerate the landing page's Open Graph / Twitter card image (public/og-image.png)
npm run generate:og-image
```

---

## Development Guidelines

### Adding a New Page

1. Create `app/pages/<route>.vue`
2. Add `definePageMeta({ layout: 'auth' })` for protected pages (applies auth middleware)
3. Add `definePageMeta({ layout: 'public' })` for public pages (e.g. auth flows)
4. Use `useApiClient` + the appropriate `use*Api` composable for data fetching
5. Use `useI18n()` → `const { t } = useI18n()` for all user-facing strings

### Adding a New API Composable

Create `app/composables/api/useMyApi.ts`:

```typescript
export const useMyApi = () => {
  const { request } = useApiClient()

  const getItems = async (): Promise<ApiResponse<Item[]>> => {
    return request<Item[]>('/api/v1.0/myresource')
  }

  const createItem = async (data: CreateItemRequest): Promise<ApiResponse<Item>> => {
    return request<Item>('/api/v1.0/myresource', { method: 'POST', body: data })
  }

  return { getItems, createItem }
}
```

Export it from `app/composables/api/index.ts`.

### Adding i18n Keys

Add the key to all three locale files: `i18n/locales/en.json`, `hu.json`, `de.json`. Never hardcode user-visible text — always use `t('key')`.

**Exception — `enums.productCategory` and `enums.productCategoryGroup`:** those blocks are generated, do not hand-edit them. See below.

### ProductCategory is generated, not hand-written

`Homassy.API/Enums/ProductCategory.cs` is the **canonical** definition (947 members). The numeric value is what `Product.Category` persists, and the option list the frontend shows comes from the API (`GET /selectvalues?type=ProductCategory` → `SelectValueFunctions.GetProductCategorySelectValues`), which enumerates that enum. The web side must never invent its own numbering.

Derived from it, all regenerated by one script:

- `app/types/enums.ts` → `export enum ProductCategory`
- `app/types/enums.ts` → `export enum ProductCategoryGroup`
- `app/utils/productCategoryGroups.ts` (whole file)
- `i18n/locales/{en,hu,de}.json` → `enums.productCategory`
- `i18n/locales/{en,hu,de}.json` → `enums.productCategoryGroup`

```bash
cd Homassy.Web && npm run sync:product-category
```

After adding or renaming a member in the C# enum, add its `en`/`hu`/`de` labels to `LABELS` in `scripts/sync-product-category.mjs` (keyed by **member name**, never by number) and re-run the script. It refuses to write if the enum and `LABELS` disagree, if a member falls in no `GROUPS` range or in several, or if a language reuses a label — duplicated labels are what let an earlier key-to-label drift go unnoticed.

**Numbering convention:** the enum is organised into thematic numeric blocks with deliberate gaps (food 1-49, medicine 50-69, …, bathroom 900-929, building materials 930-969, hardware 970-999, garage 1000-1029). Add a new member inside the block it belongs to, using a free number in that range — never renumber, and never append to the end just because it is easier.

### ProductCategoryGroup and the category pickers

`GROUPS` in the sync script maps the enum's numeric blocks onto 27 presentation buckets (Fürdőszoba, Kert és szabadtér, Autó és garázs, …). It is **presentation-only** — the API neither knows nor stores the group, and `Product.Category` is unaffected.

With ~950 categories a flat `USelectMenu` is unusable, so all three pickers (`AddInventoryItemModal`, `AddShoppingListItemModal`, `ProductFormDrawer`) render grouped, virtualized, searchable lists via `useProductCategoryOptions`:

```ts
const categoryOptionsRaw = ref<SelectValue[]>([])
const { categoryOptions } = useProductCategoryOptions(categoryOptionsRaw)
```

The option values are the enum's **numbers**. `Product.Category` is a C# enum and the API registers no string enum converter, so a stringified category comes back as `400 The JSON value could not be converted to ProductCategory` — which is exactly what the master-data drawer used to do.

The composable returns an array of arrays (Nuxt UI's grouped-items shape), each headed by a `type: 'label'` entry, with the categories sorted by localized label inside each group. `virtualize` on the `USelectMenu` keeps the option list from mounting ~950 DOM nodes.

### One product form contract

A product can be created from three places — `ProductFormDrawer` (the Törzsadatok screen), `AddInventoryItemModal` and `AddShoppingListItemModal` — and they all take their Zod schema and their request payloads from `useProductFormSchema`:

```ts
const { productSchema, toCreateProductRequest, toUpdateProductRequest } = useProductFormSchema()
const form = ref(emptyProductForm())
```

Do not re-declare the schema in a form. The three used to carry their own copy and had drifted: different name/brand minimums, three different barcode and notes limits, and a category typed as a string in the master-data drawer, which the API rejected outright.

The limits mirror `CreateProductRequest` / `UpdateProductRequest` in Homassy.API (name and brand 2–128, barcode 6/8/12/13 digits, notes ≤ 128), so a form that validates locally is not turned away by the server. When those annotations change, change the schema with them.

A category of `ProductCategory.Other` is **0**, so the payload builders use `?? null` and the cards render on `category != null` — a truthiness check silently drops the category.

### Adding a New Type

Add to the appropriate file in `app/types/` or create a new file. Types are not auto-imported; import explicitly:
```typescript
import type { MyType } from '~/types/myType'
```

### Icons

Use Heroicons or Lucide via Nuxt UI's `UIcon`:
```vue
<UIcon name="i-heroicons-home" class="h-6 w-6" />
<UIcon name="i-lucide-scan-barcode" class="h-5 w-5" />
```

### Toasts

Use Nuxt UI's `useToast()`:
```typescript
const toast = useToast()
toast.add({ title: t('toast.success'), description: '...', color: 'success', icon: 'i-heroicons-check-circle' })
toast.add({ title: t('toast.error'), description: '...', color: 'error', icon: 'i-heroicons-x-circle' })
```

`useApiClient` handles error toasts automatically for API call failures; pass `showErrorToast: false` to suppress.

### SSR Considerations

- Auth state cannot be read during SSR (Kratos cookies are httpOnly)
- The auth middleware skips validation on SSR and validates on the client
- Use `import.meta.client` guards for browser-only code
- Client-only plugins: suffix filename with `.client.ts`

### Important Notes

- **Never store session tokens in `localStorage`** — auth is cookie-based only
- **`clearAuthData()` does NOT call Kratos logout** — call `useKratos().logout()` explicitly for user-initiated logout
- **`$api` uses `credentials: 'include'`** — the Kratos session cookie is forwarded automatically to `Homassy.API`
- **Build args baked in** — `NUXT_PUBLIC_*` env vars are baked into the client bundle at build time; changing them requires a rebuild
