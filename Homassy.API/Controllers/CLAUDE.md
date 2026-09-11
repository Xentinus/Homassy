# Controllers — Homassy.API

> Endpoint reference split out of [../CLAUDE.md](../CLAUDE.md). Read this when adding or changing a controller or endpoint.

## Controllers Reference

### AuthController

Provides user session management for Kratos authentication (no `[Authorize]` at class level).

**Endpoints:**

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/me` | Yes | Get current user info (syncs local user) |
| GET | `/session` | Yes | Get Kratos session information |
| POST | `/sync` | Yes | Force sync local user with Kratos |
| GET | `/config` | No | Get Kratos configuration URLs for frontend |

**Key Patterns:**
- Kratos session validation via middleware
- Automatic local user synchronization
- Kratos configuration endpoint for frontend integration

### UserController

Manages user profile, settings, activity, and push notifications (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/profile` | Get user profile |
| PUT | `/settings` | Update user settings and preferences |
| GET | `/{publicId}/profile-picture` | Serve a user's avatar as image **bytes** (`?size=thumb\|full`, `?v=` version) |
| POST | `/profile-picture` | Upload profile picture synchronously (legacy) |
| POST | `/profile-picture/upload-async` | Upload profile picture asynchronously (returns job ID) |
| DELETE | `/profile-picture` | Delete profile picture |
| GET | `/notification` | Get notification preferences |
| PUT | `/notification` | Update notification preferences |
| PUT | `/onboarding` | Set or clear the first-run tour flag (`{ completed }`) |
| GET | `/bulk` | Get multiple users by comma-separated public IDs (`?publicIds=...`) |
| GET | `/activities` | Paginated activity history with optional filters |
| GET | `/push/vapid-key` | Get VAPID public key for push subscription |
| POST | `/push/subscribe` | Subscribe device for push notifications |
| POST | `/push/unsubscribe` | Unsubscribe device from push notifications |
| POST | `/push/test` | Send a test push notification to current device |

