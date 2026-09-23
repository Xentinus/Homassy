# Homassy iOS — task breakdown

Companion to [ios-native-port.md](ios-native-port.md), which holds the reasoning. This file
holds only the work: what to build, in what order, and how to know a piece is finished.

**How to read it.** Tasks are grouped by the phases in §10 of the guide. IDs are stable — refer
to them in commits and issues (`Homassy iOS - product entity + inventory model #P2-04`). Effort
is S (an hour or two), M (half a day to a day), L (several days). `Ref:` points at the section
of the guide that explains why. *Done when* is the acceptance criterion; if it cannot be
demonstrated, the task is not finished.

**There is no login task.** The architecture has no accounts (§3.4). What would have been
sign-up and sign-in is instead P5-03, a single "what should we call you" screen shown once,
after someone accepts a household invitation.

**Phases are ordered by dependency, not by importance.** P0 gates everything; P1 and P2 produce
a usable single-user app; P3 is the safety net and deliberately precedes sharing.

---

## D — Decisions that block tasks

Not work, but nothing downstream is safe until these are settled. Each is also listed in §12 of
the guide.

- [ ] **D-01 — SwiftData or Core Data** (Ref: §5.2) — answered by P0-02. Blocks all of P2.
- [ ] **D-02 — Same repository or a new one** — the iOS app alongside the existing tree, or its
  own repo. A separate repo is cleaner for the licensing question in D-03 and keeps the .NET CI
  untouched; a monorepo keeps the `ProductCategory` generator and locale files in one place.
  Blocks P1-01.
- [ ] **D-03 — Licence for the iOS build** (Ref: §11) — the repo is AGPL-3.0, which conflicts
  with App Store terms. Sole copyright holder, so relicensing is available, but it has to be a
  conscious decision before submission. Blocks release, not development.
- [ ] **D-04 — One household per user, or several** (Ref: §3.7) — a schema decision, cheap now,
  expensive later. Blocks P2-03.
- [ ] **D-05 — Rename "family" to "household" in user-facing strings** (Ref: §3.7). Blocks
  X-01 (the string catalog import) if it is going to happen at all.
- [ ] **D-06 — Does barcode scanning earn its place without a product database** (Ref: §6.0) —
  the assumed design is catalogue-matching with manual first entry. Blocks P2-07.
- [ ] **D-07 — FAB or platform-idiomatic toolbar** (Ref: §4.5). Blocks P1-07.
- [ ] **D-08 — Donation platform** (Ref: §9.2) — GitHub Sponsors + Ko-fi recommended. Blocks
  X-06.
- [ ] **D-09 — Hungarian tax treatment confirmed with a könyvelő** (Ref: §9.5). Blocks the
  support link going live, not its implementation.

---

## P0 — Spike

Throwaway code. The only deliverable is three answers. Do not let it grow into the app.

- [ ] **P0-01 — Two-device CloudKit harness** (M)
  Minimal app, three models with one relationship, CloudKit container, running on two physical
  devices with different Apple IDs. Ref: §3.
  *Done when:* a row created on device A appears on device B.
- [ ] **P0-02 — Does SwiftData expose `CKShare`?** (M)
  The decisive question. Try to share a record zone from SwiftData on the target OS version; if
  it cannot, confirm `NSPersistentCloudKitContainer` can. Ref: §5.2. Answers D-01.
  *Done when:* a written answer plus the smallest code sample that proves it.
- [ ] **P0-03 — Attribution through `lastModifiedUserRecordID`** (S)
  Device B modifies a record created by device A; device A reads back who changed it. Ref: §3.5.
  *Done when:* the second device's identity is readable and stable across app restarts.
- [ ] **P0-04 — Sharing latency and failure modes** (S)
  Measure how long a change takes to appear with `CKDatabaseSubscription` + silent push, and what
  happens offline, on airplane mode, and when both devices edit the same row. Ref: §3, §6.
  *Done when:* rough numbers written down, and the conflict behaviour described.
