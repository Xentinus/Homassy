# Homassy — native iOS port guide

Written 2026-09-22. This is a porting reference, not a plan of record: it inventories what
the current web app actually contains, maps each piece to its native iOS equivalent, and
lays out the three architectures that were open — including the local-only / iCloud-export /
paid-online-backup shape sketched in the original request.

**Decided 2026-09-22: Option B** — local-first SwiftData/Core Data with CloudKit, private
database for personal data and a shared record zone for the family. §3.4 covers what that
means for authentication, which is the largest single consequence: **the free app has no
login at all.**

**Also decided 2026-09-22:**

- **Images stay in CloudKit.** The existing compression (500 px / 0.5 MB for products) keeps
  them small enough that offloading them to a server is not worth building. §9.4 records the
  option and why it was dropped.
- **Cut from the iOS app entirely: family chat, external calendar subscriptions, and the Open
  Food Facts lookup.** §6.0 lists what each removal takes with it, and the one design question
  the Open Food Facts cut opens up about barcode scanning.
- **No subscription, no paid features.** A voluntary "Support Homassy" screen linking out to
  our own donation URL — **donations, with nothing given in return**, which is what keeps both
  the App Store review and the tax treatment simple (§9). Apple Pay is not an option for this;
  §9.1 covers the external-link rules and the consumable-IAP fallback. With no entitlement to
  check, **the product needs no server anywhere**.

Nothing here has been built. The repository is unchanged.

**The work itself is broken down in [ios-native-port-tasks.md](ios-native-port-tasks.md)** —
phased, checkable tasks with acceptance criteria, each pointing back at the section here that
explains it. This document holds the reasoning; that one holds the to-do list.

## Contents