**Key Patterns:**
- All endpoints require authentication
- Base64 image upload for profile pictures
- **Avatars are served, not embedded.** Every DTO that mentions a user (`UserInfo`, `UserProfileResponse`, `FamilyMemberResponse`, `FamilyJoinRequestResponse`) carries `ProfilePictureUrl` — a server-relative path built by `Constants/MediaUrls` — instead of the image. The bytes come from `GET /{publicId}/profile-picture`, which answers raw image data (no `ApiResponse` envelope) with an `ETag`, `Cache-Control: private, max-age=1y, immutable` and `304` handling, via `ControllerBase.CacheableImage()` in `Extensions/ImageResponseExtensions`
- The `?v=` in the URL is the stored image's content hash and is **not read** by the endpoint. It exists so a changed picture is a changed URL — which is what makes the year-long cache lifetime correct and means there is no cache to invalidate on upload
- `?size=thumb` (the default, and what every list uses) serves the 128px square WebP thumbnail generated on upload; `?size=full` serves the uploaded image. A row with no thumbnail (uploaded before thumbnails existed) gets one generated and stored on the first `thumb` request, and falls back to the full image if that fails — the ETag says which was served, so a cached fallback is not kept after a successful backfill
- Thumbnails are WebP; a client whose `Accept` header rules that out gets a JPEG transcode, hence the `Vary: Accept`
- Bytes live in `UserProfilePictures`, not on `UserProfiles` — see [../Entities/CLAUDE.md](../Entities/CLAUDE.md)
- Async image upload returns a `jobId`; track progress via `GET /progress/{jobId}`
- Push notifications use Web Push API (VAPID)
- Activity feed supports pagination (`pageNumber`, `pageSize`, `returnAll`) and filtering by type/date/user
- `PUT /onboarding` writes `UserProfile.OnboardingCompletedAt` (#98) and has no `GET` counterpart on purpose: the flag rides back on `GET /auth/me` (`UserInfo.OnboardingCompletedAt`), a payload the client already fetches at boot, so knowing whether to start the tour costs no extra request. One endpoint with a `completed` flag rather than a "complete" and a "reset" verb - finishing the tour and replaying it are the same field written two ways
- The notification *preferences* here are settings; the notification *content* lives on its own controller (see `NotificationController` below). Conflating the two under one prefix is how the web client's `NotificationsDrawer` came to mean "the preferences panel"

### FamilyController

Manages family operations (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Get family details (including members) |
| GET | `/members` | Get all members of the current user's family |
| PUT | `/` | Update family |
| POST | `/create` | Create new family |
| POST | `/join-requests` | Request to join a family by share code (requires approval) |
| GET | `/join-requests/mine` | Get the current user's pending join request, if any |
| DELETE | `/join-requests/mine` | Withdraw the current user's pending join request |
| GET | `/join-requests` | List pending join requests for the current user's family |
| POST | `/join-requests/{publicId}/approve` | Approve a pending join request (adds requester to family) |
| POST | `/join-requests/{publicId}/reject` | Decline a pending join request |
| POST | `/leave` | Leave family |
| POST | `/picture` | Upload family picture (Base64) |
| DELETE | `/picture` | Delete family picture |

**Key Patterns:**
- Family context from `SessionInfo.GetFamilyId()`
- Validation that user belongs to a family
- Family share-code system for joining
- **Approval-gated join requests**: joining is not immediate — a request stays `Pending` until an existing member approves or rejects it (a user may hold only one pending request at a time). Backed by `FamilyJoinRequestFunctions` and the `FamilyJoinRequest` entity.
- Base64 image upload for family pictures

### FamilyChatController

The family conversation (#144) — history and the two writes that change it (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Query Params | Description |
|--------|----------|--------------|-------------|
| GET | `/messages` | `before`, `limit` | One page of the caller's family conversation, newest first |
| POST | `/messages` | - | Send a text message |
| POST | `/messages/image` | - | Send a picture, with an optional caption (#147) |
| GET | `/messages/{publicId}/image` | `size`, `v` | Serve an image message's picture as **bytes** (#147) |
| DELETE | `/messages/{publicId}` | - | Delete one of your own messages (soft delete) |

**Key Patterns:**
- **No endpoint takes a family id.** `FamilyChatFunctions` resolves the caller's own family from `SessionInfo`, so the only conversation a caller can address is theirs — "a member of another family cannot read or post" is true by construction, not by a check somebody has to remember. A caller with no family gets **403** `FAMILYCHAT-0001`, the same answer as a family that is not theirs
- **Cursor-paged on `(SentAt, PublicId)`** via the existing `Models/Activity/ActivityCursor`, reused rather than reimplemented. Messages arrive at the top *while the reader is scrolling back*, so a numeric offset would re-show or skip rows; and a burst of messages routinely shares a tick, which is why the id is half the cursor. An undecodable cursor is a **400**, never a 500
- **Deleting is own-messages-only, and a soft delete.** Somebody else's message answers **404**, indistinguishably from one that does not exist — distinguishing them would let a caller probe which ids exist. The row stays so clients that already rendered it can be told it is gone, and so #149's read markers keep pointing at something
- **The body deliberately skips `[SanitizedString]`.** That attribute rejects any value containing `<` or `>`; "5 < 10" and a pasted line of code are ordinary things to send your family. The safety it buys elsewhere is bought here by rendering text nodes, never `v-html` (#147)
- **Rate limited per user**, not per IP, inside the Functions layer (`family-chat:send:{userId}`, 40/min). A family behind one NAT shares an IP bucket, so the middleware's route-template limit can only throttle the household. Answers **429** `FAMILYCHAT-0004`. Inherits the process-local scope of `RateLimitService` — see #82
- Messages are **not** wired into the trigger-based cache: that cache is for slow-changing master data, and `DatabaseTriggerInitializer` skips `FamilyChatMessages` by name for the same reason it skips `UserNotifications`
- Sender payloads carry the public id, display name, avatar **URL** and identity colour — never the avatar bytes (a page is hundreds of rows) and never the internal user id
- **Pictures are served, not embedded** (#147). Bytes live in `FamilyChatImages`, a `StoredImageEntity` table keyed to the message; the message payload carries `imageUrl` / `imageFullUrl` plus the stored dimensions (so the client can reserve the box before the bytes arrive). The upload is base64 in, like every other upload here; what is deliberately *not* base64 is the answer
- The picture endpoint is addressed by the **message's** public id: whether you may see the picture is the same question as whether you may see the message, so one id answers both. It behaves exactly like the avatar and product-image endpoints (ETag, `private, max-age=1y, immutable`, `?size=thumb|full`, `Vary: Accept`). Another family's message answers **404**
- The message row and its bytes **commit in one transaction, and the broadcast follows the commit** — otherwise a `MessageCreated` can reach clients whose image request would 404
- Size and mime are validated *before* the bytes reach the decoder, and the image path has its own per-user rate limit (`family-chat:image:{userId}`, 8/min) — an image costs a decode and a resize, so it must not share the text allowance
- **No server-side link unfurling**, now or by accident later: fetching a user-supplied URL from the API is the SSRF class #78 closed. Rich previews would need their own issue, with an allowlist or an egress proxy

**Realtime (SignalR):**
- Hub at `/hubs/family-chat` (`FamilyChatHub`, `[Authorize]`) — same Kratos-cookie-on-handshake auth as the other three hubs
- `JoinChat()` takes **no argument**: the group is derived from the session (`family-chat:{familyPublicId}`), so a client cannot ask for somebody else's group. It answers with the newest page, so opening the panel is one round trip. `LeaveChat()` removes the connection
- After a successful commit `FamilyChatFunctions` broadcasts through the injected `FamilyChatRealtime`: `MessageCreated` (carries the message **and the sender's own correlation id**, so an optimistically appended message is reconciled rather than rendered twice), `MessageDeleted`, `TypingChanged`
- `SetTyping(bool)` (#148) sets a per-connection flag with a ~5s TTL in `FamilyChatConnectionState`, the single bag holding every per-connection flag the chat has. **Every flag expires on its own**, because a closed lid or a dropped socket never sends the "stopped" call; `FamilyChatTypingSweepService` retires expired ones and broadcasts the change, which is what makes the indicator self-healing. The set is collapsed per user (two devices is one typist) and a typist is never echoed their own state (`GroupExcept`). Sending a message clears the sender's flags on the write path, not only from the client
- Broadcast failures are logged but never break the write

### ProductController

Manages product catalog and inventory (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Get all products |
| GET | `/{publicId}/image` | Serve a product's picture as image **bytes** (`?size=thumb\|full`, `?v=` version) |
| POST | `/` | Create new product |
| PUT | `/{productPublicId}` | Update product |
| DELETE | `/{productPublicId}` | Always rejected with **403** `PRODUCT-0004` — products are global |
| POST | `/{productPublicId}/favorite` | Toggle favorite status |
| GET | `/{productPublicId}/detailed` | Get detailed product info with inventory |
| GET | `/inventory/expiration-count` | Count of expiring/expired items, split by already-expired |
| GET | `/detailed` | Get all detailed products for user |

**Key Patterns:**
- **Pictures are served, not embedded.** `ProductInfo` carries `ProductImageUrl` (the list thumbnail) and `ProductImageFullUrl` (the detail/lightbox rendition) instead of the image. Both come from `GET /{publicId}/image`, which behaves exactly like the avatar endpoint on `UserController` — read its notes for the ETag, caching, `?v=` and `Accept` rules. The full URL is carried in list payloads too, because the lightbox opens from a card in a list
- Product thumbnails are **bounded (256px), not square-cropped** like avatars: a card renders its image `object-contain`, so a centre crop would clip a tall bottle or a wide label
- `GET /inventory/expiration-count` returns `TotalCount` **and** `ExpiredCount`. The split is what lets the client colour its nav badge from the same expiration ramp its cards use — a single total could only ever be one colour, and it was always the alarming one
- `InventoryGridProductInfo` carries `Category`, so the grid can group its cards by category (the client maps it onto a presentation-only `ProductCategoryGroup`; the API neither knows nor stores the group)
- `InventoryGridItemInfo` carries `OriginalQuantity` (from the item's purchase info) so a grid card can draw a stock ring. It is the only piece of purchase detail in that otherwise deliberately light payload, and `ProductFunctions.GridItem` — the instance wrapper around `BuildGridItem` — is what fills it from the cache on every projection this layer emits, including the realtime broadcasts
- Bytes live in `ProductImages`, not on `Products` — the strongest case for the split, since these are the largest images the app stores, a list shows many, and the whole `Product` row sits in a process-wide cache
- Product customization per user (favorites, notes)
- Inventory tracking with purchase info and consumption logs
- Family-shared products support
- **Products are never deletable through the API**: a `Product` row is global (only `ProductCustomization` is family-scoped), so deleting one would remove it — and its inventory items — for every family. `DELETE /{productPublicId}` rejects with 403 `PRODUCT-0004`; `ProductFunctions.DeleteProductAsync` remains for maintenance use only. Users drop a product from their own view by deleting their inventory items or their customization.

**Realtime (SignalR):**
- Hub at `/hubs/inventory` (`InventoryHub`, `[Authorize]`) — same Kratos-cookie-on-handshake auth as the shopping-list hub
- Groups are identity-derived (not per-resource): on connect each connection joins `inventory:user:{userId}` and, if the user has a family, `inventory:family:{familyId}` — matching the grid's visibility filter (`item.UserId == me || item.FamilyId == myFamily`). `JoinInventory()` returns the light `List<InventoryGridProductInfo>` snapshot (only the fields the grid cards render)
- After a successful write, `ProductFunctions` (and the in-process automation consume path in `AutomationFunctions`) broadcasts via the injected `InventoryRealtime` helper: `InventoryUpserted` (create/quick-add/update/partial-consume/split/move — carries a light product + item), `InventoryDeleted` (delete / consume-to-zero), `ProductUpdated` (catalog fields), `ProductFavoriteChanged` (per-user, user group only), `ProductDeleted`. Family-shared items route to the family group, personal items to the user group
- Out-of-process mutations (the `Homassy.Notifications` automation worker) relay through `POST /api/v1/internal/inventory/broadcast` (see InternalController), since they can't reach the hub in-process
- Broadcast failures are logged but never break the write

### LocationController

Manages shopping and storage locations (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/shopping` | Get all shopping locations |
| POST | `/shopping` | Create shopping location |
| PUT | `/shopping/{publicId}` | Update shopping location |
| DELETE | `/shopping/{publicId}` | Delete shopping location |
| POST | `/shopping/reorder` | Set the manual order of the caller's shopping locations |
| GET | `/storage` | Get all storage locations |
| POST | `/storage` | Create storage location |
| PUT | `/storage/{publicId}` | Update storage location |
| DELETE | `/storage/{publicId}` | Delete storage location |
| POST | `/storage/reorder` | Set the manual order of the caller's storage locations |

**Key Patterns:**
- Two location types: Shopping (stores) and Storage (home locations)
- Color coding support for UI
- Family sharing via `IsSharedWithFamily` flag
- Ownership validation for modifications
- Shopping locations carry optional `Latitude`/`Longitude` (nullable `double`) — geocoded on the client at save time and sent in `ShoppingLocationRequest`; on update they are treated as a pair. Powers the frontend shopping-list proximity ("you are here") feature. No server-side geocoding.
- Both kinds carry a `SortOrder` (see the manual-ordering note under ShoppingListController). Lists are returned ordered by it with the name as the tie-break, so a set of locations nobody has dragged is still alphabetical
- Shopping locations also carry `StoreTypes` — a set of `StoreType` enum values stored as a PostgreSQL `integer[]` (Npgsql maps `List<StoreType>` → `integer[]`; no converter). A location can belong to several (e.g. OBI = `HardwareStore` + `GardenCenter`). On update a non-null (possibly empty) `StoreTypes` list replaces the set; `null` means "no change". Localized client-side only (`enums.storeType.*`); powers the shopping-list "similar store here" highlight.

### ShoppingListController

Manages shopping lists and items (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Query Params | Description |
|--------|----------|--------------|-------------|
| GET | `/` | - | Get all shopping lists |
| GET | `/{publicId}` | `showPurchased` | Get detailed shopping list |
| POST | `/` | - | Create shopping list |
| PUT | `/{publicId}` | - | Update shopping list |
| DELETE | `/{publicId}` | - | Delete shopping list |
| POST | `/item` | - | Create shopping list item |
| PUT | `/item/{publicId}` | - | Update shopping list item |
| DELETE | `/item/{publicId}` | - | Delete shopping list item |
| POST | `/item/reorder` | - | Set the manual (aisle) order of a list's items |

**Query Parameters:**
- `showPurchased` (bool, default: false) - Include purchased items older than 1 day

**Key Patterns:**
- Hierarchical structure: Lists contain Items
- Items can reference Products or use custom names
- Purchased items auto-hidden after 1 day (configurable via `showPurchased`)
- Family sharing support
- Shopping location assignment per item — `PUT /item/{publicId}` can reassign it (`ShoppingLocationPublicId`) or clear it (`ClearShoppingLocation: true`, needed because a null id means "no change")
- **Manual (aisle) ordering.** Items carry a `SortOrder` and `POST /item/reorder` takes the ordered ids and writes them in one transaction. The positions are sparse gapped integers, not indices: `Functions/SparseOrdering` keeps the longest already-increasing run and rewrites only the rest, so a single drag is one row update and the whole list is renumbered only when a gap is exhausted. The response (and the broadcast) carries **only the rows that moved**. A subset of the list is accepted and ordered relative to itself; ids from another list are rejected rather than skipped, as are duplicates. Every pre-existing row is 0, so a list nobody has dragged is unaffected. The same scheme and the same helper back the location and automation reorder endpoints

**Realtime (SignalR):**
- Hub at `/hubs/shopping-list` (`ShoppingListHub`, `[Authorize]`) — the Kratos session cookie rides the WebSocket handshake, so the existing auth pipeline works unchanged
- `JoinList(publicId, showPurchased)` joins the list's group (`shopping-list:{publicId}`) and returns the current `DetailedShoppingListInfo` snapshot via the same access-checked path as the REST endpoint; `LeaveList(publicId)` leaves the group
- After a successful REST write, `ShoppingListFunctions` broadcasts through the injected `ShoppingListRealtime` helper: `ItemUpserted` (create/update/purchase/restore, hydrated item), `ItemDeleted`, `ItemsReordered` (only the items whose position changed, as `{ publicId, sortOrder }`), `ListUpdated`, `ListDeleted`
- Broadcast failures are logged but never break the HTTP write that triggered them

### HealthController

Provides health check endpoints for monitoring and orchestration (all endpoints have no authentication requirement).

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Comprehensive health check with all dependencies |
| GET | `/health/ready` | Readiness probe (database only) |
| GET | `/health/live` | Liveness probe (always returns 200) |

**Response Format:**
```json
{
  "Status": "Healthy",
  "Duration": "45ms",
  "Dependencies": {
    "npgsql": {
      "Status": "Healthy",
      "Duration": "12ms",
      "Description": null
    },
    "openfoodfacts": {
      "Status": "Healthy",
      "Duration": "150ms"
    }
  }
}
```

**Status Codes:**
- 200 OK - All checks healthy
- 503 Service Unavailable - One or more checks degraded/unhealthy

**Key Patterns:**
- Kubernetes-compatible probes (ready/live)
- Tagged health checks for selective monitoring
- Dependency health with timing information
- `/health` - Full comprehensive check
- `/health/ready` - Only checks tagged with "ready" (database)
- `/health/live` - Lightweight check (no external dependencies)

### VersionController

Returns application version information (no authentication required).

**Endpoints:**

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/version` | No | Get application version info |

**Response:**
```json
{
  "Success": true,
  "Data": {
    "Version": "25.1214.2132-prod",
    "ShortVersion": "25.1214",
    "BuildType": "prod",
    "BuildDate": "2025-12-14T21:32:00"
  }
}
```

**Key Patterns:**
- Date-based versioning `YY.MMDD.HHmm` (defined in `Directory.Build.props`); `BuildType` is `prod` (Release) or `dev` (other configs)
- Build date is reconstructed from the version string
- Public endpoint (no auth required)
- Useful for deployment tracking

### OpenFoodFactsController

Provides barcode lookup integration with Open Food Facts database (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/{barcode}` | Look up product by barcode |

**Response Includes:**
- Product name, brand, categories
- Nutrition information (energy, proteins, carbs, fats, fiber, salt, sugars)
- Nutrition grades (Nutriscore, Ecoscore, NOVA group)
- Allergens and ingredients
- Product image (Base64 encoded)

**Error Responses:**
- 404 Not Found - Product not found in Open Food Facts database
- 400 Bad Request - Invalid barcode format

**Key Patterns:**
- External API integration with graceful error handling
- Automatic image downloading and Base64 encoding
- Rich nutrition data for product enrichment
- Timeout handling for external service calls

### SelectValueController

Provides dropdown/select list values for UI components (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/{type}` | Get select values for specified type |

**Type Parameter Values:**
- `ShoppingLocation` - User's shopping locations
- `StorageLocation` - User's storage locations
- `Product` - User's products
- `ProductInventoryItem` - User's inventory items
- `ShoppingList` - User's shopping lists

**Response Format:**
```json
{
  "Success": true,
  "Data": [
    {
      "PublicId": "123e4567-e89b-12d3-a456-426614174000",
      "Text": "Aldi - Main Street"
    },
    {
      "PublicId": "223e4567-e89b-12d3-a456-426614174001",
      "Text": "Walmart - Downtown"
    }
  ]
}
```

**Key Patterns:**
- Simplified data structure for dropdowns (PublicId + Text)
- Respects family sharing (includes family-shared entities)
- Alphabetically ordered for better UX
- User/family context from SessionInfo

### ErrorCodesController

Exposes all typed error codes as a public reference (no authentication required).

**Endpoints:**

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/` | No | Get all error codes with descriptions, grouped by category |
| GET | `/{group}` | No | Get error codes for a specific group prefix (e.g., `AUTH`, `USER`, `PRODUCT`, `VALIDATION`) |

**Key Patterns:**
- Public endpoint – no authentication required
- Error codes are `ErrorCode` enum values serialized as strings grouped by prefix
- Useful for frontend i18n and debugging

### ProgressController

Tracks progress of long-running background jobs (e.g., async image uploads). All endpoints require `[Authorize]`.

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/{jobId}` | Get progress status of a job (percentage, stage, status, errorMessage) |
| DELETE | `/{jobId}` | Cancel a running job |

**Response Shape:**
```json
{
  "Success": true,
  "Data": {
    "jobId": "3fa85f64-...",
    "percentage": 45,
    "stage": "processing",
    "status": "running",
    "errorMessage": null
  }
}
```

**Key Patterns:**
- Jobs created by async endpoints (e.g., `POST /user/profile-picture/upload-async`)
- Polling-based progress tracking via `IProgressTrackerService`
- Returns 404 if job ID unknown
- DELETE cancels the job via `CancellationTokenSource`

### AutomationController

Manages item-automation rules — scheduled or threshold-driven actions on inventory items, products, and shopping lists (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Query Params | Description |
|--------|----------|--------------|-------------|
| GET | `/` | - | Get all automation rules for the current user and family |
| GET | `/{publicId}` | - | Get a single automation rule |
| POST | `/` | - | Create an automation rule |
| PUT | `/{publicId}` | - | Update an automation rule (partial) |
| DELETE | `/{publicId}` | - | Delete an automation rule (soft delete) |
| POST | `/reorder` | - | Set the manual order of the caller's automation rules |
| POST | `/{publicId}/execute` | - | Manually execute the rule (auto-consume or confirm a notify-only rule) |
| GET | `/{publicId}/history` | `skip`, `take` (default 0/5) | Get execution history for the rule |

**Key Patterns:**
- Backed by `AutomationFunctions`; rules persisted as `ItemAutomation`, runs logged as `ItemAutomationExecution`
- Family-shared or user-scoped via `IsSharedWithFamily` (sets `FamilyId` vs `UserId`); ownership/family access validated on every operation
- Action types: `AutoConsume`, `AddToShoppingList`, `LowStockAddToShoppingList`, `NotifyOnly`
- Schedule types: `Interval` (every N days) and `FixedDate` (days-of-week or day-of-month); `NextExecutionAt` computed in the user's timezone
- Low-stock rules are event-driven (no schedule) and cannot be manually executed
- Units are always inherited from the related product, never supplied by the client

### CalendarController

Aggregates calendar events (inventory expirations, automation executions, shopping-list deadlines) within a date range (requires `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/` | Get calendar events for the date range in the request body |

**Key Patterns:**
- Request body carries `StartDate` / `EndDate` (`DateOnly`); the range may not exceed 93 days (validated, else 400)
- Dates are converted to UTC day boundaries before querying
- Backed by `CalendarFunctions`; returns `List<CalendarEventInfo>`

### NotificationController

The notification centre (#116): the caller's own inbox of everything `Homassy.Notifications` has
sent them, and its read state. Class-level `[Authorize]`.

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | One page of the caller's notifications, newest first (`?cursor=`, `?pageSize=`) |
| GET | `/unread-count` | The caller's unread count |
| POST | `/{publicId}/read` | Mark one read (idempotent) |
| POST | `/read-all` | Mark every unread one read |
| DELETE | `/{publicId}` | Dismiss one (a soft delete) |

**Key Patterns:**
- **Rows carry a type and parameters, never rendered text.** `NotificationInfo.Type` is the
  `NotificationType` member's *name* (`"ShoppingListItemsAdded"`), which is the key the client looks
  the localized template up under; the numeric value stays an implementation detail of the column.
  So the text is composed in whatever language the reader is using when they open the inbox, and
  fixing a notification's wording fixes it retroactively
- **Cursor-paged**, on `(CreatedAt, PublicId)` via the existing `Models/Activity/ActivityCursor` -
  reused rather than reimplemented, since it is a codec for exactly that pair. A worker iteration
  writes a whole batch in one tick, and a page boundary that falls inside a tied group is what
  makes an inbox lose or repeat rows. An undecodable cursor is a **400**, never a 500
- **Every mutating endpoint answers with the new unread count**, so the header badge is corrected
  by the act of acting and the client never has to guess it or re-fetch to find out
- **Scoped to the caller by `SessionInfo.GetUserId()`, never by an id from the request.** Read
  state is personal, and one family member must not read or dismiss another's. A row that is not
  the caller's answers **404**, indistinguishably from one that does not exist - distinguishing
  them would let a caller probe whether a notification id exists for somebody else
- Rows are written by the notification workers alongside the push they send (see
  [../../Homassy.Notifications/CLAUDE.md](../../Homassy.Notifications/CLAUDE.md)), and pruned past
  a 60-day window by the same service

### StatisticsController

Exposes nightly-cached, global (platform-wide) counts (no authentication — `[AllowAnonymous]`).

**Endpoints:**

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/` | No | Get the cached global platform statistics |

**Key Patterns:**
- Served from the in-memory `StatisticsService` singleton (no per-request DB query)
- Cache refreshed once on startup and nightly at 02:00 UTC by `StatisticsRefreshWorker`
- Returns totals for products, inventory items, shopping lists, purchased items, shopping locations, and storage locations plus `LastUpdatedUtc`

---