- [ ] **P0-05 — Write up and decide** (S)
  Update §5.2 and D-01 in the guide with the answer.
  *Done when:* the persistence layer is chosen and recorded.

---

## P1 — Project and design system

No features. This is what makes the result look like Homassy rather than a generic list app.

- [ ] **P1-01 — Xcode project skeleton** (S) — depends on D-02
  App target, bundle identifier (`hu.kellner.homassy`), deployment target, SwiftUI lifecycle,
  `.gitignore`, folder layout.
  *Done when:* it builds and runs on a device.
- [ ] **P1-02 — Mocha palette constants** (S)
  The eleven `--color-mocha-*` values from `main.css` as `Color` constants, plus a `Color(hex:)`
  helper. Ref: §4.1.
  *Done when:* the ramp renders identically to the web app side by side.
- [ ] **P1-03 — Semantic colour asset catalog** (M)
  Colour Sets with Any/Dark appearances for `bg`, `bg-elevated`, `bg-muted`, `border`,
  `border-accented`, `text`, `text-muted`, `text-dimmed`, `primary`, `primary-muted`, `success`,
  `warning`, `error`, `info`. Read the resolved light/dark values out of the running web app
  with devtools; do not eyeball them. Ref: §4.1.
  *Done when:* a sample screen renders correctly in both themes with no hardcoded hex at any
  call site.
- [ ] **P1-04 — Public Sans and type scale** (M)
  Bundle the font, register in `Info.plist`, define text styles that track Dynamic Type. Ref: §4.2.
  *Done when:* every style scales with the system text-size setting up to the largest
  accessibility size without clipping.
- [ ] **P1-05 — `Motion` tokens** (S)
  Port the durations and the two curves as a single enum, and wire
  `@Environment(\.accessibilityReduceMotion)` so motion is removed while end states stay visible.
  Ref: §4.3.
  *Done when:* toggling Reduce Motion removes animation without losing any information.
- [ ] **P1-06 — Sheet and card primitives** (M)
  The drawer-equivalent (`.sheet` + `.presentationDetents` + drag-to-dismiss) and the card shape
  the data screens are built from. Ref: §2.3.
  *Done when:* a throwaway form opens, drags and dismisses like the web drawer.
- [ ] **P1-07 — Navigation shell** (M) — depends on D-07
  `TabView` on phone, `NavigationSplitView` on iPad, with the size-class switch in place from the
  start. Ref: §4.5, §12.6.
  *Done when:* both layouts work and rotating an iPad does not lose navigation state.
- [ ] **P1-08 — Undo queue and toast** (M)
  Port `undoQueue.ts` and `useUndoableAction`: a five-second window, optimistic apply, commit on
  expiry. The toast sits above the tab bar and above any full-screen cover. Ref: §2.3, §4.4.
  *Done when:* an optimistic delete can be undone within the window and commits after it, and
  the toast is reachable from inside a full-screen view.
- [ ] **P1-09 — Swipe actions** (M)
  Swipe-to-delete and swipe-to-edit on list rows, with haptics, committing to the undo queue
  rather than to a confirm dialog. Ref: §2.3.
  *Done when:* the gesture, the haptic and the undo path work together on a device.
- [ ] **P1-10 — Skeletons and empty states** (S)
  The loading placeholders and the empty-state illustration slot. Ref: §2.3.
  *Done when:* a list with no data and a list still loading are both visually resolved.

---

## P2 — Core loop

After this the app is genuinely usable by one person.

- [ ] **P2-01 — App Group container** (S)
  Put the store and the image directory in `group.hu.kellner.homassy` **now**. Cheap here, a
  user-data migration later. Ref: §7.8, §8.1.
  *Done when:* the store opens from the app and from a stub extension target.
- [ ] **P2-02 — Persistence stack** (M) — depends on D-01
  The chosen container, CloudKit private database, and the local-only fallback when
  `accountStatus()` is not `.available`. Ref: §3.3, §3.4.
  *Done when:* the app works signed into iCloud and signed out of it, and says which mode it is
  in.
