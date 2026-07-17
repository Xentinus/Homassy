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
- **SignalR Realtime (Shopping Lists)**: Each shopping list is a SignalR group; clients join the list they are viewing and receive live item/list events. Writes stay on the REST endpoints — after a successful commit the Functions layer broadcasts via the static `ShoppingListRealtime` helper
- **SignalR Realtime (Inventory / Készletek)**: Identity-derived groups (per-family + per-user, joined on connect) push live inventory/product events to every grid that can see the change; the Functions layer broadcasts light card-only payloads via the static `InventoryRealtime` helper after each commit, and out-of-process automation relays through the internal broadcast endpoint

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
│       ├── SanitizedStringAttribute.cs
│       └── ValidBarcodeAttribute.cs
├── Constants/              Application-wide constants
│   ├── ErrorCodeDescriptions.cs   Error code enum → human-readable map
│   └── TableNames.cs
├── Context/               Database context and session management
│   ├── HomassyDbContext.cs
│   ├── HomassyDbContextFactory.cs
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
│   ├── ShoppingListRealtime.cs    Static broadcast helper (ItemUpserted/ItemDeleted/ListUpdated/ListDeleted)
│   ├── InventoryHub.cs            Per-family + per-user groups joined on connect; JoinInventory returns the light grid snapshot
│   └── InventoryRealtime.cs       Static broadcast helper (InventoryUpserted/InventoryDeleted/ProductUpdated/ProductFavoriteChanged/ProductDeleted)
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
- Instantiate `Functions` classes
- Handle exceptions with consistent error mapping
- Return standardized `ApiResponse` objects

**Example from UserController:**

```csharp
[HttpGet("profile")]
[MapToApiVersion(1.0)]
public IActionResult GetProfile()
{
    var profileResponse = new UserFunctions().GetProfileAsync();
    return Ok(ApiResponse<UserProfileResponse>.SuccessResponse(profileResponse));
    // Exceptions bubble up to GlobalExceptionMiddleware
}
```

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
    public UserProfileResponse GetProfileAsync()
    {
        var userId = SessionInfo.GetUserId();
        var user = GetAllUserDataById(userId); // Cache-first data access

        // Business logic here

        return profileResponse;
    }
}
```

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

**Configuration in Program.cs:**
```csharp
HomassyDbContext.SetConfiguration(builder.Configuration);

builder.Services.AddDbContext<HomassyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

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
    var product = await new ProductFunctions().GetProductAsync(id, cancellationToken);
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
        // Skip public paths: /health, /openapi, /api/v1/version, /api/v1/errorcodes
        if (ShouldSkipAuthentication(context.Request.Path)) { ... }

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
app.UseResponseCompression();
app.Use(async (context, next) => { /* Security + App Headers */ });
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestTimeoutMiddleware>();
app.UseRequestLogging(builder.Configuration); // extension method
app.UseMiddleware<GlobalExceptionMiddleware>();

// OpenAPI only in Development
if (app.Environment.IsDevelopment()) app.MapOpenApi();

// HSTS + HTTPS only if enabled and not Development
if (httpsSettings.Enabled && httpsSettings.Hsts.Enabled && !app.Environment.IsDevelopment()) app.UseHsts();
if (httpsSettings.Enabled && !app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseCors("HomassyPolicy");
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<KratosSessionMiddleware>(); // validates Kratos session first
app.UseAuthentication();                     // reads ClaimsPrincipal set by KratosSessionMiddleware
app.UseAuthorization();
app.UseMiddleware<SessionInfoMiddleware>();   // populates AsyncLocal from claims
app.MapControllers();
```

**Order matters:**
1. **Response Compression** - Brotli and Gzip compression for responses
2. **Response Headers** - Adds security headers (CSP, X-Frame-Options, HSTS, etc.) and app metadata; removes `Server` / `X-Powered-By`
3. **Correlation ID** - Generates/propagates `X-Correlation-ID` for request tracing
4. **Request Timeout** - Enforces per-endpoint timeout limits
5. **Request Logging** - Logs HTTP requests/responses (sanitized) via extension method `UseRequestLogging`
6. **Global Exception Handler** - Catches and maps all unhandled exceptions
7. **OpenAPI** - Swagger UI (development only)
8. **HSTS** - HTTP Strict Transport Security (non-dev, if enabled)
9. **HTTPS Redirection** - Forces HTTPS (non-dev, if enabled)
10. **CORS** - Cross-Origin Resource Sharing (configurable origins, allows localhost always)
11. **Rate Limiting** - Global and per-endpoint request throttling
12. **Kratos Session** - Calls Kratos `/sessions/whoami`, sets `context.User` and `HttpContext.Items["KratosSession"]`
13. **Authentication** - Reads the `ClaimsPrincipal` already set by KratosSessionMiddleware
14. **Authorization** - Enforces `[Authorize]` attributes
15. **Session Info** - Extracts user/family IDs from claims into `AsyncLocal` (`SessionInfo`)
16. **Controllers** - Route to endpoints

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