- [1. The decision that drives everything else](#1-the-decision-that-drives-everything-else)
- [2. What exists today](#2-what-exists-today)
- [3. Architecture options](#3-architecture-options)
- [4. Porting the design system](#4-porting-the-design-system)
- [5. Porting the data model](#5-porting-the-data-model)
- [6. Feature-by-feature port map](#6-feature-by-feature-port-map)
- [7. Siri and App Intents](#7-siri-and-app-intents)
- [8. Local persistence, export and import](#8-local-persistence-export-and-import)
- [9. Supporting the app](#9-supporting-the-app)
- [10. Suggested phasing](#10-suggested-phasing)
- [11. Apple-side checklist](#11-apple-side-checklist)
- [12. Open decisions](#12-open-decisions)

---

## 1. The decision that drives everything else

Homassy is not a single-user app. Roughly a third of the current feature surface only
exists because two or more people share one household:

- Collaborative shopping lists with per-member attribution (`--member-color`, the
  "changed by" flash in `main.css`).
- Family chat (`FamilyChatHub`, `FamilyChatMessage`, read state, typing indicator,
  message references to products/list items) — **cut from the iOS app**, see §6.0.
- Live presence (`PresenceHub`, `HomePresenceStrip`, `PresenceAvatars`).
- Real-time inventory and master-data sync (`InventoryHub`, `MasterDataHub`), including
  the reconnect-diff `.row-updated-flash`.
- Join-by-share-code with an approval gate (`FamilyJoinRequest`).
- Insights: leaderboards, streaks, badges — all of them comparative between members.

A strictly local, device-only app deletes that third of the product. That is a legitimate
product decision, but it should be a deliberate one and not a side effect of "no server".

**There is a middle path that keeps it: CloudKit.** Apple's CloudKit gives you, at no
running cost to you and with no server to operate:

| CloudKit database | What it gives Homassy |
|---|---|
| Private DB | The user's own data, synced across their iPhone/iPad/Mac, backed by their iCloud |
| **Shared DB** | A record zone shared with other iCloud users — i.e. **the family**, invited by link |
| Public DB | Not needed here |

CloudKit push subscriptions (`CKDatabaseSubscription` + silent pushes) replace SignalR for
"a family member changed the list". Storage is billed against *each user's own* iCloud
quota, not yours. Sharing is invite-by-link, which maps almost exactly onto the current
share-code-plus-approval flow.

This is why the option table in §3 exists before anything else in this document.

---

## 2. What exists today

### 2.1 Stack being ported from

| Layer | Today |
|---|---|
| Frontend | Vue 3.5 + Nuxt 4.2, TypeScript, Pinia, Nuxt UI v4 (Radix), Tailwind v4 |
| Realtime | `@microsoft/signalr` against four hubs |
| Auth | Ory Kratos, passwordless email code + WebAuthn passkeys (`@simplewebauthn/browser`) |
| Backend | ASP.NET Core 10, EF Core, PostgreSQL 16, plus Email and Notifications microservices |
| i18n | `@nuxtjs/i18n`, three locales, **3270 lines each** (en / hu / de) |
| PWA | `@vite-pwa/nuxt` — standalone manifest, shortcuts, Web Share Target, app badge |

### 2.2 Screens (from `app/pages`)

`home`, `products/index`, `products/[publicId]`, `shopping-lists/index`, `calendar`,
`activity/index`, `insights/index`, `profile/index` and its nine sub-pages
(`automation` list + create + detail, `create-family`, `join-family`, `data`,
`external-calendars`, `products`, `shopping-lists`, `shopping-locations`,
`storage-locations`), `auth/{login,register,verify,recovery}`, `share` (share-target
landing), `offline`, `index` (public landing).

That is **27 routes**; a first native release will not have all of them (see §10).

### 2.3 Component inventory

110 Vue components. The shapes that matter for a port, because they are not
off-the-shelf UIKit/SwiftUI:

- **Drawer-first UI.** `AppDrawer`, `ProductFormDrawer`, `ShoppingListFormDrawer`,
  `SettingsEditDrawer`, `SettingsSelectDrawer`, `InventoryOperationsDrawer`,
  `NotificationCenterDrawer`, `WizardDrawer`, `VoiceItemDrawer`, … — nearly every
  create/edit path is a bottom sheet with drag-to-close (`useDrawerDragToClose`).
  SwiftUI `.sheet` + `.presentationDetents` is a near-exact match and is free.
- **Swipe actions** on list cards (`useSwipeActions`: pointer-based, axis lock, haptics,
  confirm-modal safety net) → SwiftUI `.swipeActions` natively, *but* the current design
  commits with an overshoot curve and an undo toast rather than a confirm dialog.
- **Undo window** (`UndoToast`, `useUndoableAction`, `undoQueue.ts`, `--undo-window: 5000ms`).
  No SwiftUI equivalent; port the queue as-is.
- **Shopping mode** (`ShoppingModeView`, full-screen, wake lock) → `.fullScreenCover` +
  `UIApplication.shared.isIdleTimerDisabled`.
- **Charts** (`ChartLine`, `ChartBar`, `ChartDonut`, `ChartCard`, hand-rolled SVG with
  draw-in animation) → Swift Charts; replace the stroke-dashoffset draw-in with a plain
  `.animation` on the series.
- **Command palette** (`CommandPalette`, `useCommandPalette`) → `.searchable` + a custom
  sheet; iOS has no ⌘K convention, so this is likely cut on phone and kept on iPad/Mac.
- **Section index rail** (`SectionIndexRail`) → a custom overlay; SwiftUI has no built-in
  equivalent of the UITableView section index.
- **Onboarding spotlight** (`OnboardingSpotlight`, z-tour on top of everything) →
  **TipKit** gets you 80% of it with far less code.
- **Maps** (`InteractiveMap`, `LocationMap`, currently MapLibre GL) → MapKit, which is
  strictly better here and removes a dependency.
- **Splash** (`SplashScreen.vue`, path-traced logo) → a launch storyboard cannot animate;
  do the trace in the first SwiftUI view instead.

### 2.4 Composables → what each becomes

| Composable | Native equivalent | Note |
|---|---|---|
| `useBarcodeScanner` (vue-qrcode-reader) | `VisionKit.DataScannerViewController` | Much better; EAN-13/8, UPC-A/E, Code-128 out of the box |
| `useCameraAvailability` | `AVCaptureDevice.authorizationStatus` | |
| `useGeolocation`, `useGeocoding` | `CoreLocation` + `CLMonitor` region monitoring | **Background geofencing becomes possible** — the PWA could only do foreground |
| `usePushNotifications` (WebPush/VAPID) | `UNUserNotificationCenter` | Local-only mode needs **no server at all** for expiration/automation alerts |
| `useHaptics` | `UIImpactFeedbackGenerator` / `.sensoryFeedback` | |
| `useWakeLock` | `isIdleTimerDisabled` | |
| `useSpeechRecognition`, `useVoiceItemMatching` | `SFSpeechRecognizer` (on-device mode) | |
| `useShareTarget` (Web Share Target) | **Share Extension** target | Text/image → shopping item or product; same UX, different plumbing |
| `useAppBadge` | `UNUserNotificationCenter.setBadgeCount` | |
| `useImageCrop` (vue-advanced-cropper) | `PHPickerViewController` + a crop view | |
| `browser-image-compression` | `ImageIO` / `UIGraphicsImageRenderer` | |
| `useWebAuthn` (passkeys) | `ASAuthorizationPlatformPublicKeyCredential*` | Only relevant if a server survives |
| `usePullToRefresh` | `.refreshable` | Free |
| `useSwipeActions` | `.swipeActions` (or custom, see §2.3) | |
| `useDeviceDetection`, `useBreakpoint` | `@Environment(\.horizontalSizeClass)` | |
| `useDateFormat`, `useInputDateLocale` | `Date.FormatStyle` | |
| `useEnumLabel` | String Catalog lookups | |
| `useMemberColor` | Port the hash-to-hue function verbatim | |
| `use*Socket` (inventory / shopping / chat / presence / master data) | CloudKit subscriptions, or dropped | The §1 decision |

### 2.5 Backend surface

21 controllers: `Auth`, `Automation`, `Calendar`, `ErrorCodes`, `ExternalCalendar`,
`FamilyChat`, `Family`, `Health`, `Insights`, `Internal`, `Location`, `Notification`,
`OpenFoodFacts`, `Product`, `Progress`, `Search`, `SelectValue`, `ShoppingList`,
`Statistics`, `User`, `Version`.

Only one of these was ever genuinely external — `OpenFoodFacts` (barcode → product metadata) —
and **it is cut** (§6.1). `ExternalCalendar` and `FamilyChat` are cut with it.

Everything remaining is CRUD, computation over the user's own rows, or realtime fan-out, and
all of it runs on-device. The consequence is worth stating plainly: **the iOS app makes no
third-party network calls at all.** Its only network traffic is CloudKit, i.e. Apple's own. That
simplifies the privacy manifest, the App Store privacy labels and the "data collected" answers
to something close to empty, and it means the app has no upstream that can break, rate-limit or
change its terms.

---

## 3. Architecture options

### Option A — Pure local

SwiftData (or Core Data) on device. No account, no network except Open Food Facts. Export /
import a document to iCloud Drive or anywhere via the document picker.

- **Keeps:** inventory, products, storage/shopping locations, expirations, calendar,
  automations, statistics, barcode scanning, notifications (all local), shopping mode.
- **Loses:** family, chat, presence, realtime, join requests, member attribution,
  leaderboards, shared lists. Insights degrade to personal-only.
- **Backup:** manual export only. If the phone is lost between exports, the data is gone.
  This is the risk to be honest about in the App Store description.
- **Cost to run:** zero.

### Option B — Local-first + CloudKit *(recommended)*

SwiftData with `ModelConfiguration(cloudKitDatabase:)`, or Core Data +
`NSPersistentCloudKitContainer`.

- **Keeps:** everything in A, plus cross-device sync, plus **family sharing** through a
  CloudKit shared record zone, plus realtime-ish updates via `CKDatabaseSubscription` and
  silent push.
- **Loses:** nothing structural. Chat over CloudKit is laggier than SignalR (push latency,
  not milliseconds) — acceptable for a household chat, not for a typing indicator. Presence
  is the one feature that genuinely does not fit and would be cut or reduced to
  "last seen".
- **Constraints to know before committing:**
  - CloudKit-backed SwiftData models **cannot use** `@Attribute(.unique)`, and every
    property must be optional or have a default. This changes the model definitions in §5.
  - Requires an iCloud account on device. No iCloud → local-only fallback container.
  - Apple-only. No Android or web client can ever read this data.
  - Schema changes are additive-only in production; plan migrations carefully.
- **Cost to run:** zero. Storage is on the user's iCloud quota.

### Option C — Native client over the existing backend

Keep Kratos + the API + Postgres, write a SwiftUI client against it.

- **Keeps:** 100% of today's behaviour, including SignalR (there is a Swift SignalR client,
  though it is community-maintained and thinner than the JS one).
- **Loses:** nothing functionally; loses the "no server, no running cost" goal entirely.
- **Cost to run:** the current VPS, forever, for every user.

### Recommendation — chosen

**B, with A as the no-iCloud fallback path inside the same binary**, and the export/import
document from A shipped in both, because it is also the answer to "let me leave" and to
Apple's data-portability expectations.

Then the subscription (§9) is *not* "backup" — CloudKit already backs up for free — it is
the cross-platform / off-Apple tier: server-side encrypted backup readable by a future
Android or web client, which is exactly what the current backend already is.

If instead you keep the paid tier as literally "online backup", price it against what it
actually adds over iCloud: a second copy under your control, restorable without an Apple ID.

### 3.4 What replaces login

Under Option B the app has **no login screen, no registration, no email OTP, no passkeys and
no Kratos**. The whole `auth/` route group (`login`, `register`, `verify`, `recovery`) and the
`AuthController` have no native counterpart. First launch goes straight to the app.

Identity comes from the iCloud account the device is already signed into. The app never sees a
password, never stores a credential, and never has an account database to breach.

**How each thing that needed an account is served instead:**

| Needed an account for | Under CloudKit |
|---|---|
| Knowing whose data this is | `CKContainer.default().userRecordID` — stable per container, per Apple ID |
| Syncing across the user's devices | Automatic: the private database follows the Apple ID |
| Joining a family | A `CKShare` on the household record zone, sent as a link |
| Knowing who changed a shopping item | `CKShare.Participant`, plus the app's own `Member` record (below) |
| Recovering after losing the phone | Sign into iCloud on the new phone; the data is already there |

**Family, concretely.** One member's device owns a custom record zone holding the household's
data. Sharing it is `UICloudSharingController` (or `ShareLink` with a `CKShare` transferable),
which produces an ordinary link the owner sends however they like — Messages, WhatsApp, a QR
code on the fridge. The recipient taps it and the system hands the app the invitation through
`userDidAcceptCloudKitShareWith` on the scene delegate; the app calls
`CKAcceptSharesOperation` and the zone appears in their shared database.

That single flow replaces all of `Family`, `FamilyJoinRequest`, the share code, the approval
gate, and the notifications around them. The approval is implicit — the owner chose to send the
invite — and access is revoked by removing the participant, which the same controller does.
Set the share's `publicPermission` to `.none` so only explicitly invited participants can join;
an anyone-with-the-link share would be strictly worse than today's approval gate.

**Display names and member colours.** Do not rely on CloudKit for these. A participant's real
name is only visible subject to iCloud discoverability rules, and it is the user's Apple ID
name, not what they want their family to call them. Keep the app's own `Member` record in the
shared zone, keyed by `userRecordID`, carrying a display name and an avatar the user sets on
first join. That record is also what `useMemberColor`'s hash feeds on, so attribution survives
unchanged.

**The cases that must still be handled.** `CKContainer.accountStatus()` returns
`.noAccount`, `.restricted` or `.temporarilyUnavailable` often enough to matter — a child
account, a wiped device mid-setup, a user who simply does not use iCloud. The app opens a
**local-only store** in those cases and keeps working (Option A, inside the same binary), with
a banner offering to enable sync. What it must not do is block the first launch behind an
iCloud requirement.

**What is genuinely lost with no login:** the web app can never be the same account. A browser
has no access to a user's CloudKit private database. If the web app keeps living (§12.4), the
two are separate products sharing a data *format*, not an account — and the bridge between
them becomes the paid tier's job (§9.3), which is the one place a real login comes back.

### 3.5 Who is who, with no accounts

Worth separating, because the two halves have very different trust properties.

**The identity is CloudKit's, and it cannot be forged.** Every record in a shared zone carries
`creatorUserRecordID` and `lastModifiedUserRecordID`, and **the server sets them**, not the
client. So "Anna ticked off the milk" is not something a device asserts — it is a fact CloudKit
stamped on the record. That is a stronger guarantee than the current web app has, where
attribution is whatever the authenticated API caller wrote into the row.

`userRecordID` is stable for a given Apple ID within a given container, so it is the join key
for everything below. It is also opaque: it carries no name, no email, nothing personal.

**The name and the face are the app's own, and are self-declared.** Keep a `Member` record in
the shared zone:

```
Member
  userRecordID   (the CloudKit id — the key)
  displayName    "Anna"        set by that person, on first join
  avatarHash     <content hash>, image in the shared zone or a CKAsset
  colorSeed      what useMemberColor's hash currently derives from PublicId
  joinedAt
```

First launch after accepting an invitation asks for a name and optionally a photo, writes this
record, and that is the household's onboarding. It is one screen, and it replaces registration.

Self-declared is the right call here and matches today's behaviour — the web app already lets
users pick their own profile name. A household of four people who invited each other by link
does not need identity proofing; it needs to tell Anna's row from Béla's.

**What CloudKit will tell you about a participant** (`CKShare.Participant`), and what it will
not:

- Always available: `userIdentity.userRecordID`, `role` (owner / private user), `permission`
  (read / read-write), `acceptanceStatus` (invited / accepted / removed).
- Only sometimes: `userIdentity.nameComponents` and the contact identifier — these depend on
  iCloud discoverability and on the participant having been looked up by email or phone. Treat
  them as a *nicety for prefilling the name field*, never as the source of truth. An app that
  shows blanks where names should be, because half the household is not discoverable, looks
  broken.

**So the member list on screen** is the share's participant list (authoritative: who has
access, in what role, invited or accepted) joined to the `Member` records (cosmetic: name,
avatar, colour). A participant who has accepted but not yet written a `Member` record shows as
"New member" until their first launch completes.

**Removing someone** is removing them from the `CKShare` — the zone disappears from their
device. Their past attributions remain, because `lastModifiedUserRecordID` is on the records;
keep the departed member's `Member` record so old rows still render a name rather than a blank.

**The one real gap versus today:** there is no server-side notion of "this person is a member
of family X" that a background job could check, because there is no server. Anything the
current backend does by looking across a family — the weekly summary email, the join-request
alerts — either moves onto a device (a member's own phone computes and notifies locally) or
goes away. §6 marks which is which.

### 3.6 Where the data physically lives, and what happens to the backend

**Everything is in iCloud. The backend serves no part of the free app.**

For the iOS app under Option B, the current stack — `Homassy.API`, PostgreSQL, Kratos,
`Homassy.Email`, `Homassy.Notifications`, the Caddy proxy, the VPS — has **no role at all**.
Not a reduced role: none. Every controller in §2.5 is replaced by on-device code, except
Open Food Facts, which the device calls directly.

The backend keeps running only for two reasons, both optional:

1. **The web app**, if it keeps living (§12.4). That is a separate product decision; the iOS
   app does not need it.
2. **The paid tier** (§9.3), if it ships — and that is a much smaller service than today's:
   Sign in with Apple, an encrypted blob store, and App Store entitlement checks.

If the web app is retired and the paid tier is deferred, the VPS can be switched off entirely.

**Physically, the data sits in two places:**

| Data | Lives in | Counts against |
|---|---|---|
| A user's personal data (private zone) | That user's iCloud | That user's iCloud quota |
| The household's shared data | The **zone owner's** iCloud | The **owner's** quota only |
| Images | Same zones, as `CKAsset` or in the shared file container | Same |

Two consequences that matter and are easy to miss:

- **Participants cost the owner nothing extra in their own quota, but the owner carries all of
  it.** For a household inventory this is small — text rows plus compressed product photos — so
  it is unlikely ever to be a problem. Keep images compressed anyway (§6), because that is
  someone else's storage being spent.
- **The owner is a single point of failure.** CloudKit has no way to transfer ownership of a
  shared zone. If the owner deletes the app, wipes their iCloud, or leaves the household, the
  zone goes with them and every participant loses access. This is the sharpest edge in Option
  B, and it has no clean platform-level fix. Mitigations, in order of value:
  1. Every member's device keeps a **local copy** of the shared data it has seen — which
     SwiftData/Core Data does anyway, so this is mostly about not deleting it on share loss.
  2. Prompt a **periodic export** (§8) on participant devices, not just the owner's.
  3. If the owner's share disappears, offer "re-create the household from my local copy" —
     the new owner re-shares, everyone re-joins, `publicId`s make the merge sane.
  4. There is **no paid backup to fall back on** — the subscription was dropped (§9). So
     mitigations 1–3 are the whole answer, and **export (§8) is the app's only real safety
     net.** Prompt it; do not bury it in settings.

  Say this in the UI in one line when a household is created, rather than letting someone
  discover it.

### 3.7 Sharing with anyone — not just an iCloud Family

**`CKShare` has nothing to do with Apple's Family Sharing.** They are unrelated features with
confusingly similar names. A `CKShare` is a share of one record zone between arbitrary iCloud
accounts: two flatmates, a couple, a parent and an adult child in another city, three friends
who cook together. No family group, no shared payment method, no organiser, no age rules, and
no requirement that anyone be related or live together.

The flow is the same regardless: the owner sends a link, the recipient taps it, they are in.
CloudKit allows up to 100 participants per share, which is about 98 more than this app needs.

What this actually *improves* on the current model:

- Today, `User.FamilyId` is a single nullable column: **one household per person, ever.**
  Under CloudKit a user can be a participant in several shared zones at once — their own flat
  and their parents' house, say — and the app can let them switch. Worth designing the data
  layer for from the start even if the first release ships a single household, because it is a
  schema decision, not a UI one.
- Per-participant permissions are built in (`.readOnly` vs `.readWrite`). A child, a house
  guest or a cleaner can be given a read-only shopping list without a new concept in the data
  model.

**Where Apple's Family Sharing *is* relevant:** exactly one place, and it is the subscription.
If the paid tier ships, App Store Connect can mark it **Family Shareable**, so one purchase
covers the buyer's Apple Family group. That is a pricing decision, unrelated to how household
data is shared, and the two groups need not contain the same people.

**Naming.** Given all of the above, "family" is now the wrong word in the UI. The thing being
shared is a *household*, and its members may be friends or flatmates. Worth renaming in the
iOS strings — and, if the web app keeps living, worth considering there too. The C# entity can
keep its name; the user-facing word should change.

---

## 4. Porting the design system

### 4.1 Colour

The palette is already tokenised in `Homassy.Web/app/assets/css/main.css`. Transcribed:

```swift
// DesignTokens/Palette.swift
extension Color {
    // The `mocha` primary ramp — identical values to --color-mocha-* in main.css.
    static let mocha50  = Color(hex: 0xF5F0EB)
    static let mocha100 = Color(hex: 0xEFE9E0)
    static let mocha200 = Color(hex: 0xE0D5C7)
    static let mocha300 = Color(hex: 0xD4C7B5)
    static let mocha400 = Color(hex: 0xC9B8A0)   // == manifest theme_color
    static let mocha500 = Color(hex: 0xB8956A)
    static let mocha600 = Color(hex: 0xA0825B)
    static let mocha700 = Color(hex: 0x8B7355)
    static let mocha800 = Color(hex: 0x6F5A44)
    static let mocha900 = Color(hex: 0x5A4536)
    static let mocha950 = Color(hex: 0x3F3027)
}
```

Nuxt UI maps `primary: 'mocha'`, `neutral: 'slate'`, `success: 'mocha'` (`app.config.ts`).
Semantic tokens (`--ui-primary`, `--ui-bg`, …) resolve to a different step of the ramp in
light and dark mode. In Xcode, put each **semantic** token in the asset catalog as a Color
Set with Any/Dark appearances rather than hardcoding a ramp step at call sites — that is the
direct analogue of what Nuxt UI is doing, and it keeps the dark theme a data change.

Semantic sets to create: `bg`, `bg-elevated`, `bg-muted`, `border`, `border-accented`,
`text`, `text-muted`, `text-dimmed`, `primary`, `primary-muted`, `success`, `warning`,
`error`, `info`. Read the resolved light/dark values out of the running web app with
devtools once and paste them in — do not re-derive them by eye.

**Rule already in force in the web app, keep it:** a member's colour is only ever an accent
(a dot, a ring), never a fill or a text colour, because member hues are arbitrary and would
break contrast. See the `.item-attribution-label` comment in `main.css`.

### 4.2 Typography

`--font-sans: 'Public Sans'`, delivered by `@nuxt/fonts` with metric-adjusted fallbacks.
Ship Public Sans in the bundle (it is OFL) and register it in `Info.plist` under
`UIAppFonts`. Then define text styles that track Dynamic Type:

```swift
extension Font {
    static func publicSans(_ style: Font.TextStyle, weight: Font.Weight = .regular) -> Font {
        .custom("PublicSans", size: style.defaultSize, relativeTo: style).weight(weight)
    }
}
```

The web app sets `text-wrap: balance` on headings and `pretty` on body copy. SwiftUI has no
equivalent. Accept the loss; it is cosmetic.

### 4.3 Motion

Every duration and curve in the app is already a named token. Port them as a single enum so
the native app has the same one visible home the CSS file has:

```swift
enum Motion {
    static let bubbleIn      = Duration.milliseconds(280)
    static let bubbleOut     = Duration.milliseconds(180)
    static let bubbleMove    = Duration.milliseconds(260)
    static let bubbleStagger = Duration.milliseconds(28)

    // cubic-bezier(0.34, 1.56, 0.64, 1) — the pop/overshoot curve
    static let pop = Animation.spring(response: 0.28, dampingFraction: 0.62)
    // cubic-bezier(0.22, 1, 0.36, 1) — decelerating settle
    static let settle = Animation.timingCurve(0.22, 1, 0.36, 1, duration: 0.28)

    static let attributionFlash    = Duration.milliseconds(1500)
    static let realtimeReconnected = Duration.milliseconds(2500)
    static let undoWindow          = Duration.seconds(5)
}
```

Reduced motion: the CSS neutralises every animation under `prefers-reduced-motion` while
**keeping the end state visible** (the flash ring stays, the badge stamp stays, only the
burst is removed). Reproduce that discipline with
`@Environment(\.accessibilityReduceMotion)` — same rule: the motion goes, the information
stays.

### 4.4 Layout and stacking

The web app's z-index ladder exists because of browser stacking contexts. **It does not
port.** SwiftUI presentation is a stack of sheets/covers/overlays and the ordering is
structural. What does port is the *intent*:

| Web token | Native |
|---|---|
| `--z-header` / `--z-nav` | `NavigationStack` toolbar / `TabView` |
| `--z-fullscreen` (shopping mode) | `.fullScreenCover` |
| `--z-chat-*` | — chat is cut (§6.0), so this tier disappears |
| `--z-toast`, `--z-bottom-toast` | One overlay container at the root, above the tab bar |
| `--z-lightbox` | `.fullScreenCover` with a zoomable image |
| `--z-tour` | TipKit |

Safe-area maths (`--app-nav-inset`, `--app-fab-inset`, `--app-bottom-slot`) is all
compensation for the browser not owning the chrome. Delete it; SwiftUI's safe area does
this correctly by construction. The one thing to keep is that the undo toast must sit
**above** the tab bar and above shopping mode — the comment in `main.css` explains why
(most undoable purchases happen inside shopping mode).

### 4.5 The bottom-nav + FAB pattern

Today: a floating bottom bar with a centre FAB (`NavFab`, `useFabActions`), hidden from
`lg` where navigation moves to a sidebar.

On iOS, a centre FAB over a tab bar is not a native pattern and will read as an Android
port. Two options: (a) keep it, accept the non-native feel, gain brand continuity;
(b) move the FAB's actions into a per-tab toolbar `+` menu and keep a plain `TabView`.
Recommend (b) for phone, and `NavigationSplitView` on iPad — which is what the `lg`
sidebar already is.

---

## 5. Porting the data model

### 5.1 Entity map

Source: `Homassy.Data/Entities`. Every entity inherits `BaseEntity` (int `Id`, GUID
`PublicId`) → `SoftDeleteEntity` (`IsDeleted`) → `RecordChangeEntity` (`RecordChange` JSON).

| C# entity | SwiftData model | Notes for the port |
|---|---|---|
| `Product` | `Product` | Name, Brand, `ProductCategory?`, `Unit`, Barcode, `IsEatable`, picture version |
| `ProductCustomization` | `ProductCustomization` | Per-family overrides of a shared product |
| `ProductInventoryItem` | `InventoryItem` | The core row: product, quantity, unit, `expirationAt`, storage location, consumed flags |
| `ProductConsumptionLog` | `ConsumptionLog` | Feeds statistics and insights |
| `ProductPurchaseInfo` | `PurchaseInfo` | Price + currency + store; feeds `BestPriceBadge` / `PriceHistoryCard` |
| `ProductImage`, `FamilyPicture`, `UserProfilePicture`, `FamilyChatImage` | files on disk + a `version` hash | **Do not** put image bytes in SwiftData; store in Application Support and keep the hash, exactly as `ProductPictureVersion` does today |
| `ItemAutomation`, `ItemAutomationExecution` | `Automation`, `AutomationRun` | Schedule + action type + execution audit |
| `ShoppingList`, `ShoppingListItem` | same | |
| `ShoppingLocation`, `StorageLocation` (`LocationBase`) | `Location` with a `kind`, or two models | The C# side uses inheritance off `LocationBase` |
| `Family`, `FamilyJoinRequest` | **Dropped** | The household *is* the shared record zone; a `CKShare` invite replaces the share code, the request and the approval gate (§3.4) |
| — (new) | `Member` | Maps `userRecordID` → display name, avatar, colour seed. The app's answer to "who is who" without accounts (§3.5) |
| `FamilyChatMessage` + `Image` + `Reference` + `ReadState` | **Cut** | §6.0 |
| `CalendarNote` | `CalendarNote` | Stays. `FamilyExternalCalendar` and `ExternalCalendarReminderDispatch` are **cut** (§6.0) |
| `Activity` | `ActivityEntry` | The feed; in local-only mode it is a personal audit log |
| `User`, `UserProfile`, `UserNotificationPreferences` | `UserDefaults` / a single settings model, plus `Member` above | There is no login (§3.4): identity is the iCloud account, the profile is the `Member` record, and what remains local is preferences |
| `UserBadge` | `Badge` | `justUnlocked` is currently a server-once guarantee — on device it becomes a local flag, so guard the burst animation against re-firing |
| `UserPushSubscription` | — | Deleted: local notifications need no subscription |
| `UserNotification` | `NotificationEntry` | The in-app notification centre survives; it is just a local table now |

### 5.2 CloudKit constraints (Option B)

```swift
@Model
final class InventoryItem {
    // CloudKit-backed stores forbid @Attribute(.unique) and require
    // every property to be optional or carry a default value.
    var publicId: UUID = UUID()
    var quantity: Double = 0
    var unit: Unit = Unit.piece
    var expirationAt: Date?
    var isFullyConsumed: Bool = false
    var fullyConsumedAt: Date?

    var product: Product?
    var storageLocation: Location?
    @Relationship(deleteRule: .cascade) var consumptionLogs: [ConsumptionLog]? = []

    init() {}
}
```

Uniqueness on `publicId` must therefore be enforced in code (fetch-before-insert, or a
last-write-wins merge), not by the store. The existing `PublicId` GUID is the right key to
carry over — it already keeps internal integer ids out of the wire format.

Soft delete (`IsDeleted`) is worth keeping in Option B: it is how two devices that both
edited offline avoid resurrecting each other's deletions. In Option A, hard delete is fine.

**One thing Phase 0 must settle before any modelling work: SwiftData or Core Data.**
SwiftData's CloudKit support covers the *private* database well, but `CKShare` — the API the
entire family feature in §3.4 rests on — has historically been exposed only through Core Data's
`NSPersistentCloudKitContainer` (`share(_:to:)`, `acceptShareInvitations(from:into:)`), with no
SwiftData equivalent. If that is still true on the target OS version, the choice is:

- **Core Data + `NSPersistentCloudKitContainer`** — less pleasant to write, but sharing is a
  supported API rather than something to hand-roll. Recommended default given that sharing is
  the entire reason Option B was chosen.
- **SwiftData for the models, Core Data interop for the share** — they can address the same
  store, but the seam is a known source of trouble.
- **SwiftData plus raw CloudKit** for the shared zone — most control, most work.

This is a factual question about the current SDK, not a judgement call: check it first in the
Phase 0 spike (§10) and let the answer pick the persistence layer. Building the model layer on
SwiftData and discovering afterwards that it cannot share is the most expensive mistake
available in this project.

### 5.3 Enums

24 enums in `Homassy.Data/Enums`. Most are small and mechanical. Two need attention:

- **`ProductCategory` — 947 members**, organised in thematic number blocks, and it is the
  canonical source that `npm run sync:product-category` generates the web enum, the group
  map and all three locale files from. For iOS, extend that same script to emit a Swift
  file and a String Catalog fragment. Hand-maintaining a second copy of 947 categories will
  rot within a month.
- **`ErrorCode`** exists to give the client stable error identities. In local-only mode most
  of it disappears; keep the validation subset.

`Unit`, `Currency`, `StoreType`, `Language`, `UserTimeZone`, `DaysOfWeek`, `ScheduleType`,
`ActivityType`, `NotificationType`, `BarcodeFormat`, `ImageFormat` port as plain Swift
enums with `String` raw values — not `Int`, because raw ints in a synced store turn any
later reordering into a data migration.

### 5.4 Localisation

3270 lines × 3 locales. The port target is an Xcode **String Catalog** (`.xcstrings`).
Write a one-off script that flattens the nested JSON keys (`products.form.name.label`) into
catalog keys and emits the three languages in one pass; do not retype them. Keep the key
shape identical so the two apps stay diffable.

Plurals: `@nuxtjs/i18n` pluralisation and String Catalog plural variations are not the same
syntax — those entries need review by hand. Hungarian has one plural form, English and
German have two, so the conversion is mostly mechanical but not entirely.

---

## 6. Feature-by-feature port map

### 6.0 Cut before it starts

Three features are **out of scope for the iOS app** by decision, not by technical constraint.
Each one takes a surprising amount with it, which is the point of listing it here rather than
leaving a "No" in the table below.

**Family chat — cut.**
Removes: `FamilyChatHub`, `FamilyChatMessage`, `FamilyChatImage`,
`FamilyChatMessageReference`, `FamilyChatReadState`, `FamilyChatController`, seven components
(`FamilyChatBubble`, `Composer`, `MessageGroup`, `Panel`, `ReferencePicker`, `Stream`,
`TypingIndicator`), three composables (`useFamilyChat`, `useFamilyChatBubble`,
`useFamilyChatSocket`), four z-ladder tokens and the `chat-panel-grow` keyframes.

Three things fall out of this, all good:

1. **The storage problem disappears.** Chat images were the only unbounded image source in the
   app (1600 px / 1 MB each, on the household owner's quota — §9.4). Without them, what remains
   is product photos at 30–80 KB and a handful of avatars, which is why images can stay in
   CloudKit.
2. **The worst CloudKit fit is gone.** Chat needed sub-second delivery and a typing indicator;
   CloudKit push latency was never going to serve it well. Every remaining shared feature
   tolerates seconds.
3. The floating bubble was the one piece of chrome with no native idiom (§4.4), so the
   stacking design gets simpler too.

Households that want to talk to each other have iMessage and WhatsApp already. What Homassy
needs to carry is *attribution* on shared items — "Anna ticked this off" — and that stays.

**External calendar subscriptions — cut.**
Removes: `FamilyExternalCalendar`, `ExternalCalendarReminderDispatch`,
`ExternalCalendarController`, `useExternalCalendarApi`, `profile/external-calendars.vue`,
`DataExternalCalendarCard`, `ExternalCalendarReminderSettings`, and the ICS parsing and
reminder-dispatch machinery behind them.

The **calendar screen itself stays** — it is expirations, shopping-list deadlines and
`CalendarNote`, all of which are the app's own data. Only the "subscribe to somebody else's ICS
feed" feature goes. If that itch ever returns, `EventKit` is the native answer and it is a much
smaller feature than the current one.

**Open Food Facts lookup — cut.**
Removes: `OpenFoodFactsController`, `useOpenFoodFactsApi`, and with it the app's last
third-party network dependency (§2.5).

This one has a consequence worth deciding deliberately rather than discovering later:
**it does not cut barcode scanning, but it changes what scanning is for.** Today, scanning an
unknown barcode fetches a name, brand and category. Without the lookup, scanning a barcode the
household has never seen produces a number and nothing else.

The design that works without any lookup, and is assumed from here on:

- `Product.Barcode` already exists on the entity. Scanning matches against the **household's
  own catalogue** first.
- **Known barcode** → jumps straight to that product: add to inventory, add to the list, check
  stock. This is the common case, because households re-buy the same things.
- **Unknown barcode** → opens the product form with the barcode prefilled; the user types the
  name once. Every later scan of that barcode is then instant.

That is a coherent, fully offline feature and arguably a better fit for a household app than a
food database that is patchy outside Western Europe. It is worth saying out loud, though, that
first-time entry becomes manual typing, which is the one place the cut is felt.

### 6.1 The rest

| Feature | Local-only viable? | Native notes |
|---|---|---|
| Product inventory + consumption | Yes | Core of the app |
| Storage / shopping locations | Yes | MapKit replaces MapLibre |
| Shopping lists | Yes (personal) | Sharing needs CloudKit |
| Shopping mode (full screen, wake lock) | Yes | Straight port |
| Barcode scanning | Yes | **VisionKit `DataScannerViewController`**. Matches against the household catalogue, not a food database — see §6.0 |
| Open Food Facts lookup | — | **Cut** (§6.0). The app makes no third-party network calls |
| Expiration alerts (7 AM, 14-day horizon) | Yes | `UNCalendarNotificationTrigger`, scheduled locally. **No server needed.** iOS caps pending local notifications at 64 — schedule a rolling window, not one per item |
| Weekly summary (Mondays) | Yes | Same mechanism |
| Automations (auto-consume, reminders, list-add, low stock) | Yes | `BGAppRefreshTask` + local notifications. Background execution is opportunistic — design for "runs late" and recompute on foreground |
| Geofenced "items to buy here" | Yes, **and better** | `CLMonitor` region monitoring works in the background, which the PWA could never do. 20-region system limit: monitor the nearest N stores |
| Calendar (expirations + deadlines + notes) | Yes | Stays. Consider also writing to `EventKit` so it shows in the system calendar |
| External calendar subscriptions (ICS) | — | **Cut** (§6.0) |
| Activity feed | Yes (personal) | |
| Statistics | Yes | Compute on device; the nightly server cache is unnecessary at single-household scale |
| Insights: badges, streaks | Yes | Leaderboards need family |
| Family chat | — | **Cut** (§6.0). Takes the unbounded image source with it |
| Presence | **No** | Drop, or reduce to "last active" |
| Realtime list sync | **No** | CloudKit subscriptions |
| Join family by code | **No** | Replaced by a `CKShare` invite link |
| Push notifications from other members | **No** | CloudKit silent push → local notification |
| Passkeys / email-code login | **Deleted** | No login anywhere in the free app; identity is the iCloud account (§3.4)  |
| Share target (text/image → item) | Yes | **Share Extension** target in the Xcode project |
| App icon badge (expiring count) | Yes | `setBadgeCount` |
| Home-screen shortcuts | Yes | **App Intents** + `AppShortcutsProvider` — and the same declaration drives Siri, Spotlight and the Action Button. See §7  |
| Widgets | New capability | Expiring-soon and shopping-list widgets are the highest-value native-only addition |
| Voice item entry | Yes | `SFSpeechRecognizer`, on-device, **`hu-HU` supported** — this is the Hungarian voice path, because Siri has no Hungarian (§7.9)  |
| Onboarding tour | Yes | TipKit |
| Image crop + compress | Yes | PhotoKit + ImageIO |

---

## 7. Siri and App Intents

Voice is a requirement for this port, not a late extra. The two moments it earns its keep are
exactly the two where the phone is in a pocket and the hands are not free: putting something on
the list while cooking, and asking what still needs buying while standing in the shop. Both are
already half-built — `VoiceItemDrawer` and `useVoiceItemMatching` exist in the web app — so what
follows is mostly about wiring, not new logic.

### 7.1 What the user must be able to say

**Write** — hands busy in the kitchen:

- "Add milk to Homassy"
- "Add two litres of milk to Homassy"
- "Add milk to the weekly shop in Homassy"
- "Mark bread as bought in Homassy"
- "I used up the eggs in Homassy"

**Read** — hands busy in the shop, or in front of the fridge:

- "What do I need to buy in Homassy?"
- "What's expiring in Homassy?"
- "How much milk do I have in Homassy?"

Every phrase contains the app name. That is not a stylistic choice — see §7.3.

### 7.2 Framework choice

**App Intents** (iOS 16+) is the framework to build on. It is the same declaration that feeds
Siri, Spotlight, the Shortcuts app, the Action Button, Control Center and widgets, so one intent
definition serves all of them.

Two things worth knowing before committing:

- **The legacy alternative is SiriKit's Lists and Notes domain** (`INAddTasksIntent`,
  `INSetTaskAttributeIntent`, …). Its one advantage is real: it lets Siri route *generic*
  phrasing — "add milk to my shopping list", with no app name — because the system owns the
  vocabulary. Its disadvantages are that it is the old framework, it constrains the data model to
  Apple's task shape, and it gives nothing to Spotlight or widgets. Recommendation: App Intents,
  and revisit SiriKit only if generic phrasing turns out to matter more than everything else.
- **iOS 18's assistant schemas** (`@AssistantIntent`) cover mail, photos, files, browser, camera,
  documents, spreadsheets, presentations, whiteboards and journals. There is **no list or
  inventory domain**, so Homassy uses plain App Intents and gets no deeper Apple Intelligence
  integration for free.

### 7.3 The app-name constraint

Apple requires every App Shortcut phrase to contain the app name, via the `\(.applicationName)`
token. "Add milk to my shopping list" on its own will never reach Homassy; "Add milk to Homassy"
will.

Consequences to design around:

- The app name must be short and speakable. "Homassy" is two syllables and phonetically
  unambiguous in English and German — it holds up.
- Users can add their own alias in Settings → Siri → App Names, so a user who finds the name
  awkward is not stuck. Mention that in onboarding rather than hoping they discover it.
- The system caps the number of App Shortcuts per app (10 at time of writing), so the set in §7.5
  must be chosen, not accumulated. Everything else lives in the Shortcuts app as an intent the
  user can wire up themselves, which has no such limit.

### 7.4 Entities

Siri needs to refer to the app's own nouns. Each becomes an `AppEntity` backed by the existing
`PublicId` GUID:

```swift
struct ShoppingItemEntity: AppEntity {
    static var typeDisplayRepresentation: TypeDisplayRepresentation = "Shopping item"
    static var defaultQuery = ShoppingItemQuery()

    let id: UUID                      // the existing PublicId — see §5.2
    @Property(title: "Name") var name: String
    var listName: String
    var quantity: String?

    var displayRepresentation: DisplayRepresentation {
        DisplayRepresentation(title: "\(name)", subtitle: "\(listName)")
    }
}

struct ShoppingItemQuery: EntityStringQuery {
    func entities(for identifiers: [UUID]) async throws -> [ShoppingItemEntity] { … }

    // What Siri calls when the user said a name rather than picked from a list.
    func entities(matching string: String) async throws -> [ShoppingItemEntity] {
        ItemMatcher.candidates(for: string, locale: .current)   // see §7.6
    }

    func suggestedEntities() async throws -> [ShoppingItemEntity] { … }
}
```

Entities to define: `ShoppingListEntity`, `ShoppingItemEntity`, `ProductEntity`,
`StorageLocationEntity`, `ShoppingLocationEntity`. `ProductEntity` should also conform to
`EntityPropertyQuery`, which is what puts individual products into Spotlight (§7.10).

### 7.5 The intents to ship

| Intent | Spoken example | Key parameters | Returns |
|---|---|---|---|
| `AddShoppingItemIntent` | "Add milk to Homassy" | item name, quantity?, unit?, list? | dialog confirmation |
| `CompleteShoppingItemIntent` | "Mark bread as bought in Homassy" | item entity | dialog + remaining count |
| `ReadShoppingListIntent` | "What do I need to buy in Homassy?" | list? | dialog + snippet |
| `ReadExpiringIntent` | "What's expiring in Homassy?" | horizon (default 7 days) | dialog + snippet |
| `CheckStockIntent` | "How much milk do I have in Homassy?" | product entity | dialog |
| `ConsumeInventoryIntent` | "I used up the eggs in Homassy" | inventory item, amount? | dialog |
| `StartShoppingModeIntent` | "Start shopping in Homassy" | list? | opens the app in shopping mode |
| `ScanBarcodeIntent` | "Scan a barcode in Homassy" | — | opens the app on the scanner |

Only the last two set `openAppWhenRun = true`. Everything else must complete without the app
coming to the foreground — that is the whole point.

```swift
struct AddShoppingItemIntent: AppIntent {
    static var title: LocalizedStringResource = "Add to shopping list"
    static var description = IntentDescription("Adds an item to a Homassy shopping list.")
    static var openAppWhenRun = false          // the phone stays in the pocket

    @Parameter(title: "Item", requestValueDialog: "What should I add?")
    var itemName: String

    @Parameter(title: "Quantity")
    var quantity: Double?

    @Parameter(title: "List")
    var list: ShoppingListEntity?

    static var parameterSummary: some ParameterSummary {
        Summary("Add \(\.$itemName) to \(\.$list)") { \.$quantity }
    }

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let store  = try HomassyStore.shared               // App Group container — see §7.8
        let target = try list.map(store.list(for:)) ?? store.defaultShoppingList()
        let match  = ItemMatcher.match(itemName, locale: .current)   // see §7.6
        let item   = try store.addItem(match, quantity: quantity, to: target)
        return .result(dialog: "Added \(item.name) to \(target.name).")
    }
}
```

And the shortcut declarations that expose them to Siri:

```swift
struct HomassyShortcuts: AppShortcutsProvider {
    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: AddShoppingItemIntent(),
            phrases: [
                // Every phrase MUST contain \(.applicationName) — see §7.3.
                "Add to \(.applicationName)",
                "Add \(\.$itemName) to \(.applicationName)",
                "Put \(\.$itemName) on the \(.applicationName) list"
            ],
            shortTitle: "Add item",
            systemImageName: "cart.badge.plus"
        )
        AppShortcut(
            intent: ReadShoppingListIntent(),
            phrases: [
                "What do I need to buy in \(.applicationName)",
                "Read my \(.applicationName) shopping list"
            ],
            shortTitle: "Read list",
            systemImageName: "list.bullet"
        )
    }
}
```

Phrases are localised in a dedicated `AppShortcuts.xcstrings` catalog, separate from the app's
own strings — and only into languages Siri actually speaks (§7.9).

### 7.6 Matching a spoken name — reuse what already exists

`Homassy.Web/app/composables/useVoiceItemMatching.ts` already solves this, and its reasoning is
worth preserving verbatim. Three rungs, in order:

1. **An existing product**, because a household buys the same things over and over.
2. **The `ProductCategory` vocabulary** — 947 localised words, the best guess for something this
   family has never bought before.
3. **The raw words**, as a free-text item.

It also strips Hungarian noun endings longest-first (`HU_SUFFIXES`: `okat`, `eket`, `at`, `et`,
`ot`, `k`, `t`, …) so that "két liter tejet" can find "tej". English and German barely inflect
and need none of this; Hungarian puts the sentence's grammar on the end of the noun, so without
it the matcher finds nothing.

Port this as a Swift `ItemMatcher` in a shared framework target, used by **both** the App Intent
and the in-app dictation screen. Two copies of this logic will drift.

One addition the intent needs that the drawer did not: when rung 1 returns more than one
plausible product, do not guess. Throw `$itemName.needsValueError(…)` or return the candidates
from the `EntityStringQuery` so Siri asks "Which one?" — the web version could afford to guess
because the result landed in an editable row the user confirmed; a voice flow has no such row.

### 7.7 Reading the list back

Siri **speaks** the dialog string, so it has to be written for the ear:

```swift
func perform() async throws -> some IntentResult & ProvidesDialog & ShowsSnippetView {
    let items = try HomassyStore.shared.pendingItems(in: list)
    guard !items.isEmpty else {
        return .result(dialog: "Nothing on the list.", view: EmptyListSnippet())
    }
    // Reading out 22 item names is useless. Three plus a count is what a person would say.
    let head = items.prefix(3).map(\.name).formatted(.list(type: .and))
    let dialog: IntentDialog = items.count > 3
        ? "\(items.count) items. \(head), and \(items.count - 3) more."
        : "\(head)."
    return .result(dialog: dialog, view: ShoppingListSnippet(items: items))
}
```

The snippet view is a small SwiftUI view Siri renders under the spoken answer — that is where the
full list goes. On iOS 18+ snippets can be **interactive**, so the snippet should carry a tick
button per row wired to `CompleteShoppingItemIntent`: ask what to buy, then tick items off, all
without opening the app. That is the single best voice interaction available here and it is worth
building properly.

### 7.8 Where intents run — and why it changes §8.1

App Intents execute outside the app's own process. Two consequences that have to be designed in
from the start, not retrofitted:

- **The store must live in an App Group container**, not the app's private Application Support
  directory. That changes the layout in §8.1: `Homassy.store` and `Images/` move to
  `group.hu.kellner.homassy/`. Retrofitting this later means migrating every user's store.
- **The intent must work with the app never having been foregrounded**, offline, and with no
  network. Write to the local store and let CloudKit sync when it can (§3, Option B). An intent
  that needs the network to confirm an addition will fail in exactly the basement-supermarket
  situation it exists for.
- Keep `perform()` fast — a couple of seconds at most. No image work in the intent path;
  enqueue anything slow for the app to do later.

The Share Extension (§6) and any widgets share the same container, so this is one decision serving
three features.

### 7.9 Language: Siri does not speak Hungarian

This is a hard constraint and it should be stated plainly rather than discovered during
submission. Siri's supported languages do not include Hungarian. No amount of localisation makes
a Hungarian spoken phrase reach the app.

What this means concretely:

- **Localise App Shortcut phrases into English and German only.** Shipping a Hungarian phrase
  list that can never fire is worse than shipping none — it reads as a broken feature.
- **Hungarian users have two working paths.** Either they set Siri to English or German (the
  phrase must then be spoken in that language, which for a short phrase like "add milk to
  Homassy" is a smaller ask than it sounds), or they use in-app dictation.
- **`SFSpeechRecognizer` *does* support `hu-HU`**, including on-device recognition. So the
  existing `VoiceItemDrawer` flow — press a button in the app, speak Hungarian, confirm the
  matched rows — ports fully and stays the Hungarian voice story. This is why §7.6 insists the
  matcher is shared: it is the same matcher on both paths, and the Hungarian suffix stripping is
  only ever exercised by this one.
- **The Shortcuts app still works in Hungarian.** Hungarian users can run every App Shortcut by
  tapping it, put it on the home screen, bind it to the Action Button, and use it in automations
  (§7.10). Only the *spoken trigger* is unavailable.

Say this in the app's own settings screen — one line, e.g. "Siri: English and German. Magyar
hangbevitel: az alkalmazáson belül." — rather than letting a Hungarian user conclude the feature
is broken.

### 7.10 What else the same intents unlock

Once the intents in §7.5 exist, these cost almost nothing more:

- **Spotlight.** `ProductEntity: EntityPropertyQuery` makes individual products findable from the
  home screen search field, which is the nearest thing iOS has to the web app's `CommandPalette`.
- **Shortcuts automations.** "When I arrive at Tesco → read my shopping list" is a user-built
  automation over `ReadShoppingListIntent`. It complements the geofenced notification in §6
  rather than replacing it, and it needs no code.
- **Action Button** (iPhone 15 Pro and later) bound to `AddShoppingItemIntent`.
- **Control Center control** (iOS 18 `ControlWidget`) for "add item" and "start shopping".
- **Widgets and a Live Activity** for shopping mode reuse the same entities and the same store.
- **Apple Watch and HomePod** reach App Intents through Siri, so an EN/DE user gets the list read
  out on a watch with no extra target.

### 7.11 Worth weighing: mirror the list into Reminders

An alternative, or complement, that changes the calculus: keep an EventKit reminder list called
"Homassy — Shopping" in sync with the active shopping list.

- **Gains:** Siri already knows "add milk to my shopping list" with no app name, more naturally,
  and it works from HomePod, Watch and CarPlay. The user's family members who do not have the app
  can still see the list if they share the Reminders list.
- **Costs:** a second source of truth, two-way sync conflicts, and someone editing in Reminders
  what Homassy thinks it owns.
- **Does not fix Hungarian** — Siri has no Hungarian at all, so this buys nothing for HU users.

If it is done, do it as a **one-way mirror** (Homassy → Reminders) plus an inbound sweep that
imports anything added to that list and then clears it. Treat Reminders as an inbox, never as the
store.

### 7.12 Testing notes

- App Shortcuts are indexed at app launch; a phrase change often needs a rebuild and a relaunch
  before Siri picks it up. Budget for the confusion.
- Test `perform()` from the Shortcuts app first — it exercises the same code with none of the
  speech-recognition variance.
- Siri behaviour in the simulator is limited; the voice paths need a device.
- Write the dialog strings with VoiceOver in mind too: they are the same strings.

---

## 8. Local persistence, export and import

### 8.1 Store layout

```
Application Support/
  Homassy.store            SwiftData / Core Data store (+ -wal, -shm)
  Images/
    products/<hash>.jpg    content-addressed, mirrors ProductPictureVersion today
    families/<hash>.jpg
    profiles/<hash>.jpg
```

Content-addressing images by hash is already the web app's scheme, and it makes the export
deduplicating for free.

### 8.2 Export format

Define a document type, e.g. `hu.kellner.homassy.archive`, extension `.homassy`, which is a
zip containing:

```
manifest.json     { schemaVersion, exportedAt, appVersion, counts, locale, timeZone }
data.json         every table, serialised by publicId, with relationships by publicId
images/<hash>.jpg only the images actually referenced
```

Requirements that are easy to get wrong and expensive to fix later:

- **`schemaVersion` from day one.** An export written by v1.0 must still import into v3.0.
  Version the document, not the app.
- Serialise by `publicId` (UUID), never by local integer id.
- Dates in ISO-8601 with an explicit offset, not local wall time — the app already has a
  `UserTimeZone` concept; carry it in the manifest.
- Decimal quantities as strings, not doubles, so 0.1 kg round-trips exactly.
- Export is the user's whole dataset: reachable from Settings → Data, matching the existing
  `profile/data` page.

### 8.3 Import

Three modes, and the choice must be the user's:

1. **Replace** — wipe and restore. The obvious "I got a new phone" path.
2. **Merge** — union by `publicId`; on conflict, keep the newer `updatedAt`.
3. **Import as copy** — new `publicId`s throughout. Useful for "start from a friend's setup".

Show a preview before committing (counts per entity, what will be overwritten), the same
way `InventoryItemsPreviewModal` does today for bulk inventory creation.

### 8.4 Where the file goes

Use SwiftUI `.fileExporter` / `UIDocumentPickerViewController`. That gives the user iCloud
Drive, Files, AirDrop and mail — everything — without the app knowing or caring which. Do
**not** hardcode an iCloud Drive path; it is both fragile and worse UX.

An automatic weekly export into the app's own iCloud Drive container is a cheap add-on if
the app carries the iCloud Documents entitlement anyway.

---

## 9. Supporting the app

**Decided 2026-09-22: no subscription, no paid features.** A "Support Homassy" screen instead,
pointing at an external donation page on a URL of our own. Nothing in the app is ever gated
behind it.

**The defining constraint: these are donations, and nothing is given in return.** Not a feature,
not a badge, not a supporter flag — nothing. That is not modesty, it is the single decision that
keeps both the App Store review (§9.1) and the Hungarian tax treatment (§9.5) simple. The moment
a gift unlocks something, it stops being a gift in both systems at once, and both get harder.

With no entitlement to verify there is **no server anywhere in the product** (§3.6).

### 9.1 External link versus In-App Purchase — where the line actually is

Apple's baseline rule (**Guideline 3.1.1**, "anti-steering") is that an app may not include
buttons, external links or other calls to action pointing at purchase mechanisms other than
In-App Purchase, when what is being bought is digital content or services used in the app. A
plain tip jar that unlocks something is squarely inside that rule and must be IAP.

A pure donation that unlocks nothing sits in a genuinely grey area, and the ground has moved
recently in our favour:

- **United States.** Following the April 2025 injunction in *Epic v. Apple*, apps on the US
  storefront may include external purchase links with no commission and no entitlement.
- **European Union.** The DMA forces Apple to permit link-outs, under its own fee and
  entitlement terms.
- **Elsewhere.** 3.1.1 still applies in its stricter form, and outcomes are reviewer-dependent.

In practice, donation links that give the donor nothing have long been tolerated across
storefronts, because there is no purchase to steer away from. But this is exactly the kind of
rule that gets rewritten between releases.

**What to do about it:**

1. **Verify 3.1.1 and 3.2 against the live guidelines immediately before the first submission**,
   and again before any release that touches the support screen. Do not rely on this document —
   it records the state as understood in September 2026, and this is the paragraph most likely
   to be stale.
2. **Make it unmistakably a donation.** No prices in the app, no tiers, no "unlock", no badge,
   no thank-you perk. The screen says what the app is, that it is free and has no ads, and
   offers a link. Wording matters to a reviewer: "support development", not "buy".
3. **Open it properly** — `SFSafariViewController` or an external Safari open of our own URL,
   never a web view that imitates a checkout.
4. **Have the fallback ready.** If a reviewer rejects the link, the consumable-IAP tip jar in
   §9.3 is a day's work and is guaranteed to pass. Knowing that in advance turns a rejection
   into a one-day delay rather than a redesign.

### 9.2 Which donation platform

We host the link ourselves (e.g. `homassy.hu/support` or a `kellner.dev` path) and redirect from
there, so the destination can change without an app update. Worth doing regardless of which of
these is chosen.

| Platform | Their cut | Payout to Hungary | Fit |
|---|---|---|---|
| **GitHub Sponsors** | 0% (GitHub absorbs it) | Stripe Connect | **Strongest fit.** The repo is already on GitHub and AGPL-licensed; sponsoring an open-source project is a well-understood act. Supports one-off and monthly |
| **Ko-fi** | 0% on donations (optional €6/mo Gold) | PayPal or Stripe, direct | Best pure-donation UX. No account needed to give. Good second link alongside GitHub |
| **Buy Me a Coffee** | 5% | Stripe | The name does the explaining, which is worth something. Higher cut than Ko-fi for the same thing |
| **Liberapay** | 0%, non-profit | Stripe / PayPal | Recurring only. Strong signal in FOSS circles, small audience outside them |
| **Stripe Payment Link** | ~1.5% + fixed, EU cards | Direct | Best margin and full control, but you are the merchant: invoicing and VAT questions land entirely on you (§9.5) |
| **PayPal.me** | PayPal's fees | Direct | Simplest possible. Looks less considered than the others |
| Patreon | 8–12% + processing | Stripe | Built for ongoing creator output. Overkill here |

**Recommendation: GitHub Sponsors as the primary, Ko-fi as the no-account alternative**, both
behind our own `/support` URL. GitHub fits what this project actually is, and Ko-fi catches the
people who will not sign into GitHub to give €3.

Avoid anything that implies a reward tier — Patreon-style benefits would undo §9's defining
constraint.

**The screen's own wording.** This text does double duty: it is what tells a reviewer there is
no purchase here (§9.1), and it is the evidence that supports the gift classification if NAV
ever asks (§9.5). It should say, in plain terms, that the app is free, that nothing is bought,
and that nothing is unlocked. Draft copy for the three locales:

| | |
|---|---|
| **en** | Homassy is free, has no ads and no paid features. If you would like to support its development, you can — it buys nothing and unlocks nothing. Thank you either way. |
| **hu** | A Homassy ingyenes, nincs benne hirdetés és nincs fizetős funkció. Ha szeretnéd támogatni a fejlesztést, megteheted — semmit nem vásárolsz vele és semmit nem old fel. Így is, úgy is köszönöm. |
| **de** | Homassy ist kostenlos, ohne Werbung und ohne kostenpflichtige Funktionen. Wenn du die Entwicklung unterstützen möchtest, kannst du das tun — du kaufst damit nichts und schaltest nichts frei. Danke so oder so. |

Keep a dated copy of this text with the payout records. It costs nothing now and is awkward to
reconstruct later.

### 9.3 The IAP fallback, if the link is refused

Three or four **consumable** products. Consumables, not non-consumables, so someone can give
again; and consumables have no restore-purchases obligation.

```swift
let products = try await Product.products(for: [
    "hu.kellner.homassy.tip.small",
    "hu.kellner.homassy.tip.medium",
    "hu.kellner.homassy.tip.large"
])

let result = try await product.purchase()
if case .success(let verification) = result,
   case .verified(let transaction) = verification {
    await transaction.finish()     // consumable: finish immediately, or it redelivers on every launch
    // …show the thank-you. Store nothing, unlock nothing.
}
```

- Finish the transaction straight away; an unfinished consumable is redelivered on every launch.
- Prices from `Product.displayPrice`, never hardcoded — StoreKit localises currency and tiers.
- Handle `.pending` (Ask to Buy) and `.userCancelled` as ordinary outcomes, not errors.
- Apple's cut is 15% under the Small Business Program.
- Still unlock nothing (§9).

A note in Apple's favour that matters for §9.5: with IAP, **Apple is the merchant of record**. It
collects and remits consumer VAT worldwide and pays a single monthly amount with a statement.
That is materially simpler to account for than a stream of small cross-border donations.

### 9.4 What this costs the rest of the design

- **§3.4 becomes absolute.** No login anywhere in the product, in any form.
- **§3.6 becomes unconditional.** The backend serves nothing; whether the VPS stays on is purely
  a question about the web app (§12.4).
- **The App Store checklist shrinks** (§11): no account deletion, no review account, no Sign in
  with Apple obligation.
- **The zone-owner single point of failure (§3.6) has no paid answer.** There is no server-side
  copy. The mitigations are the local replica each member's device holds, and **export (§8)**,
  which is now the app's only real safety net — hence its move to Phase 3 (§10).

### 9.5 Hungarian tax side — what to take to a könyvelő

**This is not tax advice, and it is the section most likely to be wrong or out of date.** It
exists so the first conversation with a könyvelő (accountant) is efficient rather than
exploratory. Confirm everything below before any money moves; rules here change often and the
right answer depends on the personal circumstances, which this document does not know.

**The question that decides everything: is it an `adomány` (gift) or an `ellenszolgáltatás`
(consideration for something)?**

Because §9 gives the donor nothing at all, the intended classification is a genuine gift. If
anything is given in return — a badge, early access, even a name in a credits list arguably —
it becomes consideration, and then it is revenue for a service, with VAT and invoicing
obligations attached. This is the practical reason the "nothing in return" rule is worth
holding to strictly.

**But "nothing in return" does not by itself make it a gift in NAV's eyes.** This is the single
most important thing in this section and the easiest to get wrong. Calling something
*támogatás* is a label, not a classification. Two facts push in the opposite direction:

- **Repetition and connection to an activity.** A one-off gift from a friend looks like a gift.
  A permanent support button on a published app, collecting small amounts from strangers on an
  ongoing basis, looks to a tax authority like `önálló tevékenységből származó jövedelem` —
  income from an independent activity — regardless of the wording on the screen. If it becomes
  regular and systematic (`üzletszerű`), the question of whether an `egyéni vállalkozó`
  registration is expected also opens up.
- **"Nothing in return" mainly helps with VAT and with Apple.** It keeps the transaction outside
  the scope of `ÁFA` and keeps Guideline 3.1.1 out of the way (§9.1). It does not settle income
  tax.

So the honest expectation is: **assume there is something to do, and get the classification
decided in advance rather than after the first payout.**

**The obligation most likely to be missed: there is no withholding, and advances may be due
quarterly.** GitHub, Ko-fi, Stripe and PayPal are foreign payers. A foreign payer deducts no
Hungarian tax and files nothing on your behalf. If the receipts are treated as income, the
recipient is responsible for the `adóelőleg` themselves — in Hungary that is a **quarterly**
payment, due by the 12th day of the month following the quarter, with the annual reconciliation
in the `SZJA bevallás`. Someone who assumes the annual return is the only touchpoint discovers
otherwise with late-payment interest attached. Confirm whether this applies to your case before
the first payout lands, not at the end of the year.

**Points to raise, roughly in order:**

1. **Which legal form.** Receiving this as a `magánszemély` (private individual) versus as an
   `egyéni vállalkozó` (sole trader) leads to different regimes. Note that **the post-2022 KATA
   is not available here** — it only permits invoicing private individuals, and GitHub, Ko-fi or
   Stripe are companies. So the realistic options are private-individual income, or sole trader
   under `átalányadózás` or `VSZJA`.
2. **If treated as gift income to a private individual**, ask about `ajándékozási illeték` on
   gifts of movable property (`ingó ajándékozás`), including whether a per-donor threshold
   applies, how many small foreign gifts aggregate, and what has to be declared. Do not assume
   small amounts are invisible — platform payouts arrive through a bank and are traceable.
3. **If it is instead treated as income from independent activity** (`önálló tevékenységből
   származó jövedelem`), ask about `SZJA` and `szocho`, what costs may be deducted, and whether
   the Apple developer fee, hosting and hardware qualify.
4. **VAT (`ÁFA`).** A true gift has no counter-performance and is outside VAT scope. Confirm
   that reading. If any consideration exists, cross-border digital services bring `OSS`
   registration into play, which is a materially bigger obligation.
5. **If the IAP route is used instead** (§9.3), the picture changes and is arguably simpler:
   Apple is the merchant of record, handles end-user VAT, and pays out from an EU entity. Ask
   about the `közösségi adószám` (EU VAT number) that intra-EU B2B payouts require, and how the
   monthly Apple statement is booked. Note that Apple requires completed tax and banking details
   in the Paid Apps agreement before any IAP can ship, so this cannot be deferred.
6. **Record-keeping.** Whichever route, keep the platform's payout statements and a record of
   the app's public statement that donations buy nothing. The second one is what supports the
   gift classification if it is ever questioned.
7. **Threshold effects.** Ask at what annual level the answer changes — a few tens of thousands
   of forints a year and a few million are not the same conversation, and it is cheaper to know
   the threshold in advance than to cross it unknowingly.

**Practical steps that are worth doing regardless of the answer:**

- **Have the conversation before the support link goes live**, not after the first payout. An
  hour with a könyvelő decides the classification, the form and the payment rhythm in one go.
- **Keep a separate account or a clear label** for these receipts, so the flow is identifiable
  without reconstructing it from a personal current account later.
- **Keep the payout statements** from whichever platform, plus the dated copy of the support
  screen's wording (§9.2).
- **Note the Apple Developer Program fee** ($99/yr) and any hosting or hardware costs — whether
  they are deductible depends on the form chosen, which is another reason to choose it early.

**One structural observation worth weighing:** the IAP route (§9.3) has a single counterparty,
a single monthly statement and VAT handled upstream. The donation-platform route has a better
margin and a friendlier feel but produces many small cross-border receipts to classify. If the
accountant's answer makes the donation route expensive to administer, that is a legitimate
reason to prefer IAP even though Apple takes 15%.

### 9.6 Options considered and dropped

Kept so they are not re-derived later.

**Apple Pay for donations** — dropped. Apple Pay is for physical goods and services consumed
outside the app, plus donations to *approved nonprofit organisations*. An individual developer
taking thanks for their own app is neither, and Apple treats it as digital content.

**Subscription for off-Apple backup** — dropped with the subscription itself. The pitch was real
(it is the only fix for the zone-owner failure case) but it required an account, a server and an
entitlement system for a product that otherwise needs none of them.

**Moving images to Homassy's own storage** — dropped. The app already compresses hard (product
photos 500 px / 0.5 MB, in practice 30–80 KB; profile and household pictures 800 px / 1 MB), so
a household of 300 products with photos is roughly 20–25 MB of the owner's iCloud. The one
unbounded source was **chat images** at 1600 px / 1 MB each — and chat is cut entirely (§6.0),
which removes the problem at the root rather than paying to store it elsewhere.

**Two sync engines over one store** — never viable, recorded because any future "let's also sync
to our own server" idea walks into it. CloudKit replicating the local store *and* a Homassy
engine replicating the same store means two replication systems with different conflict rules,
delete semantics and clocks fighting over the same rows. If server sync is ever wanted, it must
**replace** CloudKit for that household, not run alongside it.

### 9.7 If a server sync is ever revisited

The existing schema is most of the way there, which is worth knowing before anyone designs one
from scratch:

| Requirement | Already present |
|---|---|
| Stable cross-device identity | `BaseEntity.PublicId` (GUID) |
| Last-writer-wins metadata | `RecordChangeEntity.RecordChange` → `LastModifiedDate`, `LastModifiedBy` |
| Tombstones instead of hard deletes | `SoftDeleteEntity.IsDeleted`, set by `DeleteRecord()` |
| Content-addressed images (free dedupe) | `StoredImageEntity.Version` — a hash already used as the cache key |

Missing: a per-device sync cursor, a server change feed (`GET /changes?since=<cursor>`), a
documented conflict rule (per-row last-writer-wins is plenty for a household), and image
transfer keyed by content hash so an unchanged photo is never re-uploaded.

`LastModifiedBy` is an `int` user id today. With no user table it becomes the CloudKit
`userRecordID`; use that same identifier in the export format (§8.2) so the two are joinable.

---

## 10. Suggested phasing

**Phase 0 — spike (≈1 week).** Three models, two devices, one `CKShare`. Answer three
questions and nothing else, because the persistence layer and the whole family feature hang
off them:

1. Does CloudKit sharing carry this data model acceptably (latency, conflicts, zone size)?
2. **SwiftData or Core Data** — does the current SDK expose `CKShare` from SwiftData, or does
   sharing force `NSPersistentCloudKitContainer` (§5.2)?
3. Does `lastModifiedUserRecordID` give usable per-member attribution in practice (§3.5)?

**Phase 1 — design system.** Palette asset catalog, Public Sans, `Motion`, the sheet and
card primitives, the undo queue, swipe actions. No features. This is the piece that makes
the rest look like Homassy rather than like a generic list app.

**Phase 2 — core loop.** Products, inventory, storage locations, expirations, barcode scanning
against the household's own catalogue (§6.0), local expiry notifications. Put the store in the
**App Group container now** (§7.8) — cheap here, a user-data migration later. This is already a
usable app.

**Phase 3 — export/import** (§8). Moved ahead of shopping and sharing: with the subscription
gone there is no server-side copy anywhere, so this is the *only* thing standing between a user
and total data loss (§3.6). It is also small. Ship it early and prompt it periodically.

**Phase 4 — shopping.** Lists, shopping locations, shopping mode, geofencing, share extension.

**Phase 5 — sharing:** the `CKShare` invite flow, the `Member` record and the one-screen
"what should we call you" onboarding (§3.5), shared lists, member attribution, and
silent-push updates.

**Phase 6 — Siri and App Intents** (§7). Pulled ahead of the other native extras because it is
a stated requirement, not a bonus. The entities and the `ItemMatcher` it needs already exist by
this point, so the cost is mostly the intents themselves.

**Phase 7 — the remaining native-only extras:** widgets, Spotlight, Control Center, a Live
Activity for shopping mode.

**The support screen** (§9) is a day's work with no dependencies — slot it in wherever, as long
as it is before the first public release rather than after.

Calendar, insights, activity feed and automations slot in wherever they fit; none of them are on
the critical path to a shippable v1. Chat, external calendars and Open Food Facts are not on any
path — they are cut (§6.0).

---

## 11. Apple-side checklist

- Apple Developer Program: $99/yr. Required before TestFlight and before any device testing
  beyond a 7-day free-provisioning build.
- Capabilities to enable: iCloud (CloudKit + Documents), Push Notifications (for CloudKit
  silent pushes), Background Modes (remote notifications, background fetch, background
  processing), **App Groups** (required — App Intents, the Share Extension and widgets all
  need the shared store, §7.8), In-App Purchase, Siri.
- Privacy manifest (`PrivacyInfo.xcprivacy`) and App Store privacy labels. Location, camera,
  microphone and photos all need purpose strings in `Info.plist` — and those strings are
  user-visible, so write them properly.
- The app has no login and no account anywhere (§3.4, §9.3), which removes a whole class of
  review friction: no demo account to supply, no account-deletion flow to build, no Sign in
  with Apple obligation, no credential handling to justify in the privacy questionnaire.
- With Open Food Facts cut (§6.0) there are **no third-party network calls at all** — the
  "data collected" and "data linked to you" answers are close to empty, and there is no
  third-party SDK disclosure to make.
- **Re-read Guidelines 3.1.1 and 3.2 immediately before the first submission** (§9.1). The
  support screen is an external donation link, which sits in a grey area that has moved twice
  since 2024 and may move again. Apple Pay is definitively not an option; consumable IAP is the
  fallback if the link is refused, so have the products drafted in App Store Connect even if
  they are never submitted.
- **Nothing may be given in return for a donation** (§9) — no badge, no unlock, no perk. That
  is what keeps it a donation rather than a purchase, in both Apple's eyes and NAV's (§9.5).
- Speech Recognition and Siri usage strings are required once §7 ships.
- Speech Recognition and Siri usage strings are required once §7 ships.
- Licensing: the repository is AGPL-3.0. AGPL and the App Store are a known conflict
  (Apple's terms impose redistribution limits the GPL family forbids; VLC was pulled over
  exactly this). **You are the sole copyright holder (© 2025 Béla Kellner), so you can
  license the iOS build differently** — but that has to be a conscious relicensing decision
  before submission, and pulling in any third-party AGPL/GPL code would break it.
- Age rating and export compliance (standard HTTPS crypto → the usual exemption).

---

## 12. Open decisions

1. ~~**Option A, B or C**~~ — **decided 2026-09-22: Option B**, local-first + CloudKit, with a
   local-only fallback when there is no iCloud account. Consequences: no login in the free app
   (§3.4), identity and attribution come from CloudKit (§3.5), and family is a shared record
   zone rather than a `Family` row.
2. ~~**Does family survive?**~~ — yes, as a `CKShare`. What remains open is the **persistence
   layer** it forces: SwiftData or Core Data (§5.2). Phase 0 answers it.
3. ~~**What does the subscription sell?**~~ — **no subscription** (§9). A voluntary support
   screen linking out to our own donation URL, with nothing given in return. Images stay in
   CloudKit; the dropped server-side options are recorded in §9.6. The product needs no server
   at all.
3b. **Which donation platform** (§9.2)? Recommendation: GitHub Sponsors primary, Ko-fi as the
   no-account alternative, both behind a `/support` URL we control so the destination can
   change without an app update.
3c. **Confirm the Hungarian tax treatment with a könyvelő before any money moves** (§9.5). The
   open question is whether these are handled as gifts to a private individual or as income
   from independent activity, and at what annual level the answer changes.
4. **Does the web app keep living** alongside iOS, or is iOS the successor? This now also
   decides whether the VPS stays switched on at all (§3.6). If both live, the shared-source
   items are worth investing in: the `ProductCategory` generator, the locale files, the
   export schema.
4b. **One household per user, or several?** (§3.7) CloudKit allows several; today's
   `User.FamilyId` allows one. A schema decision, cheap now and expensive later, even if the
   first release only exposes one.
4c. **Rename "family" to "household"** in the user-facing strings? (§3.7) The thing being
   shared no longer implies a family, and the word will be wrong for a good share of users.
5. ~~**Chat:** port to CloudKit, or cut?~~ — **cut** (§6.0), along with external calendar
   subscriptions and the Open Food Facts lookup.
5b. **Does barcode scanning still earn its place** without a product database? (§6.0) The
   assumed design — scan matches the household's own catalogue, first sight of a barcode means
   typing the name once — is coherent and fully offline, but first-time entry is now manual.
   Worth confirming before Phase 2 builds it.
6. **iPad and Mac?** SwiftUI makes both close to free if the layout is size-class-aware from
   the start, and expensive to retrofit later. The existing `lg` sidebar layout is already
   the iPad design.
7. **The FAB** (§4.5): brand continuity or platform idiom.
8. **Relicensing the iOS build** (§11).