- [ ] **P2-03 — Core models** (L) — depends on D-04
  `Product`, `InventoryItem`, `ConsumptionLog`, `PurchaseInfo`, `Location`, `Member`. Every
  property optional or defaulted, no `@Attribute(.unique)`, `publicId` carried over. Ref: §5.1,
  §5.2.
  *Done when:* the schema round-trips through CloudKit and `publicId` uniqueness is enforced in
  code.
- [ ] **P2-04 — Enum port** (M)
  `Unit`, `Currency`, `StoreType`, `Language`, `DaysOfWeek`, `ScheduleType`, `ActivityType`,
  `NotificationType`, `BarcodeFormat`, `ImageFormat` as `String`-raw Swift enums. Ref: §5.3.
  *Done when:* every enum used by P2-03 exists and no raw `Int` backing remains.
- [ ] **P2-05 — `ProductCategory` generator** (M)
  Extend `scripts/sync-product-category.mjs` to emit a Swift file and a String Catalog fragment
  from the same C# source. Do not hand-maintain 947 members. Ref: §5.3.
  *Done when:* `npm run sync:product-category` produces the Swift enum and the app compiles
  against it.
- [ ] **P2-06 — Image store** (M)
  Content-addressed files on disk keyed by hash, thumbnails, compression matching the web caps
  (500 px / 0.5 MB for products). Bytes never go in the database. Ref: §5.1, §8.1, §9.6.
  *Done when:* adding the same photo twice stores one file, and a 12 MP camera image lands under
  the cap.
- [ ] **P2-07 — Barcode scanning** (M) — depends on D-06
  `DataScannerViewController`, matching against the household's own catalogue: known barcode
  jumps to the product, unknown opens the product form prefilled. Ref: §6.0.
  *Done when:* both paths work on a device, and camera-permission denial is handled.
- [ ] **P2-08 — Product list and detail** (L)
  The main catalogue screen and the product detail, with search, filters and section index.
  Ref: §2.2, §2.3.
  *Done when:* create, edit, delete and search all work against the store.
- [ ] **P2-09 — Inventory operations** (L)
  Add stock, consume, move between storage locations, mark fully consumed, with the consumption
  log written. Ref: §5.1.
  *Done when:* quantities and logs stay consistent across every operation, including undo.
- [ ] **P2-10 — Storage locations** (M)
  CRUD plus the inventory grouping that hangs off them. Ref: §5.1.
  *Done when:* an item can be filed, found and moved.
- [ ] **P2-11 — Expiration model and status** (M)
  Port `useExpirationStatus` and `ExpirationChip`: the thresholds, colours and ordering. Ref: §2.4.
  *Done when:* the same item shows the same status as the web app for the same dates.
- [ ] **P2-12 — Local expiry notifications** (M)
  `UNCalendarNotificationTrigger` at 07:00 for a 14-day horizon, plus the Monday summary.
  **Schedule a rolling window, not one per item** — iOS caps pending local notifications at 64.
  Ref: §6.1.
  *Done when:* notifications fire on a device with the app closed, and the pending count never
  exceeds the cap with 500 items in stock.
- [ ] **P2-13 — App icon badge** (S)
  Expiring-soon count via `setBadgeCount`, kept fresh on foreground and after each change.
  Ref: §6.1.
  *Done when:* the badge matches the in-app count.

---

## P3 — Export and import

Moved ahead of sharing on purpose: with no server anywhere, this is the only thing between a
user and total data loss (§3.6, §9.4).

- [ ] **P3-01 — Archive format** (M)
  Define `hu.kellner.homassy.archive` / `.homassy`: zip with `manifest.json`, `data.json`,
  `images/`. `schemaVersion` from the first version. Ref: §8.2.
  *Done when:* the format is written down and a fixture file exists.
