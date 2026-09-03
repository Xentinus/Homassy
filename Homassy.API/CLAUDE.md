# Homassy.API - Project Architecture Documentation

> **Note:** This is a living document that is updated as the project evolves.
> It is not versioned - changes are made directly to reflect the current state of the project.

## Documentation Map

This file is the entry point and carries the essentials. Detailed reference is split out so only what you need gets loaded — subdirectory `CLAUDE.md` files load automatically when you work in that folder, and the `docs/*.md` files are linked below.

| Area | Doc | Read when |
|------|-----|-----------|
| Controllers & endpoints | [Controllers/CLAUDE.md](Controllers/CLAUDE.md) | adding or changing a controller or endpoint |
| Middleware details | [Middleware/CLAUDE.md](Middleware/CLAUDE.md) | touching compression, CORS, correlation, timeout, request logging, exception handling, rate limiting, or security headers |
| Services, workers, health | [Services/CLAUDE.md](Services/CLAUDE.md) | working on application/background services, health checks, or Serilog |
| Entities, triggers, session | [Entities/CLAUDE.md](Entities/CLAUDE.md) | entity inheritance, DB triggers, or session context |
| Security & validation | [docs/security-and-validation.md](docs/security-and-validation.md) | input sanitization, barcode validation, or image upload |
| Feature deep-dives | [docs/features.md](docs/features.md) | error codes, push, activity feed, automation, family join, lockout, graceful shutdown |
| Development guidelines | [docs/development-guidelines.md](docs/development-guidelines.md) | how to add a controller, functions class, DTO, entity, etc. |

## Overview

Homassy.API is a home storage management system built with ASP.NET Core. The project follows a **Controller → Functions** architecture pattern, eschewing the traditional repository pattern in favor of direct database access within a dedicated business logic layer. The system emphasizes performance through aggressive in-memory caching with database trigger-based invalidation.

### Key Architectural Decisions

- **No Repository Pattern**: Functions layer directly accesses DbContext for simplicity
- **Cache-First Architecture**: Heavy use of in-memory caching with database trigger-based invalidation
- **Ory Kratos Authentication**: Self-hosted identity management with passwordless login (verification codes)
- **Functions Over Services**: Business logic in dedicated Functions classes rather than traditional service layer
- **Soft Delete by Default**: All entities support soft deletion via inheritance
- **Session via AsyncLocal**: User context stored in thread-local storage for easy access throughout the application
- **Static Service Initialization**: Services like ConfigService use static initialization
- **Standardized API Responses**: All endpoints return consistent `ApiResponse<T>` structure
- **Kratos Email Delivery**: Kratos Courier handles authentication emails (verification, recovery, login codes)
- **Correlation ID Tracking**: Request tracing across the application for distributed systems
- **Health Check Integration**: Kubernetes-compatible health probes for monitoring and orchestration
- **Kratos Session Management**: Secure session handling via Kratos with configurable lifespans
- **Centralized Exception Handling**: GlobalExceptionMiddleware for consistent error responses
- **Per-Endpoint Timeouts**: Configurable timeout enforcement to prevent long-running requests
- **Request/Response Logging**: Sanitized logging with sensitive data filtering for observability
- **Input Sanitization**: Automatic XSS protection via `[SanitizedString]` validation attribute
- **Barcode Validation**: Multi-format barcode validation with checksum verification (EAN-13, EAN-8, UPC-A, UPC-E, Code-128)
- **Image Processing**: Secure image upload with magic number validation, format detection, and dimension constraints
- **Async Progress Tracking**: Long-running operations (e.g. image uploads) tracked via `ProgressTrackerService` with job IDs
- **Push Notifications**: Web Push API (VAPID) for browser push notifications with per-user subscription management
- **Activity Feed**: Per-family activity log tracking create/update/delete operations across entities
- **Error Code System**: Typed `ErrorCodes` enum with descriptions instead of plain string messages in all API error responses
- **Account Lockout**: Automatic account lockout after repeated failed login attempts via `AccountLockoutService`
- **Graceful Shutdown**: Configurable drain period before process exit, ensuring in-flight requests complete
- **CORS Support**: Configurable cross-origin resource sharing for web clients
- **Response Compression**: Brotli and Gzip for improved performance
- **SignalR Realtime (Shopping Lists)**: Each shopping list is a SignalR group; clients join the list they are viewing and receive live item/list events. Writes stay on the REST endpoints — after a successful commit the Functions layer broadcasts via the injected `ShoppingListRealtime` helper
- **SignalR Realtime (Inventory / Készletek)**: Identity-derived groups (per-family + per-user, joined on connect) push live inventory/product events to every grid that can see the change; the Functions layer broadcasts light card-only payloads via the injected `InventoryRealtime` helper after each commit, and out-of-process automation relays through the internal broadcast endpoint

