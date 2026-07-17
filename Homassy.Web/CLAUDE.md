# Homassy.Web – CLAUDE.md

## Overview

Homassy.Web is the **frontend application** of the Homassy platform. It is a **Nuxt 4 / Vue 3** single-page application with PWA support, targeting mobile-first usage. It communicates with `Homassy.API` for business data and directly with **Ory Kratos** for authentication flows (login, registration, verification, recovery, WebAuthn/passkeys).

### Key Architectural Decisions

- **Nuxt 4 + Vue 3 Composition API** – file-based routing, auto-imports, SSR disabled for auth-sensitive pages
- **Ory Kratos** – session-based auth using httpOnly cookies (`ory_kratos_session`); no JWT on the frontend
- **`nuxt-api-party`** – server-side API proxying to `Homassy.API`; `$api` plugin for client-side calls with automatic 401 handling
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
│   │   ├── security/           Security/WebAuthn components
│   │   └── *.vue               Cards, modals, buttons, etc.
│   ├── composables/
│   │   ├── api/                API composables (one per controller)
│   │   │   ├── index.ts        Re-exports all API composables
│   │   │   ├── useAuthApi.ts
│   │   │   ├── useProductsApi.ts
│   │   │   ├── useShoppingListApi.ts
│   │   │   ├── useLocationsApi.ts
│   │   │   ├── useFamilyApi.ts
│   │   │   ├── useUserApi.ts
│   │   │   ├── useSelectValueApi.ts
│   │   │   ├── useOpenFoodFactsApi.ts
│   │   │   ├── useProgressApi.ts
│   │   │   ├── useAutomationApi.ts
│   │   │   ├── useCalendarApi.ts
│   │   │   ├── useStatisticsApi.ts
│   │   │   ├── useHealthApi.ts
│   │   │   ├── useErrorCodesApi.ts
│   │   │   └── useVersionApi.ts
│   │   ├── index.ts            Re-exports all composables
│   │   ├── useApiClient.ts     Wrapper: $api + toast error handling
│   │   ├── useKratos.ts        Ory Kratos FrontendApi flows
│   │   ├── useBarcodeScanner.ts
│   │   ├── useCameraAvailability.ts
│   │   ├── useDateFormat.ts
│   │   ├── useDeviceDetection.ts
│   │   ├── useEnumLabel.ts
│   │   ├── useEventBus.ts
│   │   ├── useExpirationCheck.ts
│   │   ├── useFabActions.ts    Shared state for the layout floating action button
│   │   ├── useGeocoding.ts     Address → coordinates via Nominatim (OpenStreetMap, keyless)
│   │   ├── useGeolocation.ts   Browser Geolocation wrapper (permission + getCurrentPosition + watch)
│   │   ├── useImageCrop.ts
│   │   ├── useInputDateLocale.ts
│   │   ├── usePullToRefresh.ts
│   │   ├── usePushNotifications.ts
│   │   ├── useShoppingListSocket.ts  SignalR realtime client for shopping lists
│   │   ├── useInventorySocket.ts  SignalR realtime client for the Készletek (inventory) grid
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
│       └── stringUtils.ts
├── i18n/
│   └── locales/
│       ├── en.json             English translations
│       ├── hu.json             Hungarian translations
│       └── de.json             German translations
├── public/                     Static assets (icons, favicons, sw-push.js)
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
| `useAuthApi` | Auth-related API helpers |
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
| `useStatisticsApi` | Global platform statistics |
| `useHealthApi` | API health check |
| `useErrorCodesApi` | Error code descriptions |
| `useVersionApi` | API version info |

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

## Layouts

### `auth.vue` (authenticated)

Used for all protected pages. Features a **fixed bottom navigation bar** with:
- Home (Products)
- Shopping Lists
- Activity
- Profile

The bottom nav shows a **red badge** for expiring products (from `getExpirationCount()`) and overdue shopping list items (from `getDeadlineCount()`). Counts are refreshed on mount and via `useEventBus`.

### `public.vue`

Used for `/auth/*` pages. Minimal layout without navigation.

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
- **Manifest**: standalone display, `#c9b8a0` theme color
- **Service Worker**: `workbox`-powered
  - Pages: `NetworkFirst`, 1-day cache
  - Static assets: `CacheFirst`, 30-day cache
  - Push notifications: `/sw-push.js` (imported into SW)

---

## Shopping-list proximity ("you are here")

On the shopping-list page a locate button (shown when the open list has location-bound items) requests the device position via `useGeolocation` and highlights items to buy at a nearby store:

- Shopping locations store `latitude`/`longitude` (geocoded once on save in `ShoppingLocationFormDrawer` via `useGeocoding`); locations without stored coords are geocoded at runtime as a fallback. `LocationMap` prefers stored coords.
- Locations also carry `storeTypes` — a **multi-select** of the localized `StoreType` enum (`app/types/enums.ts`, labels `enums.storeType.*` in all three locale files). A location can have several (e.g. OBI = hardware + garden). Shown as badges on `DataShoppingLocationCard` / `ShoppingLocationOverviewDrawer`; edited in `ShoppingLocationFormDrawer` and the inline create form in `AddShoppingListItemModal` (via `formatStoreType` from `useEnumLabel`).
- The page loads **all** saved shopping locations (`useLocationsApi().getShoppingLocations`), so proximity is matched against every store you own — not only ones on the open list. Items whose shopping location is within `NEARBY_RADIUS_METERS` (`utils/geoUtils.ts`, haversine) get a blue border + "buy here" chip (`ShoppingListItemCard` `atCurrentLocation` prop), plus a "you are here" banner.
- **"Similar store here"**: the type(s) of the store(s) you're currently at form `currentStoreTypes`; any list item assigned to a *different* store that shares a type (`isItemSimilarTypeHere` in `index.vue`) gets a distinct **cyan dashed** border + chip (`ShoppingListItemCard` `similarTypeAtCurrentLocation` prop). `Other` is excluded from matching. So standing in an Auchan flags your Tesco items, and an OBI flags both hardware-store and garden-centre items.
- Editing a list item (`ShoppingListItemCard` edit modal) can reassign its shopping location or clear it — clearing sends `clearShoppingLocation: true` (a null id means "no change" server-side).
- While the page is open, `watchPosition` fires a **foreground-only** local notification (via the SW registration, reusing the existing Notification permission) on arriving at a store with items to buy. There is **no background geolocation/geofencing** in a PWA — a closed-app "you arrived" notification would need a native (Capacitor) wrapper.

---

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `NUXT_PUBLIC_API_BASE` | `http://localhost:5226` | Homassy.API base URL (production: `https://homassy.kellner.dev` — same origin, the reverse proxy routes `/api/v*` + `/hubs/*` to the API; other `/api/*` paths such as `@nuxt/icon`'s `/api/_nuxt_icon/*` stay on the Nuxt server) |
| `NUXT_PUBLIC_KRATOS_URL` | `http://localhost:4433` | Kratos public URL (production: `https://homassy.kellner.dev/kratos`) |

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