- [ ] **P3-02 — Export** (M)
  Serialise by `publicId`, ISO-8601 with offsets, decimals as strings, only referenced images.
  Ref: §8.2.
  *Done when:* a full household exports and the archive opens in any zip tool.
- [ ] **P3-03 — Import with three modes** (L)
  Replace, merge (newer `updatedAt` wins), and import-as-copy (fresh `publicId`s). Ref: §8.3.
  *Done when:* each mode behaves as described against an archive with deliberate conflicts.
- [ ] **P3-04 — Import preview** (M)
  Counts per entity and what will be overwritten, shown before anything is committed. Ref: §8.3.
  *Done when:* no import path can destroy data without the preview being seen first.
- [ ] **P3-05 — File picker integration** (S)
  `.fileExporter` / `.fileImporter`. No hardcoded iCloud Drive paths. Ref: §8.4.
  *Done when:* export lands in Files, iCloud Drive and AirDrop, and import accepts from all of
  them.
- [ ] **P3-06 — Backup reminder** (S)
  A periodic, dismissible prompt to export — not buried in settings. Ref: §3.6, §10.
  *Done when:* it appears on a schedule and respects being dismissed.
- [ ] **P3-07 — Round-trip test** (M)
  Export, wipe, import, compare. Automated.
  *Done when:* a full dataset survives the round trip byte-for-byte on the fields that matter.

---

## P4 — Shopping

- [ ] **P4-01 — Shopping list models** (M)
  `ShoppingList`, `ShoppingListItem`, ordering, purchase state. Ref: §5.1.
- [ ] **P4-02 — List screen** (L)
  Cards, quick-purchase on tap, swipe to delete or edit, reordering. Ref: §2.3.
  *Done when:* every interaction the web app has on this screen has an equivalent.
- [ ] **P4-03 — Shopping locations** (M)
  CRUD plus MapKit for the location picker and display, replacing MapLibre. Ref: §2.3, §6.1.
- [ ] **P4-04 — Shopping mode** (M)
  `.fullScreenCover`, wake lock via `isIdleTimerDisabled`, large touch targets. Ref: §2.3.
  *Done when:* the screen stays awake and the undo toast is still reachable inside it.
- [ ] **P4-05 — Geofencing** (L)
  `CLMonitor` region monitoring around the nearest N shopping locations — **20-region system
  limit**, so monitor the nearest, not all. Background notification when entering a store with
  pending items. Ref: §6.1.
  *Done when:* the notification fires with the app closed, and permission downgrades are handled.
- [ ] **P4-06 — Share extension** (M)
  Text or image shared from another app becomes a list item or a product, via the App Group
  store. Ref: §6.1, §7.8.
  *Done when:* sharing from Safari and from Photos both work with the app not running.
- [ ] **P4-07 — Purchase info and price history** (M)
  `PurchaseInfo` capture, best-price badge, price history card. Ref: §5.1.

---

## P5 — Household sharing

- [ ] **P5-01 — Shared record zone** (L) — depends on P0-02
  Move household data into a custom shared zone; keep personal data in the private default zone.
  Ref: §3.4.
  *Done when:* the split is enforced and a zone can be created, populated and read back.
- [ ] **P5-02 — Invite and accept flow** (L)
  `UICloudSharingController` / `ShareLink` to produce the link; `CKAcceptSharesOperation` in the
  scene delegate to accept. `publicPermission = .none`. Ref: §3.4.
  *Done when:* two Apple IDs end up in one household from a link sent over Messages.
- [ ] **P5-03 — First-run member setup** (M)
  The one screen that replaces registration: display name, optional photo, writes the `Member`
  record. Ref: §3.5.
  *Done when:* a newly joined participant appears by name to the other members.
- [ ] **P5-04 — Member list and permissions** (M)
  Participant list joined to `Member` records, role and acceptance status, read-only versus
  read-write, removal. Ref: §3.5, §3.7.
  *Done when:* removing a participant revokes access and their past attributions still render a
  name.