---

## Technology Stack

### Framework & Runtime
- **.NET 10.0**
- **ASP.NET Core Web API**

### Database
- **PostgreSQL** - Primary database
- **Entity Framework Core 10.0.0** - ORM
- **Npgsql 10.0.0** - PostgreSQL provider

### Authentication & Identity
- **Ory Kratos** - Self-hosted identity management
- Session-based authentication with cookie/token support
- Passwordless login via email verification codes
- Account recovery and settings management via Kratos flows

### Email
- **Kratos Courier** - Handles authentication-related emails through Kratos

### Logging
- **Serilog 9.0.0** - Structured logging
  - Console sink
  - File sink (rolling daily, 14-day retention)

### API Features
- **Asp.Versioning 8.1.0** - API versioning
- **OpenAPI** - API documentation (built-in)
- **Microsoft.AspNetCore.Diagnostics.HealthChecks** - Health monitoring
- **Response Compression** - Brotli and Gzip support

---

## Project Structure

```
Homassy.API/
├── Constants/              Application-wide constants
├── Context/               Database context and session management
│   ├── HomassyDbContext.cs
│   └── SessionInfo.cs
├── Attributes/           Custom validation attributes
│   └── Validation/
│       ├── PublicFeedUrlAttribute.cs   Server-fetchable URL (https, public host) — anti-SSRF
│       ├── SanitizedStringAttribute.cs
│       └── ValidBarcodeAttribute.cs
├── Constants/              Application-wide constants
│   ├── ErrorCodeDescriptions.cs   Error code enum → human-readable map
│   └── TableNames.cs
├── Context/               Database context and session management
│   ├── HomassyDbContext.cs
│   ├── HomassyDbContextFactory.cs             Design-time factory, for EF tooling only
│   ├── HomassyDbContextFactoryExtensions.cs   CreateForReading() — the no-tracking context
│   └── SessionInfo.cs
├── Controllers/           HTTP endpoint handlers (thin layer)
│   ├── AuthController.cs
│   ├── AutomationController.cs    Item-automation rule management
│   ├── CalendarController.cs      Calendar event aggregation
│   ├── ErrorCodesController.cs    Error code reference (public)
│   ├── FamilyController.cs
│   ├── HealthController.cs
│   ├── LocationController.cs
│   ├── OpenFoodFactsController.cs
│   ├── ProductController.cs
│   ├── ProgressController.cs      Job progress tracking
│   ├── SelectValueController.cs
│   ├── ShoppingListController.cs
│   ├── StatisticsController.cs    Public global platform statistics
│   ├── UserController.cs
│   └── VersionController.cs
├── Entities/              Database entity models
│   ├── Activity/
│   │   └── Activity.cs
│   ├── Common/           Base entities
│   │   ├── BaseEntity.cs
│   │   ├── SoftDeleteEntity.cs
│   │   ├── RecordChangeEntity.cs
│   │   └── TableRecordChange.cs
│   ├── Family/
│   │   ├── Family.cs
│   │   └── FamilyJoinRequest.cs    Approval-gated join request
│   ├── Location/
│   │   ├── LocationBase.cs
│   │   ├── ShoppingLocation.cs
│   │   └── StorageLocation.cs
│   ├── Product/
│   │   ├── Product.cs
│   │   ├── ProductConsumptionLog.cs
│   │   ├── ProductCustomization.cs
│   │   ├── ProductInventoryItem.cs
│   │   ├── ProductPurchaseInfo.cs
│   │   ├── ItemAutomation.cs            Automation rule (schedule/threshold)
│   │   └── ItemAutomationExecution.cs   Automation execution log entry
│   ├── ShoppingList/
│   │   ├── ShoppingList.cs
│   │   └── ShoppingListItem.cs
│   └── User/
│       ├── User.cs
│       ├── UserNotificationPreferences.cs
│       ├── UserProfile.cs
│       └── UserPushSubscription.cs
├── Enums/                Application enumerations
│   ├── ActivityType.cs
│   ├── BarcodeFormat.cs
│   ├── Currency.cs
│   ├── ErrorCode.cs               Typed error codes for all API error responses
│   ├── ImageFormat.cs
│   ├── ImageValidationError.cs
│   ├── Language.cs
│   ├── ProductCategory.cs
│   ├── SelectValueType.cs
│   ├── StoreType.cs
│   ├── Unit.cs
│   ├── UserStatus.cs
│   └── UserTimeZone.cs
├── Exceptions/           Custom exception classes
│   ├── AccountLockedException.cs  429 – account temporarily locked
│   ├── AuthException.cs           Base auth exception with StatusCode
│   ├── LocationException.cs
│   ├── ProductException.cs
│   ├── RequestTimeoutException.cs
│   └── ShoppingListException.cs
├── Extensions/           Extension methods
│   ├── CurrencyExtensions.cs
│   ├── HttpContextExtensions.cs    GetKratosSession() helper
│   ├── LanguageExtensions.cs
│   ├── QueryableExtensions.cs
│   ├── RequestLoggingMiddlewareExtensions.cs
│   ├── StringExtensions.cs
│   ├── UnitExtensions.cs
│   └── UserTimeZoneExtensions.cs
├── Functions/            Business logic layer (replaces traditional services)
│   ├── ActivityFunctions.cs       Activity feed queries
│   ├── AutomationFunctions.cs     Item-automation CRUD, scheduling, execution, low-stock check
│   ├── CalendarFunctions.cs       Calendar event aggregation
│   ├── FamilyFunctions.cs
│   ├── FamilyJoinRequestFunctions.cs  Approval-gated family join requests
│   ├── FunctionsRuntime.cs        The layer's cross-cutting deps as one typed parameter object
│   ├── ImageFunctions.cs          Image upload/delete for products & profiles
│   ├── LocationFunctions.cs
│   ├── ProductFunctions.cs
│   ├── PushNotificationFunctions.cs  Subscribe/unsubscribe/send notifications
│   ├── SelectValueFunctions.cs
│   ├── ShoppingListFunctions.cs
│   ├── TimeZoneFunctions.cs
│   ├── UnitFunctions.cs
│   └── UserFunctions.cs
├── HealthChecks/         Health check implementations
│   └── OpenFoodFactsHealthCheck.cs
├── Hubs/                 SignalR realtime hubs
│   ├── ShoppingListHub.cs         Per-list groups; JoinList returns the current snapshot
│   ├── ShoppingListRealtime.cs    Broadcast helper, singleton (ItemUpserted/ItemDeleted/ListUpdated/ListDeleted)
│   ├── InventoryHub.cs            Per-family + per-user groups joined on connect; JoinInventory returns the light grid snapshot
│   └── InventoryRealtime.cs       Broadcast helper, singleton (InventoryUpserted/InventoryDeleted/ProductUpdated/ProductFavoriteChanged/ProductDeleted)
├── Infrastructure/       Infrastructure components
│   └── DatabaseTriggerInitializer.cs
├── Middleware/           Custom middleware
│   ├── CorrelationIdMiddleware.cs
│   ├── GlobalExceptionMiddleware.cs
│   ├── KratosSessionMiddleware.cs  Validates Kratos session + populates HttpContext
│   ├── RateLimitingMiddleware.cs
│   ├── RequestLoggingMiddleware.cs
│   ├── RequestTimeoutMiddleware.cs
│   └── SessionInfoMiddleware.cs
├── Migrations/           EF Core database migrations
├── Models/               DTOs and request/response models
│   ├── Activity/         Activity feed DTOs (ActivityInfo, GetActivitiesRequest)
│   ├── ApplicationSettings/  (HttpsSettings, GracefulShutdownSettings, etc.)
│   ├── Barcode/          Barcode validation result models
│   ├── Common/           Shared models (ApiResponse, PagedResult, SelectValue, VersionInfo)
│   ├── Family/
│   ├── HealthCheck/
│   ├── ImageUpload/      Image upload request/response models
│   ├── Kratos/           Kratos session/config models
│   ├── Location/
│   ├── OpenFoodFacts/
│   ├── Product/
│   ├── ProgressInfo.cs   Progress tracking DTO
│   ├── PushNotification/ (CreatePushSubscriptionRequest, VapidPublicKeyResponse, etc.)
│   ├── RateLimit/
│   ├── RequestLoggingOptions.cs
│   ├── ShoppingList/
│   └── User/
├── Security/            Security utilities
│   ├── CorsOriginPolicy.cs    Origin allowlist matching for the CORS policy
│   ├── ExternalUrlGuard.cs    Screens user-supplied fetch targets (anti-SSRF), incl. the connect-time DNS re-check
│   └── SecureCompare.cs
└── Services/            Application services
    ├── Background/      Background hosted services
    │   ├── StatisticsRefreshWorker.cs   Nightly global-statistics cache refresh
    │   └── TokenCleanupService.cs
    ├── Sanitization/
    │   ├── IInputSanitizationService.cs
    │   └── InputSanitizationService.cs
    ├── AccountLockoutService.cs
    ├── BarcodeValidationService.cs
    ├── CacheManagementService.cs  (IHostedService – cache invalidation)
    ├── ConfigService.cs
    ├── GracefulShutdownService.cs (IHostedService – drain on shutdown)
    ├── IBarcodeValidationService.cs
    ├── IImageProcessingService.cs
    ├── ImageProcessingService.cs
    ├── KratosService.cs
    ├── OpenFoodFactsService.cs
    ├── ProgressTrackerService.cs
    ├── RateLimitCleanupService.cs (IHostedService)
    ├── RateLimitService.cs
    └── StatisticsService.cs       Singleton in-memory global-statistics cache
```

