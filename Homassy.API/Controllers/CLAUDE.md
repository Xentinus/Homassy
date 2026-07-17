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
| POST | `/profile-picture` | Upload profile picture synchronously (legacy) |
| POST | `/profile-picture/upload-async` | Upload profile picture asynchronously (returns job ID) |
| DELETE | `/profile-picture` | Delete profile picture |
| GET | `/notification` | Get notification preferences |
| PUT | `/notification` | Update notification preferences |
| GET | `/bulk` | Get multiple users by comma-separated public IDs (`?publicIds=...`) |
| GET | `/activities` | Paginated activity history with optional filters |
| GET | `/push/vapid-key` | Get VAPID public key for push subscription |
| POST | `/push/subscribe` | Subscribe device for push notifications |
| POST | `/push/unsubscribe` | Unsubscribe device from push notifications |
| POST | `/push/test` | Send a test push notification to current device |

**Key Patterns:**
- All endpoints require authentication
- Base64 image upload for profile pictures
- Async image upload returns a `jobId`; track progress via `GET /progress/{jobId}`
- Push notifications use Web Push API (VAPID)
- Activity feed supports pagination (`pageNumber`, `pageSize`, `returnAll`) and filtering by type/date/user

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

### ProductController

Manages product catalog and inventory (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Get all products |
| POST | `/` | Create new product |
| PUT | `/{productPublicId}` | Update product |
| DELETE | `/{productPublicId}` | Delete product |
| POST | `/{productPublicId}/favorite` | Toggle favorite status |
| GET | `/{productPublicId}/detailed` | Get detailed product info with inventory |
| GET | `/detailed` | Get all detailed products for user |

**Key Patterns:**
- Product customization per user (favorites, notes)
- Inventory tracking with purchase info and consumption logs
- Family-shared products support

**Realtime (SignalR):**
- Hub at `/hubs/inventory` (`InventoryHub`, `[Authorize]`) — same Kratos-cookie-on-handshake auth as the shopping-list hub
- Groups are identity-derived (not per-resource): on connect each connection joins `inventory:user:{userId}` and, if the user has a family, `inventory:family:{familyId}` — matching the grid's visibility filter (`item.UserId == me || item.FamilyId == myFamily`). `JoinInventory()` returns the light `List<InventoryGridProductInfo>` snapshot (only the fields the grid cards render)
- After a successful write, `ProductFunctions` (and the in-process automation consume path in `AutomationFunctions`) broadcasts via the static `InventoryRealtime` helper: `InventoryUpserted` (create/quick-add/update/partial-consume/split/move — carries a light product + item), `InventoryDeleted` (delete / consume-to-zero), `ProductUpdated` (catalog fields), `ProductFavoriteChanged` (per-user, user group only), `ProductDeleted`. Family-shared items route to the family group, personal items to the user group
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
| GET | `/storage` | Get all storage locations |
| POST | `/storage` | Create storage location |
| PUT | `/storage/{publicId}` | Update storage location |
| DELETE | `/storage/{publicId}` | Delete storage location |

**Key Patterns:**
- Two location types: Shopping (stores) and Storage (home locations)
- Color coding support for UI
- Family sharing via `IsSharedWithFamily` flag
- Ownership validation for modifications
- Shopping locations carry optional `Latitude`/`Longitude` (nullable `double`) — geocoded on the client at save time and sent in `ShoppingLocationRequest`; on update they are treated as a pair. Powers the frontend shopping-list proximity ("you are here") feature. No server-side geocoding.
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

**Query Parameters:**
- `showPurchased` (bool, default: false) - Include purchased items older than 1 day

**Key Patterns:**
- Hierarchical structure: Lists contain Items
- Items can reference Products or use custom names
- Purchased items auto-hidden after 1 day (configurable via `showPurchased`)
- Family sharing support
- Shopping location assignment per item — `PUT /item/{publicId}` can reassign it (`ShoppingLocationPublicId`) or clear it (`ClearShoppingLocation: true`, needed because a null id means "no change")

**Realtime (SignalR):**
- Hub at `/hubs/shopping-list` (`ShoppingListHub`, `[Authorize]`) — the Kratos session cookie rides the WebSocket handshake, so the existing auth pipeline works unchanged
- `JoinList(publicId, showPurchased)` joins the list's group (`shopping-list:{publicId}`) and returns the current `DetailedShoppingListInfo` snapshot via the same access-checked path as the REST endpoint; `LeaveList(publicId)` leaves the group
- After a successful REST write, `ShoppingListFunctions` broadcasts through the static `ShoppingListRealtime` helper: `ItemUpserted` (create/update/purchase/restore, hydrated item), `ItemDeleted`, `ListUpdated`, `ListDeleted`
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