- [ ] **P5-05 — Attribution** (M)
  `lastModifiedUserRecordID` → member colour and the "changed by" flash. Member colour is only
  ever an accent, never a fill or text colour. Ref: §3.5, §4.1.
  *Done when:* a change made on another device is attributed correctly and the flash respects
  Reduce Motion.
- [ ] **P5-06 — Change subscriptions** (M)
  `CKDatabaseSubscription` plus silent push, and the reconnect diff that flashes rows which
  changed while away. Ref: §2.3, §6.1.
  *Done when:* a change on device B appears on device A without a manual refresh.
- [ ] **P5-07 — Owner-loss guidance** (S)
  Explain the single point of failure once, at household creation, and prompt participants to
  export. Ref: §3.6.
  *Done when:* the message is shown once and recorded as seen.

---

## P6 — Siri and App Intents

- [ ] **P6-01 — `ItemMatcher`** (M)
  Port `useVoiceItemMatching`: existing product → `ProductCategory` vocabulary → free text, with
  the Hungarian suffix stripping. Shared by the intents and in-app dictation. Ref: §7.6.
  *Done when:* "két liter tejet" resolves to the existing `tej` product, and the same matcher is
  called from both paths.
- [ ] **P6-02 — App entities** (M)
  `ShoppingListEntity`, `ShoppingItemEntity`, `ProductEntity`, `StorageLocationEntity`,
  `ShoppingLocationEntity`, with `EntityStringQuery` backed by P6-01. Ref: §7.4.
- [ ] **P6-03 — Write intents** (L)
  `AddShoppingItemIntent`, `CompleteShoppingItemIntent`, `ConsumeInventoryIntent`. No app
  foreground, works offline. Disambiguate rather than guess. Ref: §7.5, §7.6.
  *Done when:* each runs from the Shortcuts app with the main app never opened.
- [ ] **P6-04 — Read intents** (M)
  `ReadShoppingListIntent`, `ReadExpiringIntent`, `CheckStockIntent`, with dialog written for the
  ear — three names and a count, not twenty-two names. Ref: §7.7.
- [ ] **P6-05 — Interactive snippet** (M)
  The snippet view under the spoken answer carries a tick button per row wired to
  `CompleteShoppingItemIntent`. Ref: §7.7.
  *Done when:* items can be ticked off from the Siri result without opening the app.
- [ ] **P6-06 — App shortcuts and phrases** (M)
  `AppShortcutsProvider` with `\(.applicationName)` in every phrase, localised **English and
  German only**. Ref: §7.3, §7.9.
  *Done when:* the phrases trigger on a device, and no Hungarian phrase list ships.
- [ ] **P6-07 — Hungarian voice path** (M)
  `SFSpeechRecognizer` with `hu-HU`, on-device, feeding the same `ItemMatcher` into a review
  sheet. Ref: §7.9.
  *Done when:* Hungarian dictation adds items correctly, and the settings screen explains that
  Siri itself has no Hungarian.
- [ ] **P6-08 — Open-app intents** (S)
  `StartShoppingModeIntent`, `ScanBarcodeIntent` with `openAppWhenRun = true`. Ref: §7.5.

---

## P7 — Native extras

- [ ] **P7-01 — Expiring-soon widget** (M) — Ref: §6.1
- [ ] **P7-02 — Shopping-list widget** (M) — Ref: §6.1
- [ ] **P7-03 — Spotlight indexing** (M)
  `ProductEntity: EntityPropertyQuery`, the nearest thing to the web `CommandPalette`. Ref: §7.10.
- [ ] **P7-04 — Control Center control** (S) — Ref: §7.10
- [ ] **P7-05 — Live Activity for shopping mode** (L) — Ref: §7.10
- [ ] **P7-06 — Calendar screen** (M)
  Expirations, shopping deadlines and `CalendarNote`. External ICS subscriptions are cut.
  Ref: §6.0, §6.1.