---

## Architecture Patterns

### Controller → Functions Pattern

The project uses a unique **Controller → Functions** architecture instead of the traditional Controller → Service → Repository pattern.

#### Controllers (Thin HTTP Layer)

Controllers are kept deliberately thin and focused on HTTP concerns:

- Validate `ModelState`
- Take the `Functions` classes they need through the constructor
- Handle exceptions with consistent error mapping
- Return standardized `ApiResponse` objects

**Example from UserController:**

```csharp
private readonly UserFunctions _userFunctions;

public UserController(UserFunctions userFunctions)
{
    _userFunctions = userFunctions;
}

[HttpGet("profile")]
[MapToApiVersion(1.0)]
public IActionResult GetProfile()
{
    var profileResponse = _userFunctions.GetProfileAsync();
    return Ok(ApiResponse<UserProfileResponse>.SuccessResponse(profileResponse));
    // Exceptions bubble up to GlobalExceptionMiddleware
}
```

Never `new` a `Functions` class from a controller, hub or service: they are registered as scoped
services in `Program.cs`, and that registration is what supplies the context factory below.

#### Functions (Business Logic + Data Access)

Functions classes contain all domain logic and directly interact with the database:

- Contains all business logic
- Directly interacts with `DbContext`
- Manages in-memory caching
- Handles database transactions
- Cache-first read pattern (check cache, fallback to DB)

**Pattern:**
```csharp
public class UserFunctions
{
    private readonly IDbContextFactory<HomassyDbContext> _contextFactory;

    public UserFunctions(IDbContextFactory<HomassyDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public UserProfileResponse GetProfileAsync()
    {
        var userId = SessionInfo.GetUserId();
        var user = GetAllUserDataById(userId); // Cache-first data access

        // Business logic here

        return profileResponse;
    }
}
```

Inside the layer the classes still instantiate each other, passing their dependency along
(`new ActivityFunctions(_contextFactory)`, `new ProductFunctions(_runtime)`). That is
deliberate: `UserFunctions` ↔ `FamilyFunctions` and `ProductFunctions` ↔ `AutomationFunctions`
are mutually dependent, so constructor injection between them would be an unresolvable cycle.

Which of the two constructors a class takes follows from what it needs:

| Constructor | Classes | Why |
|---|---|---|
| `IDbContextFactory<HomassyDbContext>` | Activity, Family, FamilyJoinRequest, PushNotification, User | They need nothing but a context, and only ever construct each other — a closed set, so they also work in a host with no SignalR hubs (`Homassy.Notifications` borrows two of them) |
| `FunctionsRuntime` | Automation, Calendar, ExternalCalendar, Image, Location, Product, SelectValue, ShoppingList | They broadcast over SignalR, need a scope of their own, or construct a class that does |

`FunctionsRuntime` is a parameter object, not a service locator: every member is a declared,
typed dependency (`ContextFactory`, `ScopeFactory`, `Inventory`, `MasterData`, `ShoppingList`).
It exists so adding one more broadcast to one class does not re-cascade a constructor parameter
through every class that constructs it. Read its XML docs before changing its shape.

#### DbContext lifetime — two rules, no exceptions

```csharp
// An operation that only reads
using var context = _contextFactory.CreateForReading();

// An operation that writes
using var context = _contextFactory.CreateDbContext();
...
await context.SaveChangesAsync(cancellationToken);
```

1. **Always `using`, always from the factory.** A context that is not disposed keeps its
   `NpgsqlConnection` leased from the pool and keeps every entity it materialised alive until
   the finalizer runs. Under sustained traffic that shows up as memory climbing with requests
   served and, once the 100-connection pool is exhausted, requests blocking on `Timeout`
   instead of failing fast. `HomassyDbContext` has no parameterless constructor, so there is no
   way to create one that the DI registration did not configure.
2. **`CreateForReading()` unless the method saves.** It sets
   `NoTrackingWithIdentityResolution`, so rows are not entered into a change tracker nothing
   will ever save through — roughly half the allocation per row on list endpoints, and it is
   what keeps the bulk cache loads from pinning whole tables. Calling `SaveChanges` on such a
   context throws rather than silently writing nothing.

A context is method-local, so "does this method save?" is the whole decision.