- [ ] **P7-07 — Automations** (L)
  Auto-consume, usage reminders, list-add, low-stock thresholds, on `BGAppRefreshTask`. Design
  for "runs late" and recompute on foreground. Ref: §6.1.
- [ ] **P7-08 — Activity feed** (M) — Ref: §6.1
- [ ] **P7-09 — Statistics and insights** (L)
  Swift Charts, badges, streaks. Guard the badge burst against re-firing — `justUnlocked` was a
  server-once guarantee and becomes a local flag. Ref: §2.3, §5.1.
- [ ] **P7-10 — Onboarding tour** (M)
  TipKit in place of `OnboardingSpotlight`. Ref: §2.3.

---

## X — Cross-cutting

Not phase-bound. X-01 and X-02 should start early because they touch everything.

- [ ] **X-01 — String Catalog import** (L) — depends on D-05
  Script the flattening of 3×3270 lines of nested JSON into `.xcstrings`, keeping key shapes
  identical so the two apps stay diffable. Plural rules need manual review — Hungarian has one
  form, English and German two. Ref: §5.4.
  *Done when:* the script runs repeatably and the app is fully localised in en/hu/de.
- [ ] **X-02 — Accessibility pass** (M, recurring)
  Dynamic Type to the largest sizes, VoiceOver labels, Reduce Motion, contrast — including the
  rule that member colours never become text or fill. Ref: §4.1, §4.3.
  *Done when:* every shipped screen is navigable with VoiceOver at the largest text size.
- [ ] **X-03 — App icon and launch** (S)
  Icon set, and the logo trace done in the first SwiftUI view since a launch storyboard cannot
  animate. Ref: §2.3.
- [ ] **X-04 — Capabilities and entitlements** (S)
  iCloud (CloudKit + Documents), Push Notifications, Background Modes, App Groups, Siri. Ref: §11.
- [ ] **X-05 — Privacy manifest and usage strings** (M)
  `PrivacyInfo.xcprivacy`, purpose strings for location, camera, microphone, photos and speech.
  They are user-visible, so write them properly. With no third-party calls the privacy labels are
  close to empty. Ref: §2.5, §11.
- [ ] **X-06 — Support screen** (S) — depends on D-08
  Static screen, the en/hu/de copy from §9.2, opening our own `/support` URL in
  `SFSafariViewController`. **Unlocks nothing, no badge, no tiers.** Ref: §9.
  *Done when:* the screen matches the approved copy and grants the user nothing.
- [ ] **X-07 — `/support` redirect page** (S)
  A page on our own domain that redirects to whichever platform D-08 picks, so the destination
  can change without an app update. Ref: §9.2.
- [ ] **X-08 — Guideline re-check before submission** (S)
  Re-read Guidelines 3.1.1 and 3.2 against the live text; confirm the external donation link is
  still acceptable, and have the consumable-IAP fallback (§9.3) drafted in App Store Connect
  either way. Ref: §9.1, §11.
- [ ] **X-09 — Record-keeping setup** (S) — depends on D-09
  Separate account or label for donation receipts, dated copy of the support-screen wording,
  a place for platform payout statements. Ref: §9.5.
- [ ] **X-10 — TestFlight and release prep** (M)
  Developer Program enrolment, App Store Connect record, screenshots, age rating, export
  compliance, licence decision from D-03. Ref: §11.

---

## Not building

Listed so nobody re-adds them by accident. Ref: §6.0.

- Family chat, and everything attached to it (5 entities, 7 components, 3 composables, the
  typing indicator, the chat z-index tier).
- External calendar (ICS) subscriptions. The calendar *screen* stays — P7-06.
- Open Food Facts lookup. Barcode scanning stays — P2-07.
- Live presence. Reduced to "last active" at most.
- Any login, registration, password, passkey or email-OTP flow. Ref: §3.4.
- Push-notification subscriptions to our own server, and the server itself. Ref: §3.6.
- Any subscription, paid feature or entitlement check. Ref: §9.