A method that creates a context therefore cannot be `static` — it needs the instance field.

### In-Memory Caching Strategy

The Functions classes implement a sophisticated caching mechanism:

- **Thread-Safe Storage**: Uses `ConcurrentDictionary` for cache storage
- **Cache-First Pattern**: Always check cache before hitting the database
- **Cache Initialization**: Loaded on application startup
- **Trigger-Based Invalidation**: PostgreSQL triggers notify the cache system via `TableRecordChanges` table
- **Background Refresh**: `CacheManagementService` periodically refreshes caches

**Benefits:**
- Dramatic reduction in database queries
- Improved response times
- Automatic cache invalidation on data changes

---

## Database Layer

### Entity Framework Core with PostgreSQL

The project uses Entity Framework Core as the ORM with PostgreSQL as the database backend.

**Configuration in Program.cs** — the only place a context is configured:
```csharp
Action<DbContextOptionsBuilder> configureDbContext = options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));

// What the Functions layer uses: one context per operation, disposed with it.
builder.Services.AddDbContextFactory<HomassyDbContext>(configureDbContext);
// The ambient request context, for the few consumers that want one (startup trigger
// initialisation, the integration tests). Registering both requires singleton options.
builder.Services.AddDbContext<HomassyDbContext>(configureDbContext, optionsLifetime: ServiceLifetime.Singleton);
```

EF tooling gets its context from `Context/HomassyDbContextFactory.cs`
(`IDesignTimeDbContextFactory`), which builds the options itself.

> The entity inheritance hierarchy, the PostgreSQL trigger-based cache invalidation, and the per-request session context are documented in [Entities/CLAUDE.md](Entities/CLAUDE.md).


## API Conventions

### API Versioning

The API uses URL segment versioning:

```csharp
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[MapToApiVersion(1.0)]
```

**Example URLs:**
- `GET /api/v1.0/auth/me`
- `POST /api/v1.0/user/profile-picture`
- `GET /api/v1.0/family`

**Configuration:**
- Default version: 1.0
- Versioning library: `Asp.Versioning 8.1.0`
- Version in URL segment (not header or query string)

### Standardized Response Format

All API endpoints return a consistent `ApiResponse<T>` structure:

```csharp
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public List<string>? Errors { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

**Success Response Example:**
```json
{
  "Success": true,
  "Data": {
    "userId": "123e4567-e89b-12d3-a456-426614174000",
    "email": "user@example.com"
  },
  "Message": "Profile retrieved successfully",
  "Errors": null,
  "Timestamp": "2025-12-02T10:30:00Z"
}
```

**Error Response Example:**
```json
{
  "Success": false,
  "Data": null,
  "Message": null,
  "Errors": ["User not found"],
  "Timestamp": "2025-12-02T10:30:00Z"
}
```

**Usage in Controllers:**
```csharp
// Success with data
return Ok(ApiResponse<UserProfileResponse>.SuccessResponse(profileData));

// Success with message only
return Ok(ApiResponse.SuccessResponse("Settings updated successfully"));

// Error
return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));

// Error with multiple messages
return BadRequest(ApiResponse.ErrorResponse(validationErrors));
```

### Model Validation

Controllers use the `[ApiController]` attribute for automatic model validation:

```csharp
[HttpPost("settings")]
public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingsRequest request)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
    }
    // Process request...
}
```

- Validation attributes on DTOs (e.g., `[Required]`, `[EmailAddress]`)
- Automatic 400 Bad Request if validation fails
- Manual `ModelState` check in endpoints for consistent error responses

### Exception Handling Pattern

Most controllers rely on `GlobalExceptionMiddleware` for exception handling (no try-catch boilerplate required). Minimal try-catch is used only when a specific response shape is needed:

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetProduct(Guid id, CancellationToken cancellationToken)
{
    // No try-catch needed — GlobalExceptionMiddleware handles all exceptions
    var product = await _productFunctions.GetProductAsync(id, cancellationToken);
    return Ok(ApiResponse<ProductResponse>.SuccessResponse(product));
}
```

**Error Codes System:**

All API errors use a typed `ErrorCodes` enum (in `Enums/ErrorCode.cs`) rather than raw strings, with human-readable descriptions in `Constants/ErrorCodeDescriptions.cs`. This ensures consistency and makes error codes discoverable via the `ErrorCodesController`:

```csharp
return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
return Unauthorized(ApiResponse.ErrorResponse(ErrorCodes.AuthInvalidCredentials));
return StatusCode(403, ApiResponse.ErrorResponse(ErrorCodes.AuthRegistrationDisabled));
```

**Custom Exception Hierarchy:**
- `AuthException` - Base authentication exception with `StatusCode` and `ErrorCode` property
- `AccountLockedException` - Account locked after too many attempts (429) – subclass of `AuthException`
- `LocationException` – Wraps location-related errors (not found, access denied, invalid)
- `ProductException` – Wraps product-related errors (not found, access denied, invalid)
- `ShoppingListException` – Wraps shopping list errors (not found, access denied, item not found, etc.)
- `RequestTimeoutException` - Request timeout (504)

**GlobalExceptionMiddleware mapping:**
- `AuthException` → custom `StatusCode` from exception
- `AccountLockedException` → 429 with `LockedUntil` and `RemainingSeconds` in response
- `ProductException`, `LocationException`, `ShoppingListException` → 404 / 403 depending on subtype
- `RequestTimeoutException` → 504 Gateway Timeout
- `OperationCanceledException` → 499 Client Closed Request
- All other exceptions → 500 Internal Server Error (no stack trace exposed)

---

## Authentication & Authorization

### Ory Kratos Integration

The system uses **Ory Kratos** as a self-hosted identity management solution. All authentication flows (login, registration, recovery, settings) are handled by Kratos.

#### Architecture Overview

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Frontend      │────▶│  Ory Kratos     │────▶│  PostgreSQL     │
│   (Nuxt.js)     │◀────│  (Identity)     │◀────│  (Kratos DB)    │
└────────┬────────┘     └─────────────────┘     └─────────────────┘
         │                       │
         ▼                       ▼
┌─────────────────┐     ┌─────────────────┐
│   Homassy API   │────▶│  PostgreSQL     │
│   (.NET Core)   │◀────│  (App DB)       │
└─────────────────┘     └─────────────────┘
```

**Key Components:**
- **Kratos Public API** (port 4433): Handles user-facing authentication flows
- **Kratos Admin API** (port 4434): Internal API for identity management
- **Kratos Courier**: Sends authentication emails (verification codes, recovery links)

#### Authentication Flow

1. **Login/Registration**
   - Frontend initiates flow via Kratos self-service endpoints
   - User enters email address
   - Kratos sends 6-digit verification code via email
   - User submits code to complete authentication
   - Kratos creates session cookie

2. **Session Validation**
   - API validates Kratos session via `KratosAuthenticationHandler`
   - Session data stored in `HttpContext.Items["KratosSession"]`
   - Local user record synced with Kratos identity

3. **Accessing Protected Endpoints**
   - Frontend includes Kratos session cookie (`ory_kratos_session`) or `X-Session-Token` header
   - `KratosSessionMiddleware` calls Kratos `/sessions/whoami` to validate the session
   - On success, sets `context.User` claims and stores session in `HttpContext.Items["KratosSession"]`
   - `SessionInfo` middleware populates the per-request `AsyncLocal` context with user/family IDs

#### KratosSessionMiddleware

Validates every incoming request's Kratos session **before** the ASP.NET Core authentication pipeline:

```csharp
public class KratosSessionMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Route-driven skip: the endpoint carries [AllowAnonymous] (Health, Version,
        // ErrorCodes, Statistics, Internal, and MapOpenApi). Never a path list — the
        // versioned routes are /api/v1.0/..., so literal paths silently stop matching.
        if (ShouldSkipAuthentication(context)) { ... }

        // Extract from cookie (ory_kratos_session) or X-Session-Token header
        var session = await kratosService.GetSessionAsync(cookie, token, ct);

        if (session != null && session.Active)
        {
            // Build ClaimsPrincipal from Kratos identity
            context.User = new ClaimsPrincipal(identity);
            // Store for later use by controllers
            context.Items["KratosSession"] = session;
        }

        await _next(context);
    }
}
```

Two short-circuits keep Kratos out of the hot path:

- **`[AllowAnonymous]` on the matched endpoint.** Requires the middleware to run after
  `UseRouting` (Program.cs calls it explicitly). Marking a controller `[AllowAnonymous]` is
  therefore what makes it public — there is no path list to keep in sync, and no prefix match
  that can classify `/api/v1.0/healthy-secrets` as public because `/api/v1.0/health` is public.
- **No credentials on the request.** `whoami` without a cookie or `X-Session-Token` can only
  answer 401, so the round trip is skipped.

The middleware resolves `IKratosService` from `context.RequestServices` — that *is* the
request's scope. Creating another scope per request builds a second set of scoped services for
nothing.

Controllers access the session via an extension method:
```csharp
var kratosSession = HttpContext.GetKratosSession(); // returns KratosSession?
```

#### KratosAuthenticationHandler

A lightweight custom `AuthenticationHandler` registered under the `"Kratos"` scheme. It reads the `ClaimsPrincipal` that was **already set** by `KratosSessionMiddleware`, allowing ASP.NET Core's `[Authorize]` attribute to work without re-validating the session.

```csharp
builder.Services.AddAuthentication("Kratos")
    .AddScheme<AuthenticationSchemeOptions, KratosAuthenticationHandler>("Kratos", _ => { });
```

#### User Synchronization

Local user records are synchronized with Kratos identities:

```csharp
public async Task<User?> EnsureLocalUserAsync(KratosSession session, CancellationToken ct)
{
    var kratosId = session.Identity.Id;
    var email = session.Identity.Traits.Email;
    var name = session.Identity.Traits.Name;

    // Find or create local user by Kratos ID
    var user = await FindByKratosIdAsync(kratosId, ct);
    
    if (user == null)
    {
        user = await CreateUserFromKratosAsync(kratosId, email, name, ct);
    }
    else
    {
        // Sync any changed traits
        await SyncUserTraitsAsync(user, session.Identity.Traits, ct);
    }
    
    return user;
}
```

#### Kratos Configuration

Kratos is configured via `kratos.yml`:

- **Passwordless Login**: Uses 6-digit codes sent via email
- **Session Lifespan**: 30 days in production (720h), 7 days in development (168h); set in the Kratos config, not the API (see `Homassy.Kratos`)
- **Cookie Settings**: SameSite=Lax, HttpOnly, Secure in production
- **Email Templates**: Customizable templates for verification, recovery

### Authorization

The system uses ASP.NET Core's attribute-based authorization:

```csharp
[Authorize]  // Entire controller requires Kratos session
public class UserController : ControllerBase
{
    [HttpGet("profile")]  // Inherits [Authorize]
    public IActionResult GetProfile() { ... }
}

// Or per-endpoint:
public class AuthController : ControllerBase
{
    [HttpGet("config")]  // No authentication required
    public IActionResult GetConfig() { ... }

    [Authorize]
    [HttpGet("me")]  // Requires valid Kratos session
    public IActionResult GetCurrentUser() { ... }
}
```

**Authorization Patterns:**
- **No role-based authorization**: All authenticated users have equal permissions
- **Family-scoped operations**: Validate `SessionInfo.GetFamilyId()` exists
- **User-scoped operations**: Validate `SessionInfo.GetUserId()` exists
- **Resource ownership**: Functions verify user owns the resource being accessed

### User Registration

Registration is handled entirely by Kratos:

- Frontend initiates registration flow via Kratos
- Kratos validates email and sends verification code
- On successful verification, Kratos creates identity
- API syncs local user record on first authenticated request (via `GET /auth/me`)
- Registration can be disabled via `"RegistrationEnabled": false` in `appsettings.json`
  - When disabled, existing local users can still log in, new users are blocked with **403**
  - The `GET /auth/config` endpoint exposes this flag so the frontend can hide the registration UI

---


## Cross-Cutting Concerns

### Middleware Pipeline

The middleware pipeline is configured in a specific order in `Program.cs`:

```csharp
if (forwardedHeadersSettings.Enabled) app.UseForwardedHeaders(); // must be first
app.UseResponseCompression();
app.Use(async (context, next) => { /* Security + App Headers */ });
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestTimeoutMiddleware>();
app.UseRequestLogging(builder.Configuration); // extension method
app.UseMiddleware<GlobalExceptionMiddleware>();

// OpenAPI only in Development
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();

// HSTS + HTTPS only if enabled and not Development
if (httpsSettings.Enabled && httpsSettings.Hsts.Enabled && !app.Environment.IsDevelopment()) app.UseHsts();
if (httpsSettings.Enabled && !app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseRouting();                            // explicit: the two middleware below read endpoint metadata
app.UseCors("HomassyPolicy");
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<KratosSessionMiddleware>(); // validates Kratos session first
app.UseAuthentication();                     // reads ClaimsPrincipal set by KratosSessionMiddleware
app.UseAuthorization();
app.UseMiddleware<SessionInfoMiddleware>();   // populates AsyncLocal from claims
app.MapControllers();
```

**Order matters:**
1. **Forwarded Headers** - Unwinds `X-Forwarded-For`/`-Proto` from configured proxies only. Must be first: everything below reads `RemoteIpAddress` and `Request.Scheme`
2. **Response Compression** - Brotli and Gzip compression for responses
3. **Response Headers** - Adds security headers (CSP, X-Frame-Options, HSTS, etc.) and app metadata; removes `Server` / `X-Powered-By`
4. **Correlation ID** - Generates/propagates `X-Correlation-ID` for request tracing
5. **Request Timeout** - Enforces per-endpoint timeout limits
6. **Request Logging** - Logs HTTP requests/responses (sanitized) via extension method `UseRequestLogging`
7. **Global Exception Handler** - Catches and maps all unhandled exceptions
8. **OpenAPI** - Swagger UI (development only)
9. **HSTS** - HTTP Strict Transport Security (non-dev, if enabled)
10. **HTTPS Redirection** - Forces HTTPS (non-dev, if enabled)
11. **Routing** - Matches the endpoint. Explicit, because rate limiting keys on the route template and the Kratos middleware reads `[AllowAnonymous]` from endpoint metadata
12. **CORS** - Cross-Origin Resource Sharing; allowlist only, plus a loopback shortcut in Development
13. **Rate Limiting** - Global and per-route-template request throttling
14. **Kratos Session** - Calls Kratos `/sessions/whoami`, sets `context.User` and `HttpContext.Items["KratosSession"]`; skipped for `[AllowAnonymous]` endpoints and for requests with no session credentials
15. **Authentication** - Reads the `ClaimsPrincipal` already set by KratosSessionMiddleware
16. **Authorization** - Enforces `[Authorize]` attributes
17. **Session Info** - Extracts user/family IDs from claims into `AsyncLocal` (`SessionInfo`)
18. **Controllers** - Route to endpoints

> Each middleware is documented in depth in [Middleware/CLAUDE.md](Middleware/CLAUDE.md). The remaining cross-cutting concerns (error codes, push, activity feed, automation, family join, lockout, graceful shutdown) live in [docs/features.md](docs/features.md); input sanitization, barcode, and image validation in [docs/security-and-validation.md](docs/security-and-validation.md); application/background services and health checks in [Services/CLAUDE.md](Services/CLAUDE.md).


## Summary

Homassy.API is a modern ASP.NET Core Web API with a unique architecture optimized for performance, observability, and developer productivity. Key takeaways:

- **Controller → Functions** pattern simplifies architecture
- **In-memory caching** with database triggers provides excellent performance
- **Ory Kratos** session-based passwordless authentication (no JWT, no refresh tokens)
- **KratosSessionMiddleware** validates sessions before ASP.NET Core's auth pipeline
- **Entity inheritance** provides soft delete and change tracking automatically
- **Standardized responses** with typed `ErrorCode` enum for all API errors
- **Comprehensive middleware** provides rate limiting, security headers, request tracing, and session management
- **Correlation ID tracking** enables distributed tracing across the application
- **Health checks** provide monitoring and Kubernetes-compatible orchestration support
- **Centralized exception handling** simplifies controller code and ensures consistent error responses
- **Request/response logging** with sensitive data filtering improves observability
- **Per-endpoint timeouts** prevent long-running requests from consuming resources
- **Input sanitization** with automatic XSS protection via `[SanitizedString]` validation attribute
- **Barcode validation** with multi-format support and checksum verification (EAN-13, EAN-8, UPC-A, UPC-E, Code-128)
- **Image processing** with magic number validation, format detection, and secure upload system
- **Async progress tracking** for long-running jobs (image uploads) via `ProgressTrackerService` and `ProgressController`
- **Web Push notifications** (VAPID) with per-device subscription management and scheduled sending
- **Activity feed** per-family audit log with pagination and filtering
- **Item automation engine** scheduled and low-stock (event-driven) actions over inventory, products, and shopping lists
- **Approval-gated family join requests** join-by-share-code requiring an existing member's approval
- **Global statistics** nightly-cached, public platform-wide counts
- **SignalR realtime** per-list groups push live shopping-list changes to viewing clients; writes remain on REST
- **Account lockout** after repeated failed auth attempts (429 with unlock timer)
- **Graceful shutdown** drain period for zero-downtime rolling restarts
- **Open Food Facts integration** enriches product data with barcode lookup and nutrition information
- **CancellationToken support** throughout for proper async operation handling and timeouts
- **Response compression** (Brotli/Gzip) improves performance for large payloads
- **CORS support** enables web client integration (localhost always allowed)

This architecture prioritizes:
- **Performance**: Aggressive caching, response compression, efficient async operations
- **Security**: Kratos session auth, rate limiting, account lockout, security headers, input sanitization, magic number validation, sanitized logging
- **Data Quality**: Barcode validation with checksum verification, image format validation, input sanitization, typed error codes
- **Observability**: Correlation IDs, request logging, health checks, activity feed, structured logging with Serilog
- **Resilience**: Graceful shutdown, timeout enforcement, graceful degradation, health monitoring
- **Maintainability**: Clear separation of concerns, consistent patterns, centralized error handling
- **Developer Experience**: Simple patterns, minimal boilerplate, easy to extend, comprehensive documentation
- **DevOps Readiness**: Kubernetes-compatible health probes, version endpoint, configurable timeouts, container-friendly design
