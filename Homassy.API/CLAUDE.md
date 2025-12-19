# Homassy.API - Project Architecture Documentation

> **Note:** This is a living document that is updated as the project evolves.
> It is not versioned - changes are made directly to reflect the current state of the project.

## Table of Contents

1. [Overview](#overview)
2. [Technology Stack](#technology-stack)
3. [Project Structure](#project-structure)
4. [Architecture Patterns](#architecture-patterns)
5. [Database Layer](#database-layer)
6. [API Conventions](#api-conventions)
7. [Authentication & Authorization](#authentication--authorization)
8. [Controllers Reference](#controllers-reference)
9. [Cross-Cutting Concerns](#cross-cutting-concerns)
10. [Development Guidelines](#development-guidelines)

---

## Overview

Homassy.API is a home storage management system built with ASP.NET Core. The project follows a **Controller → Functions** architecture pattern, eschewing the traditional repository pattern in favor of direct database access within a dedicated business logic layer. The system emphasizes performance through aggressive in-memory caching with database trigger-based invalidation.

### Key Architectural Decisions

- **No Repository Pattern**: Functions layer directly accesses DbContext for simplicity
- **Cache-First Architecture**: Heavy use of in-memory caching with database trigger-based invalidation
- **Passwordless Authentication**: Email verification codes instead of traditional passwords
- **Functions Over Services**: Business logic in dedicated Functions classes rather than traditional service layer
- **Soft Delete by Default**: All entities support soft deletion via inheritance
- **Session via AsyncLocal**: User context stored in thread-local storage for easy access throughout the application
- **Static Service Initialization**: Services like JwtService, EmailService, ConfigService use static initialization
- **Standardized API Responses**: All endpoints return consistent `ApiResponse<T>` structure
- **Async Email Queue**: Background email processing with retry logic for improved reliability
- **Correlation ID Tracking**: Request tracing across the application for distributed systems
- **Health Check Integration**: Kubernetes-compatible health probes for monitoring and orchestration
- **Refresh Token Rotation**: Token theft detection and prevention through automatic rotation
- **Centralized Exception Handling**: GlobalExceptionMiddleware for consistent error responses
- **Per-Endpoint Timeouts**: Configurable timeout enforcement to prevent long-running requests
- **Request/Response Logging**: Sanitized logging with sensitive data filtering for observability
- **Input Sanitization**: Automatic XSS protection via `[SanitizedString]` validation attribute
- **Barcode Validation**: Multi-format barcode validation with checksum verification (EAN-13, EAN-8, UPC-A, UPC-E, Code-128)
- **CORS Support**: Configurable cross-origin resource sharing for web clients
- **Response Compression**: Brotli and Gzip for improved performance

---

## Technology Stack

### Framework & Runtime
- **.NET 10.0**
- **ASP.NET Core Web API**

### Database
- **PostgreSQL** - Primary database
- **Entity Framework Core 10.0.0** - ORM
- **Npgsql 10.0.0** - PostgreSQL provider

### Authentication
- **JWT Bearer Tokens** - Access and refresh token system
- **Microsoft.IdentityModel.Tokens 8.15.0**

### Email
- **MailKit 4.14.1** - Email delivery

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
├── Controllers/           HTTP endpoint handlers (thin layer)
│   ├── AuthController.cs
│   ├── UserController.cs
│   ├── FamilyController.cs
│   ├── ProductController.cs
│   ├── LocationController.cs
│   ├── ShoppingListController.cs
│   ├── HealthController.cs
│   ├── VersionController.cs
│   ├── OpenFoodFactsController.cs
│   └── SelectValueController.cs
├── EmailTemplates/        HTML email templates (embedded resources)
├── Entities/              Database entity models
│   ├── Common/           Base entities and shared models
│   │   ├── BaseEntity.cs
│   │   ├── SoftDeleteEntity.cs
│   │   └── RecordChangeEntity.cs
│   ├── Family/           Family-related entities
│   ├── Location/         Location entities (shopping/storage)
│   ├── Product/          Product entities
│   ├── ShoppingList/     Shopping list entities
│   └── User/             User-related entities
├── Enums/                Application enumerations
│   ├── EmailType.cs
│   └── SelectValueType.cs
├── Exceptions/           Custom exception classes
│   ├── AuthException.cs
│   ├── BadRequestException.cs
│   ├── UserNotFoundException.cs
│   ├── FamilyNotFoundException.cs
│   ├── LocationException.cs
│   ├── ProductException.cs
│   ├── ShoppingListException.cs
│   └── RequestTimeoutException.cs
├── Extensions/           Extension methods
│   ├── CurrencyExtensions.cs
│   ├── HttpContextExtensions.cs
│   ├── LanguageExtensions.cs
│   ├── UnitExtensions.cs
│   └── UserTimeZoneExtensions.cs
├── Functions/            Business logic layer (replaces traditional services)
│   ├── UserFunctions.cs
│   ├── FamilyFunctions.cs
│   ├── ProductFunctions.cs
│   ├── LocationFunctions.cs
│   ├── ShoppingListFunctions.cs
│   ├── SelectValueFunctions.cs
│   ├── UnitFunctions.cs
│   └── TimeZoneFunctions.cs
├── Infrastructure/       Infrastructure components (triggers, etc.)
│   └── DatabaseTriggerInitializer.cs
├── Middleware/           Custom middleware
│   ├── CorrelationIdMiddleware.cs
│   ├── GlobalExceptionMiddleware.cs
│   ├── RateLimitingMiddleware.cs
│   ├── RequestLoggingMiddleware.cs
│   ├── RequestTimeoutMiddleware.cs
│   └── SessionInfoMiddleware.cs
├── Migrations/           EF Core database migrations
├── Models/               DTOs and request/response models
│   ├── ApplicationSettings/  Application settings (HTTPS, HSTS, Timeouts)
│   ├── Auth/            Authentication models
│   ├── Background/      Background service models (EmailTask)
│   ├── Common/          Shared models (ApiResponse, SelectValue, VersionInfo)
│   ├── Family/          Family DTOs
│   ├── HealthCheck/     Health check models
│   ├── Location/        Location DTOs
│   ├── OpenFoodFacts/   Open Food Facts API models
│   ├── Product/         Product DTOs
│   ├── RateLimit/       Rate limiting models
│   ├── ShoppingList/    Shopping list DTOs
│   └── User/            User DTOs
├── Security/            Security utilities
│   └── SecureCompare.cs
└── Services/            Application services
    ├── Background/      Background services
    │   ├── EmailBackgroundService.cs
    │   ├── EmailQueueService.cs
    │   ├── IEmailQueueService.cs
    │   └── TokenCleanupService.cs
    ├── HealthChecks/    Health check implementations
    │   ├── EmailServiceHealthCheck.cs
    │   └── OpenFoodFactsHealthCheck.cs
    ├── CacheManagementService.cs
    ├── ConfigService.cs
    ├── EmailService.cs
    ├── JwtService.cs
    ├── OpenFoodFactsService.cs
    ├── RateLimitCleanupService.cs
    └── RateLimitService.cs
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
    try
    {
        var profileResponse = new UserFunctions().GetProfileAsync();
        return Ok(ApiResponse<UserProfileResponse>.SuccessResponse(profileResponse));
    }
    catch (UserNotFoundException ex)
    {
        return NotFound(ApiResponse.ErrorResponse(ex.Message));
    }
    catch (Exception ex)
    {
        Log.Error($"Unexpected error getting user profile: {ex.Message}");
        return StatusCode(500, ApiResponse.ErrorResponse("An error occurred while retrieving user profile"));
    }
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

### Entity Inheritance Hierarchy

All entities follow a clear inheritance hierarchy that provides built-in functionality:

```
BaseEntity (abstract)
  ├── Id: int (primary key, auto-generated)
  └── PublicId: Guid (auto-generated via gen_random_uuid())
      │
      └── SoftDeleteEntity
          └── IsDeleted: bool
              │
              └── RecordChangeEntity
                  └── RecordChange: JSON string (LastModifiedDate, LastModifiedBy)
                      │
                      └── User, Family, Product, ShoppingList, Location, etc.
```

#### BaseEntity

The foundation for all entities:

```csharp
public abstract class BaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid PublicId { get; set; }
}
```

- **Id**: Internal integer primary key
- **PublicId**: Externally-facing GUID identifier (prevents ID enumeration attacks)

#### SoftDeleteEntity

Adds soft delete capability:

```csharp
public class SoftDeleteEntity : BaseEntity
{
    public bool IsDeleted { get; set; } = false;

    public void DeleteRecord()
    {
        IsDeleted = true;
    }
}
```

- Global query filter automatically excludes `IsDeleted = true` records
- Records are never physically deleted, only marked as deleted

#### RecordChangeEntity

Adds automatic change tracking:

```csharp
public class RecordChangeEntity : SoftDeleteEntity
{
    public string RecordChange { get; set; } = JsonSerializer.Serialize(new RecordChange());

    public void UpdateRecordChange(int? modifiedBy = null)
    {
        RecordChange = JsonSerializer.Serialize(new RecordChange
        {
            LastModifiedDate = DateTime.UtcNow,
            LastModifiedBy = modifiedBy ?? -1
        });
    }

    public void DeleteRecord(int? modifiedBy = null)
    {
        IsDeleted = true;
        UpdateRecordChange(modifiedBy);
    }
}
```

- Tracks `LastModifiedDate` and `LastModifiedBy` in JSON format
- Automatically updated via `DbContext.SaveChanges` override

### Database Trigger System

PostgreSQL triggers automatically track changes for cache invalidation:

1. **Trigger Function**: `record_table_change()` PostgreSQL function
2. **Automatic Triggers**: Created for all `RecordChangeEntity` descendants
3. **Change Table**: `TableRecordChanges` table stores change notifications
4. **Initialization**: `DatabaseTriggerInitializer` sets up triggers on startup

**Flow:**
```
1. Entity updated in database
2. PostgreSQL trigger fires
3. Record inserted into TableRecordChanges
4. Cache system reads changes and invalidates affected caches
```

### Session Context Management

The `SessionInfo` static class provides user context throughout the application via `AsyncLocal`:

```csharp
public static class SessionInfo
{
    private static readonly AsyncLocal<Guid?> _publicId = new();
    private static readonly AsyncLocal<int?> _userId = new();
    private static readonly AsyncLocal<int?> _familyId = new();

    public static Guid? GetPublicId() => _publicId.Value;
    public static int? GetUserId() => _userId.Value;
    public static int? GetFamilyId() => _familyId.Value;

    public static void SetUser(Guid? publicId, int? familyId = null) { ... }
    public static void Clear() { ... }
}
```

- **AsyncLocal Storage**: Thread-safe, request-scoped storage
- **Set by Middleware**: `SessionInfoMiddleware` extracts claims from JWT
- **Cleared After Request**: Ensures no data leakage between requests
- **Global Access**: Any part of the application can access current user context

---

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
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] CreateUserRequest request)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ApiResponse.ErrorResponse("Invalid request data"));
    }

    // Process request...
}
```

- Validation attributes on DTOs (e.g., `[Required]`, `[EmailAddress]`)
- Automatic 400 Bad Request if validation fails
- Manual `ModelState` check in endpoints for consistent error responses

### Exception Handling Pattern

All controllers follow a consistent exception handling pattern:

```csharp
try
{
    var result = await new SomeFunctions().DoSomethingAsync();
    return Ok(ApiResponse<ResultType>.SuccessResponse(result));
}
catch (AuthException ex)
{
    return ex.StatusCode switch
    {
        400 => BadRequest(ApiResponse.ErrorResponse(ex.Message)),
        401 => Unauthorized(ApiResponse.ErrorResponse(ex.Message)),
        403 => StatusCode(403, ApiResponse.ErrorResponse(ex.Message)),
        _ => StatusCode(500, ApiResponse.ErrorResponse("An error occurred"))
    };
}
catch (UserNotFoundException ex)
{
    return NotFound(ApiResponse.ErrorResponse(ex.Message));
}
catch (BadRequestException ex)
{
    return BadRequest(ApiResponse.ErrorResponse(ex.Message));
}
catch (Exception ex)
{
    Log.Error($"Unexpected error: {ex.Message}");
    return StatusCode(500, ApiResponse.ErrorResponse("An error occurred"));
}
```

**Custom Exception Hierarchy:**
- `AuthException` - Base authentication exception with `StatusCode` property
- `UserNotFoundException` - User not found (404)
- `FamilyNotFoundException` - Family not found (404)
- `BadRequestException` - Invalid request (400)
- `UnauthorizedException` - Unauthorized access (401)
- `InvalidCredentialsException` - Invalid credentials
- `ExpiredCredentialsException` - Expired credentials
- `ProductNotFoundException` - Product not found (404)
- `ShoppingListNotFoundException` - Shopping list not found (404)
- `ShoppingListItemNotFoundException` - Shopping list item not found (404)
- `ShoppingListAccessDeniedException` - Access denied to shopping list (401)
- `InvalidShoppingListItemException` - Invalid shopping list item (400)
- `ShoppingLocationNotFoundException` - Shopping location not found (404)
- `StorageLocationNotFoundException` - Storage location not found (404)
- `RequestTimeoutException` - Request timeout (504)

**Note:** Most exception mapping is handled by `GlobalExceptionMiddleware`, which catches unhandled exceptions and maps them to appropriate HTTP status codes. `OperationCanceledException` is handled separately (499 for client cancellation).

---

## Authentication & Authorization

### Passwordless Authentication Flow

The system uses email-based verification codes instead of traditional passwords:

1. **Request Verification Code**
   - User submits email address
   - System generates 6-digit code
   - Code sent via email
   - Code expires after configurable minutes

2. **Verify Code & Login**
   - User submits email + verification code
   - System validates code
   - Returns access token + refresh token
   - First login changes status from `PendingVerification` to `Active`

3. **Token Usage**
   - Access token included in `Authorization: Bearer <token>` header
   - Access token expires after short period (configurable)
   - Refresh token used to obtain new access token

**Security Features:**
- **Generic Messages**: Authentication endpoints return generic messages to prevent user enumeration
- **Security Delays**: Random delays (100-400ms) on failures to prevent timing attacks
- **Constant-Time Comparison**: Uses `SecureCompare.ConstantTimeEquals()` for sensitive comparisons

**Example - Request Code:**
```csharp
[HttpPost("request-code")]
public async Task<IActionResult> RequestVerificationCode([FromBody] LoginRequest request)
{
    var genericMessage = "If this email is registered, a verification code will be sent.";

    try
    {
        await new UserFunctions().RequestVerificationCodeAsync(request.Email);
    }
    catch (AuthException ex)
    {
        Log.Warning($"Auth error during request-code: {ex.Message}");
    }

    await Task.Delay(Random.Shared.Next(100, 300));
    return Ok(ApiResponse.SuccessResponse(genericMessage));
}
```

### JWT Token System

#### Access Tokens

Short-lived JWT tokens containing user claims:

```csharp
Claims:
- ClaimTypes.NameIdentifier: User.PublicId (Guid)
- "FamilyId": User.FamilyId (if user belongs to a family)
```

**Configuration:**
- Issuer validation
- Audience validation
- Lifetime validation
- Signing key validation
- **No clock skew** (`ClockSkew = TimeSpan.Zero`)

#### Refresh Tokens

Long-lived random tokens for obtaining new access tokens:

- **Generation**: 64-byte cryptographically random string (Base64 encoded)
- **Storage**: Stored in `UserAuthentication` table
- **Expiry**: Configurable (typically days/weeks)
- **Usage**: Exchange for new access token via `/auth/refresh` endpoint

**Refresh Flow:**
```csharp
[Authorize]
[HttpPost("refresh")]
public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
{
    try
    {
        var refreshResponse = await new UserFunctions().RefreshTokenAsync(request.RefreshToken);
        return Ok(ApiResponse<RefreshTokenResponse>.SuccessResponse(refreshResponse));
    }
    catch (AuthException ex)
    {
        // Error handling...
    }
}
```

#### Refresh Token Rotation

The system implements refresh token rotation for enhanced security against token theft:

**How It Works:**
- When a refresh token is used, it's immediately invalidated
- A new refresh token is issued with each refresh operation
- The previous token is stored temporarily with an expiry date
- If an old token is reused, it indicates potential token theft
- On theft detection: All tokens for that user are invalidated (forced logout)

**Database Fields:**
- `RefreshToken` - Current valid refresh token
- `RefreshTokenExpiry` - Current token expiration
- `PreviousRefreshToken` - Last used token (for grace period detection)
- `PreviousRefreshTokenExpiry` - Grace period expiration

**Benefits:**
- Detects and prevents token replay attacks
- Limits damage if a refresh token is compromised
- Provides automatic cleanup via `TokenCleanupService`
- Follows OAuth 2.0 security best practices

**Cleanup:**
The `TokenCleanupService` background service runs hourly to clean up expired tokens, preventing database bloat.

### Authorization

The system uses attribute-based authorization:

```csharp
[Authorize]  // Entire controller requires authentication
public class UserController : ControllerBase
{
    [HttpGet("profile")]  // Inherits [Authorize]
    public IActionResult GetProfile() { ... }
}

// Or per-endpoint:
public class AuthController : ControllerBase
{
    [HttpPost("request-code")]  // No authentication required
    public IActionResult RequestCode() { ... }

    [Authorize]
    [HttpPost("logout")]  // Requires authentication
    public IActionResult Logout() { ... }
}
```

**Authorization Patterns:**
- **No role-based authorization**: All authenticated users have equal permissions
- **Family-scoped operations**: Validate `SessionInfo.GetFamilyId()` exists
- **User-scoped operations**: Validate `SessionInfo.GetUserId()` exists
- **Resource ownership**: Functions verify user owns the resource being accessed

### User Registration

Registration is **feature-flagged** and can be disabled:

```csharp
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] CreateUserRequest request)
{
    var registrationEnabled = bool.Parse(ConfigService.GetValueOrDefault("RegistrationEnabled", "false"));

    if (!registrationEnabled)
    {
        return StatusCode(403, ApiResponse.ErrorResponse("Registration is currently disabled"));
    }

    // Process registration...
}
```

- Configuration-based feature toggle
- Generic success message (prevents user enumeration)
- Creates user in `PendingVerification` status
- Sends verification code via email

---

## Controllers Reference

### AuthController

Handles authentication and token management (no `[Authorize]` at class level).

**Endpoints:**

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/request-code` | No | Request email verification code |
| POST | `/verify-code` | No | Verify code and login |
| POST | `/refresh` | Yes | Refresh access token |
| POST | `/logout` | Yes | Logout user |
| GET | `/me` | Yes | Get current user info |
| POST | `/register` | No | User registration (config-gated) |

**Key Patterns:**
- Generic messages on sensitive operations
- Security delays on failures
- Exception mapping to HTTP status codes
- Feature flags for registration

### UserController

Manages user profile and settings (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/profile` | Get user profile |
| PUT | `/settings` | Update user settings |
| POST | `/profile-picture` | Upload profile picture (Base64) |
| DELETE | `/profile-picture` | Delete profile picture |

**Key Patterns:**
- All endpoints require authentication
- Consistent error handling
- Base64 image upload for profile pictures
- Automatic user context from `SessionInfo`

### FamilyController

Manages family operations (all endpoints require `[Authorize]`).

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Get family details |
| PUT | `/` | Update family |
| POST | `/create` | Create new family |
| POST | `/join` | Join existing family |
| POST | `/leave` | Leave family |
| POST | `/picture` | Upload family picture (Base64) |
| DELETE | `/picture` | Delete family picture |

**Key Patterns:**
- Family context from `SessionInfo.GetFamilyId()`
- Validation that user belongs to a family
- Family invite code system for joining
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
- Shopping location assignment per item

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
    "Version": "1.1.1225183000-beta",
    "ShortVersion": "1.1",
    "BuildType": "beta",
    "BuildDate": "2025-12-12T18:30:00Z"
  }
}
```

**Key Patterns:**
- Semantic versioning support
- Build date extraction from version string
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

---

## Cross-Cutting Concerns

### Middleware Pipeline

The middleware pipeline is configured in a specific order in `Program.cs`:

```csharp
app.UseResponseCompression();
app.Use(async (context, next) => { /* Response Headers Middleware */ });
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestTimeoutMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseHsts(); // If configured
app.UseCors();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<SessionInfoMiddleware>();
app.MapControllers();
```

**Order matters:**
1. **Response Compression** - Brotli and Gzip compression for responses
2. **Response Headers** - Adds security headers (CSP, X-Frame-Options, HSTS, etc.) and app metadata
3. **Correlation ID** - Generates/propagates X-Correlation-ID for request tracing
4. **Request Timeout** - Enforces per-endpoint timeout limits
5. **Request Logging** - Logs HTTP requests/responses (sanitized)
6. **Global Exception Handler** - Catches and maps all unhandled exceptions
7. **HTTPS Redirection** - Forces HTTPS
8. **HSTS** - HTTP Strict Transport Security (if enabled)
9. **CORS** - Cross-Origin Resource Sharing (configurable origins)
10. **Rate Limiting** - Global and per-endpoint request throttling
11. **Authentication** - JWT token validation
12. **Session Info** - Extracts user context from JWT claims
13. **Controllers** - Route to endpoints

### Response Compression

Automatic response compression for improved performance:

**Supported Algorithms:**
- **Brotli** - Modern compression (higher ratio, slightly slower)
- **Gzip** - Universal compression (broad compatibility)

**Configuration:**
- Compression level: Optimal
- Automatically selects best algorithm based on client Accept-Encoding header
- Reduces bandwidth usage for large JSON responses

### CORS (Cross-Origin Resource Sharing)

Configurable CORS support for web clients:

**Configuration:**
```csharp
AllowedOrigins = ["https://example.com", "http://localhost:3000"]
```

**Features:**
- Configurable allowed origins
- Supports credentials
- Configured via `appsettings.json`
- Enables web browser clients to access the API

### Correlation ID Middleware

Request tracing for distributed systems:

**Features:**
- Generates unique correlation ID for each request (GUID)
- Propagates existing `X-Correlation-ID` header if provided
- Adds correlation ID to response headers
- Integrates with Serilog for structured logging
- Enables end-to-end request tracing

**Usage:**
```
Client Request → X-Correlation-ID: <guid>
Server Response → X-Correlation-ID: <same-guid>
All logs for request tagged with correlation ID
```

### Request Timeout Middleware

Per-endpoint timeout enforcement:

**Features:**
- Default timeout: 30 seconds (configurable)
- Per-endpoint override timeouts using regex patterns
- Cancellation tokens propagated through request pipeline
- Throws `RequestTimeoutException` on timeout
- Logs warnings when timeouts occur

**Configuration Example:**
```json
{
  "RequestTimeout": {
    "DefaultTimeoutSeconds": 30,
    "Endpoints": [
      {
        "PathPattern": "^/api/v1.0/product/import$",
        "TimeoutSeconds": 120
      }
    ]
  }
}
```

**Benefits:**
- Prevents long-running requests from tying up resources
- Different timeouts for different endpoint types
- Graceful handling with appropriate error responses

### Request Logging Middleware

Configurable HTTP request/response logging:

**Features:**
- Logs HTTP method, path, query string, status code, and duration
- Optional detailed logging for specific paths
- **Sanitizes sensitive data:**
  - Query parameters: `password`, `token`, `secret`, `apikey`, `api_key`, `access_token`, `refresh_token`
  - Headers: `Authorization`, `Cookie`, `Set-Cookie`, `X-Api-Key`, `X-Auth-Token`
- Can exclude specific paths from logging
- Log level based on status code (500+ = Error, 400+ = Warning, 2xx = Information)
- Integrates correlation ID with logs

**Configuration:**
```json
{
  "RequestLogging": {
    "Enabled": true,
    "ExcludedPaths": ["/health/live", "/health/ready"],
    "DetailedPaths": ["/api/v1.0/auth/login"]
  }
}
```

**Example Log Output:**
```
[2025-12-18 10:30:00 INF] HTTP GET /api/v1.0/products completed with 200 in 45ms (Correlation: 123e4567-...)
```

### Global Exception Middleware

Centralized exception handling for consistent error responses:

**Features:**
- Catches all unhandled exceptions from the application
- Maps custom exceptions to appropriate HTTP status codes
- Prevents exception details from leaking to clients
- Logs with appropriate severity levels
- Returns consistent `ApiResponse` format

**Exception Mapping:**
- `AuthException` → Custom status code from exception
- `ProductNotFoundException`, `LocationNotFoundException` → 404
- `ProductAccessDeniedException`, `LocationAccessDeniedException` → 403
- `RequestTimeoutException` → 504 Gateway Timeout
- `OperationCanceledException` → 499 Client Closed Request
- Generic exceptions → 500 Internal Server Error

**Benefits:**
- Controllers can be simplified (less try-catch boilerplate)
- Consistent error response format
- Security: No stack traces or sensitive details exposed
- Proper logging with correlation IDs

**Example Controller (Simplified):**
```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetProduct(Guid id, CancellationToken cancellationToken)
{
    // No try-catch needed - GlobalExceptionMiddleware handles it
    var product = await new ProductFunctions().GetProductAsync(id, cancellationToken);
    return Ok(ApiResponse<ProductResponse>.SuccessResponse(product));
}
```

### Rate Limiting

Two-tier rate limiting system via `RateLimitingMiddleware`:

**1. Global Rate Limiting**
- Per IP address across all endpoints
- Default: 100 requests per minute
- Configurable via `GlobalRateLimitRequests` and `GlobalRateLimitWindowMinutes`

**2. Endpoint-Specific Rate Limiting**
- Per IP per endpoint
- Default: 30 requests per minute
- Configurable via `EndpointRateLimitRequests` and `EndpointRateLimitWindowMinutes`

**Response on Limit Exceeded:**
```json
{
  "Success": false,
  "Message": "Rate limit exceeded. Try again in X minutes.",
  "Errors": null,
  "Timestamp": "2025-12-02T10:30:00Z"
}
```
**HTTP Status:** 429 Too Many Requests

**Cleanup:**
- `RateLimitCleanupService` background service periodically cleans expired entries
- Prevents memory leaks from abandoned rate limit buckets

### Security Headers

All responses include comprehensive security headers:

```csharp
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
Strict-Transport-Security: max-age=31536000; includeSubDomains
Content-Security-Policy: default-src 'self'; script-src 'self'; ...
X-Request-ID: <unique-guid>
X-Application-Name: Homassy
X-Application-Version: <version>
```

**Benefits:**
- Prevents clickjacking (X-Frame-Options)
- Prevents MIME sniffing (X-Content-Type-Options)
- Enforces HTTPS (HSTS)
- Content Security Policy protection
- Request tracing (X-Request-ID)

### Input Sanitization & XSS Protection

The application implements a comprehensive input sanitization system to protect against XSS (Cross-Site Scripting) attacks and malicious input.

#### SanitizedString Validation Attribute

A custom validation attribute that automatically validates and sanitizes string inputs at the model binding layer:

```csharp
[SanitizedString]
public string? Name { get; set; }
```

**Features:**
- **Automatic validation** - Applied via attribute to request model properties
- **Dangerous pattern detection** - Rejects input containing XSS attack vectors
- **Case-insensitive matching** - Prevents bypass attempts with mixed-case input
- **Integration with DI** - Uses `IInputSanitizationService` via `ValidationContext`

**Detected Dangerous Patterns:**
- Script tags: `<script`
- JavaScript URIs: `javascript:`
- Event handlers: `onerror=`, `onload=`, `onclick=`, `onmouseover=`, `onfocus=`, `onblur=`
- Code evaluation: `eval(`, `expression(`
- VBScript: `vbscript:`
- Data URIs: `data:text/html`

**Example - Request Model:**
```csharp
public class CreateProductRequest
{
    [Required]
    [StringLength(128, MinimumLength = 2)]
    [SanitizedString]  // XSS protection
    public required string Name { get; set; }

    [StringLength(128)]
    [SanitizedString]  // XSS protection
    public string? Notes { get; set; }
}
```

**Validation Flow:**
1. Client submits request with input data
2. Model binding validates the request
3. `SanitizedStringAttribute` checks for dangerous patterns
4. If patterns detected, returns `400 Bad Request` with error message
5. If clean, request proceeds to controller

**Error Response:**
```json
{
  "Success": false,
  "Errors": ["The field Name contains potentially dangerous content."],
  "Timestamp": "2025-12-19T10:30:00Z"
}
```

#### InputSanitizationService

Provides HTML encoding and whitespace normalization for string inputs:

**Interface:**
```csharp
public interface IInputSanitizationService
{
    string SanitizePlainText(string? input);
    string? SanitizePlainTextOrNull(string? input);
}
```

**Implementation:**
- **HTML Encoding**: Uses `HttpUtility.HtmlEncode()` to encode dangerous characters
- **Whitespace Normalization**: Collapses multiple spaces and trims input
- **Null Handling**: Returns empty string or null depending on method

**Usage Example:**
```csharp
var sanitized = _sanitizationService.SanitizePlainText(userInput);
// "<script>alert('xss')</script>" becomes "&lt;script&gt;alert('xss')&lt;/script&gt;"
```

**Service Registration:**
```csharp
builder.Services.AddSingleton<IInputSanitizationService, InputSanitizationService>();
```

#### Security Benefits

1. **Defense in Depth**
   - Validation layer rejects dangerous patterns
   - Encoding layer neutralizes any bypasses
   - Two-layer protection strategy

2. **Automatic Application**
   - `[SanitizedString]` attribute applied to all user input strings
   - No developer action needed for protection
   - Consistent across entire API

3. **XSS Attack Prevention**
   - Blocks common XSS payloads
   - Prevents stored XSS attacks
   - Protects against reflected XSS

4. **Comprehensive Testing**
   - 30+ unit tests covering various attack vectors
   - Tests for bypass attempts (mixed-case, encoding, etc.)
   - Located in `Homassy.Tests/Unit/InputSanitizationServiceTests.cs` and `SanitizedStringAttributeTests.cs`

**Applied To:**
All user-facing string properties in request models including:
- User names, display names, emails
- Product names, brands, categories, notes
- Family names and descriptions
- Location names, addresses, cities, postal codes
- Shopping list names, descriptions, notes
- Custom item names

#### Adding to New Models

When creating new request models, apply `[SanitizedString]` to all user input string properties:

```csharp
using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

public class CreateResourceRequest
{
    [Required]
    [StringLength(100)]
    [SanitizedString]  // Add this attribute
    public required string Name { get; init; }

    [StringLength(500)]
    [SanitizedString]  // Add this attribute
    public string? Description { get; init; }
}
```

**Important Notes:**
- Only apply to user input strings (not system-generated values)
- Combine with existing validation attributes (`[Required]`, `[StringLength]`, etc.)
- Validation happens automatically during model binding
- No need to manually call sanitization service in controllers

### Barcode Validation

The application implements comprehensive barcode validation to ensure data quality and support international product standards.

#### Supported Barcode Formats

The system validates five major barcode formats with proper checksum algorithms:

| Format | Length | Description | Example |
|--------|--------|-------------|---------|
| **EAN-13** | 13 digits | European Article Number (international standard) | `5449000000996` |
| **EAN-8** | 8 digits | Short European Article Number | `96385074` |
| **UPC-A** | 12 digits | Universal Product Code (North America) | `042100005264` |
| **UPC-E** | 6-8 digits | Compressed UPC format | `01234565` |
| **Code-128** | Variable | High-density alphanumeric barcode | Any length |

#### ValidBarcode Validation Attribute

A custom validation attribute that automatically validates barcodes at the model binding layer:

```csharp
[ValidBarcode]
[StringLength(14, MinimumLength = 6)]
public string? Barcode { get; set; }
```

**Features:**
- **Automatic format detection** - Identifies barcode type based on length and pattern
- **Checksum validation** - Validates check digit using format-specific algorithms
- **Character validation** - Ensures only digits for EAN/UPC formats
- **Whitespace normalization** - Trims leading and trailing spaces
- **Integration with DI** - Uses `IBarcodeValidationService` via `ValidationContext`
- **Detailed error messages** - Specific feedback for different validation failures

**Example - Product Model:**
```csharp
public class CreateProductRequest
{
    [Required]
    [StringLength(128, MinimumLength = 2)]
    [SanitizedString]
    public required string Name { get; set; }

    [ValidBarcode]  // Barcode validation with checksum
    [StringLength(14, MinimumLength = 6)]
    public string? Barcode { get; set; }
}
```

**Validation Flow:**
1. Client submits product with barcode
2. Model binding validates the request
3. `ValidBarcodeAttribute` normalizes and validates barcode
4. Format is auto-detected (EAN-13, EAN-8, UPC-A, UPC-E, Code-128)
5. Checksum is validated using format-specific algorithm
6. If invalid, returns `400 Bad Request` with specific error
7. If valid, request proceeds to controller

**Error Responses:**

Invalid characters:
```json
{
  "Success": false,
  "Errors": ["Barcode 'ABC123' contains invalid characters. Only digits are allowed for EAN/UPC formats."],
  "Timestamp": "2025-12-19T10:30:00Z"
}
```

Invalid length:
```json
{
  "Success": false,
  "Errors": ["Barcode '12345' has invalid length. Supported formats: EAN-13 (13 digits), EAN-8 (8 digits), UPC-A (12 digits), UPC-E (6-8 digits)."],
  "Timestamp": "2025-12-19T10:30:00Z"
}
```

Invalid checksum:
```json
{
  "Success": false,
  "Errors": ["Barcode '4006381333930' has invalid checksum for EAN-13 format."],
  "Timestamp": "2025-12-19T10:30:00Z"
}
```

#### BarcodeValidationService

Provides comprehensive barcode validation with format detection and checksum verification:

**Interface:**
```csharp
public interface IBarcodeValidationService
{
    BarcodeValidationResult Validate(string? barcode);
    BarcodeFormat DetectFormat(string barcode);
    bool ValidateChecksum(string barcode, BarcodeFormat format);
}
```

**Implementation Features:**
- **Format Detection**: Identifies barcode type from length and pattern
- **Checksum Algorithms**: Implements standard algorithms for each format
- **UPC-E Expansion**: Converts compressed UPC-E to UPC-A for validation
- **Normalization**: Trims whitespace before validation
- **Detailed Results**: Returns structured `BarcodeValidationResult`

**Checksum Algorithms:**

**EAN-13 Algorithm:**
```csharp
// Weighted sum: alternating 1x and 3x multipliers
// Check digit = (10 - (sum % 10)) % 10
// Example: 5449000000996
//   5*1 + 4*3 + 4*1 + 9*3 + 0*1 + 0*3 + 0*1 + 0*3 + 0*1 + 0*3 + 9*1 + 9*3 = 74
//   Check digit = (10 - 74 % 10) % 10 = 6 ✓
```

**EAN-8 Algorithm:**
```csharp
// Weighted sum: alternating 3x and 1x multipliers
// Check digit = (10 - (sum % 10)) % 10
// Example: 96385074
//   9*3 + 6*1 + 3*3 + 8*1 + 5*3 + 0*1 + 7*3 = 84
//   Check digit = (10 - 84 % 10) % 10 = 6 (actual: 4) ✗
```

**UPC-A Algorithm:**
```csharp
// Weighted sum: alternating 3x and 1x multipliers
// Check digit = (10 - (sum % 10)) % 10
// Example: 042100005264
//   0*3 + 4*1 + 2*3 + 1*1 + 0*3 + 0*1 + 0*3 + 0*1 + 5*3 + 2*1 + 6*3 = 46
//   Check digit = (10 - 46 % 10) % 10 = 4 ✓
```

**UPC-E Algorithm:**
```csharp
// Expands UPC-E to UPC-A first, then validates UPC-A checksum
// Example: 01234565 → 012000003455 (expanded) → validates checksum
```

**Service Registration:**
```csharp
builder.Services.AddSingleton<IBarcodeValidationService, BarcodeValidationService>();
```

**Usage Example:**
```csharp
var validationService = new BarcodeValidationService();
var result = validationService.Validate("5449000000996");

if (result.IsValid)
{
    Console.WriteLine($"Valid {result.Format} barcode: {result.NormalizedBarcode}");
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

#### BarcodeValidationResult Model

Structured result returned by the validation service:

```csharp
public class BarcodeValidationResult
{
    public bool IsValid { get; init; }
    public BarcodeFormat Format { get; init; }
    public string? ErrorMessage { get; init; }
    public string? NormalizedBarcode { get; init; }

    public static BarcodeValidationResult Success(BarcodeFormat format, string normalizedBarcode);
    public static BarcodeValidationResult Failure(string errorMessage);
}
```

**BarcodeFormat Enum:**
```csharp
public enum BarcodeFormat
{
    Unknown = 0,
    EAN13 = 1,
    EAN8 = 2,
    UPCA = 3,
    UPCE = 4,
    Code128 = 5
}
```

#### Benefits

1. **Data Quality**
   - Prevents invalid barcodes from entering the database
   - Ensures only valid, scannable barcodes are stored
   - Automatic format detection eliminates user errors

2. **International Support**
   - Supports both European (EAN) and North American (UPC) standards
   - Handles compressed formats (UPC-E)
   - Future-proof with Code-128 support

3. **User Experience**
   - Clear, specific error messages
   - Automatic whitespace trimming
   - Validation happens at API boundary (fail fast)

4. **Comprehensive Testing**
   - 70+ unit tests covering all formats
   - Real-world barcode tests (Coca-Cola, etc.)
   - Integration tests for end-to-end validation
   - Tests for checksum edge cases and bypass attempts
   - Located in `Homassy.Tests/Unit/BarcodeValidationServiceTests.cs` and `ValidBarcodeAttributeTests.cs`

**Applied To:**
- Product entity barcode field
- CreateProductRequest barcode property
- UpdateProductRequest barcode property

#### Adding to New Models

When creating new models with barcode fields, apply `[ValidBarcode]` attribute:

```csharp
using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

public class ProductBarcodeRequest
{
    [ValidBarcode]  // Add this attribute
    [StringLength(14, MinimumLength = 6)]
    public string? Barcode { get; init; }
}
```

**Important Notes:**
- Minimum barcode length is 6 digits (UPC-E short format)
- Maximum barcode length is 14 digits (EAN-13 with potential padding)
- Validation is automatic during model binding
- No need to manually call validation service in controllers
- Null/empty barcodes are allowed (use `[Required]` if mandatory)

### Logging with Serilog

Structured logging with console and file sinks:

**Configuration:**
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "Logs/Homassy-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateLogger();
```

**Features:**
- Console output for development
- Daily rolling log files
- 14-day retention
- Reduced verbosity for framework logs
- Structured logging context

**Usage:**
```csharp
Log.Information("User {UserId} logged in", userId);
Log.Warning("Invalid login attempt for {Email}", email);
Log.Error($"Unexpected error: {ex.Message}");
```

### Background Services

Four background services run continuously as hosted services:

**1. CacheManagementService**
- Monitors `TableRecordChanges` for database changes
- Invalidates affected caches when data is modified
- Periodically refreshes caches to maintain consistency
- Ensures cache synchronization with database state

**2. RateLimitCleanupService**
- Periodically cleans up expired rate limit entries
- Prevents memory leaks from abandoned rate limit buckets
- Configurable cleanup interval
- Maintains rate limiting performance

**3. EmailBackgroundService**
- Async email queue processor for non-blocking email delivery
- **Retry logic with exponential backoff:**
  - Maximum 3 retry attempts per email
  - Delays: 1 second, 5 seconds, 15 seconds
- Handles two email types:
  - `EmailType.Verification` - Verification code emails
  - `EmailType.Registration` - Registration confirmation emails
- Gracefully handles cancellation on shutdown
- Detailed logging for troubleshooting delivery issues
- Consumes from `EmailQueueService`

**4. TokenCleanupService**
- Runs hourly to clean expired authentication tokens
- Cleans up expired verification codes from `UserAuthentication` table
- Cleans up expired previous refresh tokens (refresh token rotation)
- Prevents accumulation of stale security data
- Uses EF Core bulk updates for efficiency
- Scoped database access per execution

**Registration:**
All services are registered as `IHostedService` in `Program.cs` and run continuously until application shutdown.

### Application Services

The project includes several application-level services for core functionality:

#### OpenFoodFactsService

Barcode lookup integration with Open Food Facts API:

**Features:**
- Base URL: `https://world.openfoodfacts.org/api/v2`
- `GetProductByBarcodeAsync()` - Look up product by barcode
- `FetchImageAsBase64Async()` - Download and encode product images
- Returns null on API errors (graceful degradation)
- Auto-encodes images as Base64 with media type prefix (`data:image/jpeg;base64,...`)
- Propagates correlation ID to external requests for tracing
- Custom user agent header for API compliance

**Response Model:**
```csharp
record OpenFoodFactsProduct(
    string Code,                // Barcode
    string ProductName,
    string Brands,
    List<string> CategoriesTags,
    string NutritionGrades,
    string NutriscoreGrade,
    string EcoscoreGrade,
    int NovaGroup,
    string IngredientsText,
    List<string> AllergensTags,
    OpenFoodFactsNutriments Nutriments,
    string ImageBase64           // Auto-populated
);
```

**Usage:**
```csharp
var service = new OpenFoodFactsService();
var response = await service.GetProductByBarcodeAsync("3017620422003", cancellationToken);
```

#### RateLimitService

In-memory rate limiting tracking using thread-safe data structures:

**Features:**
- Static service with `ConcurrentDictionary` for thread-safe storage
- Tracks attempts per key with timestamp windows
- Automatic cleanup of expired entries
- Returns detailed status information

**Key Methods:**
```csharp
bool IsRateLimited(string key, int maxAttempts, TimeSpan window)
RateLimitStatus GetRateLimitStatus(string key, int maxAttempts, TimeSpan window)
void ResetAttempts(string key)
TimeSpan? GetLockoutRemaining(string key, TimeSpan window)
void CleanupExpiredEntries(TimeSpan maxAge)
```

**RateLimitStatus Model:**
```csharp
class RateLimitStatus {
    int Limit;                     // Max attempts allowed
    int Remaining;                 // Remaining attempts
    long ResetTimestamp;           // Unix epoch seconds when limit resets
    int? RetryAfterSeconds;        // Seconds to wait if rate limited
}
```

**Usage:**
Used by `RateLimitingMiddleware` to track and enforce rate limits per IP address.

#### EmailQueueService

Email task queue using System.Threading.Channels for efficient async processing:

**Features:**
- Bounded channel with capacity (default: 200 tasks)
- Thread-safe queue operations
- Non-blocking enqueue with timeout (5 seconds)
- Blocking dequeue for background service consumption
- Returns success/failure status on enqueue
- Integrates with correlation ID for tracing

**Methods:**
```csharp
Task<bool> TryQueueEmailAsync(EmailTask task)  // Non-blocking enqueue
ValueTask<EmailTask> DequeueAsync(CancellationToken ct)  // Blocking dequeue
int Count { get; }  // Current queue size
```

**EmailTask Model:**
```csharp
record EmailTask(
    string Email,
    string Code,
    string TimeZone,
    EmailType Type  // Verification or Registration
);
```

**Usage:**
```csharp
var success = await emailQueue.TryQueueEmailAsync(new EmailTask(
    "user@example.com",
    "123456",
    "America/New_York",
    EmailType.Verification
));
```

**Benefits:**
- Non-blocking email delivery (doesn't slow down API responses)
- Retry logic for failed deliveries
- Queue overflow protection
- Monitoring via queue count

### Health Checks

The application implements ASP.NET Core Health Checks for monitoring and orchestration:

#### Registered Health Checks

**1. Database (PostgreSQL)**
- Check type: `AddNpgSql()`
- Tags: `["db", "ready"]`
- Tests database connectivity
- Part of readiness probe for deployment orchestration

**2. EmailServiceHealthCheck**
- Check type: Custom implementation
- Tags: `["external"]`
- Tests SMTP server connectivity
- Attempts to connect and disconnect from configured SMTP server
- Uses StartTls security
- Returns health status based on connection success

**3. OpenFoodFactsHealthCheck**
- Check type: Custom implementation
- Tags: `["external"]`
- Tests external API connectivity
- Hits Open Food Facts API with test barcode
- Configurable timeout (default: 10 seconds)
- Status: Healthy (2xx), Degraded (error response), or Unhealthy (timeout/exception)

#### Health Check Endpoints

Three health endpoints provided by `HealthController`:

**`GET /api/v1.0/health`** - Full health check
- Runs all registered health checks
- Returns comprehensive dependency status
- HTTP 200 if all healthy, 503 if any degraded/unhealthy
- Includes timing information per check

**`GET /api/v1.0/health/ready`** - Readiness probe
- Only runs checks tagged with "ready" (database)
- Used by Kubernetes/orchestrators to determine if pod is ready for traffic
- Lightweight check for deployment decisions

**`GET /api/v1.0/health/live`** - Liveness probe
- Always returns 200 OK
- Confirms application process is running
- No external dependency checks
- Used by Kubernetes/orchestrators to determine if pod should be restarted

#### Response Format

```json
{
  "Status": "Healthy",
  "Duration": "45ms",
  "Dependencies": {
    "npgsql": {
      "Status": "Healthy",
      "Duration": "12ms",
      "Description": null,
      "Data": {}
    },
    "openfoodfacts": {
      "Status": "Healthy",
      "Duration": "150ms"
    },
    "emailservice": {
      "Status": "Healthy",
      "Duration": "80ms"
    }
  }
}
```

**Status Values:**
- `Healthy` - Check passed
- `Degraded` - Check passed with warnings
- `Unhealthy` - Check failed

#### Configuration

```json
{
  "HealthCheck": {
    "OpenFoodFactsTestBarcode": "3017620422003",
    "TimeoutSeconds": 10
  }
}
```

**Benefits:**
- Kubernetes-compatible probes for container orchestration
- Monitoring system integration (Prometheus, Datadog, etc.)
- Automated health status tracking
- Dependency monitoring (database, SMTP, external APIs)
- Early detection of infrastructure issues

---

## Development Guidelines

### Adding a New Controller

1. **Create Controller Class**
   ```csharp
   [ApiVersion(1.0)]
   [Route("api/v{version:apiVersion}/[controller]")]
   [ApiController]
   [Authorize]  // If all endpoints require auth
   public class MyController : ControllerBase
   {
   }
   ```

2. **Add Endpoints**
   ```csharp
   [HttpGet("resource")]
   [MapToApiVersion(1.0)]
   public IActionResult GetResource()
   {
       try
       {
           var result = new MyFunctions().GetResourceAsync();
           return Ok(ApiResponse<ResourceResponse>.SuccessResponse(result));
       }
       catch (NotFoundException ex)
       {
           return NotFound(ApiResponse.ErrorResponse(ex.Message));
       }
       catch (Exception ex)
       {
           Log.Error($"Unexpected error: {ex.Message}");
           return StatusCode(500, ApiResponse.ErrorResponse("An error occurred"));
       }
   }
   ```

3. **Follow Patterns**
   - Validate `ModelState` on requests with body
   - Use `ApiResponse<T>` for all responses
   - Map exceptions to appropriate HTTP status codes
   - Log unexpected errors
   - Use `[Authorize]` attribute for protected endpoints

### Creating a Functions Class

1. **Create Functions Class**
   ```csharp
   public class MyFunctions
   {
       private readonly HomassyDbContext _context = new();

       public MyResponse GetResourceAsync()
       {
           var userId = SessionInfo.GetUserId();

           // Cache-first pattern
           var cachedData = GetFromCache(userId);
           if (cachedData != null)
               return cachedData;

           // Database fallback
           var data = _context.MyEntities
               .Where(e => e.UserId == userId)
               .FirstOrDefault();

           if (data == null)
               throw new NotFoundException("Resource not found");

           // Cache result
           AddToCache(userId, data);

           return MapToResponse(data);
       }
   }
   ```

2. **Follow Patterns**
   - Use `SessionInfo` for user context
   - Implement cache-first pattern where appropriate
   - Use `ConcurrentDictionary` for thread-safe caching
   - Throw domain exceptions (not generic exceptions)
   - Keep transactions minimal and focused

### Cache Management

**Implementing Cache:**
```csharp
private static readonly ConcurrentDictionary<int, MyEntity> _cache = new();

public MyEntity GetById(int id)
{
    // Try cache first
    if (_cache.TryGetValue(id, out var cached))
        return cached;

    // Database fallback
    var entity = _context.MyEntities.Find(id);

    if (entity != null)
        _cache[id] = entity;

    return entity;
}

public void InvalidateCache(int id)
{
    _cache.TryRemove(id, out _);
}
```

**Cache Invalidation:**
- Automatic via database triggers and `CacheManagementService`
- Manual via `InvalidateCache()` methods in Functions
- Cache refresh on background service intervals

### Exception Handling

**Define Custom Exceptions:**
```csharp
public class MyResourceNotFoundException : Exception
{
    public MyResourceNotFoundException(string message) : base(message) { }
}
```

**Use in Functions:**
```csharp
if (resource == null)
    throw new MyResourceNotFoundException("Resource not found");
```

**Map in Controllers:**
```csharp
catch (MyResourceNotFoundException ex)
{
    return NotFound(ApiResponse.ErrorResponse(ex.Message));
}
```

### Creating DTOs

**Request Models:**
```csharp
using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

public class CreateResourceRequest
{
    [Required]
    [StringLength(100)]
    [SanitizedString]  // XSS protection
    public string Name { get; set; }

    [ValidBarcode]  // Barcode validation with checksum
    [StringLength(14, MinimumLength = 6)]
    public string? Barcode { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; }
}
```

**Response Models:**
```csharp
public class ResourceResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Adding Database Entities

1. **Create Entity Class**
   ```csharp
   public class MyEntity : RecordChangeEntity
   {
       public string Name { get; set; }
       public int UserId { get; set; }
       public User User { get; set; }
   }
   ```

2. **Add to DbContext**
   ```csharp
   public DbSet<MyEntity> MyEntities { get; set; }
   ```

3. **Configure in OnModelCreating** (if needed)
   ```csharp
   modelBuilder.Entity<MyEntity>(entity =>
   {
       entity.HasIndex(e => e.Name);
       entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
   });
   ```

4. **Create Migration**
   ```bash
   dotnet ef migrations add AddMyEntity
   dotnet ef database update
   ```

5. **Triggers Created Automatically**
   - `DatabaseTriggerInitializer` will create triggers for `RecordChangeEntity` descendants
   - No manual trigger creation needed

### Using CancellationTokens

All async controller endpoints now support `CancellationToken` for proper async operation handling:

**Pattern:**
```csharp
[HttpGet("resource")]
[MapToApiVersion(1.0)]
public async Task<IActionResult> GetResource(CancellationToken cancellationToken)
{
    try
    {
        var result = await new MyFunctions().GetResourceAsync(cancellationToken);
        return Ok(ApiResponse<ResourceResponse>.SuccessResponse(result));
    }
    catch (OperationCanceledException)
    {
        // Client closed connection - log and return 499
        Log.Warning("Request cancelled by client");
        return StatusCode(499, ApiResponse.ErrorResponse("Request cancelled"));
    }
    catch (Exception ex)
    {
        Log.Error($"Unexpected error: {ex.Message}");
        return StatusCode(500, ApiResponse.ErrorResponse("An error occurred"));
    }
}
```

**Propagate to Functions:**
```csharp
public class MyFunctions
{
    public async Task<MyResponse> GetResourceAsync(CancellationToken cancellationToken)
    {
        // Pass cancellationToken to async operations
        var data = await _context.MyEntities
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);

        // Check cancellation between operations
        cancellationToken.ThrowIfCancellationRequested();

        // Pass to external service calls
        var externalData = await externalService.FetchAsync(cancellationToken);

        return MapToResponse(data);
    }
}
```

**Benefits:**
- **Proper request cancellation** when client disconnects
- **Resource cleanup** on timeout via `RequestTimeoutMiddleware`
- **Better async operation handling** throughout the stack
- **Prevents wasted work** if client is no longer waiting
- **Timeout enforcement** at the middleware level

**Important Notes:**
- Always accept `CancellationToken` in async controller methods
- Propagate the token to all async operations (EF Core queries, HTTP calls, etc.)
- `GlobalExceptionMiddleware` handles `OperationCanceledException` automatically (499 status)
- `RequestTimeoutMiddleware` creates a timeout-bound cancellation token per request
- Don't catch `OperationCanceledException` unless you have a specific reason

---

## Summary

Homassy.API is a modern ASP.NET Core Web API with a unique architecture optimized for performance, observability, and developer productivity. Key takeaways:

- **Controller → Functions** pattern simplifies architecture
- **In-memory caching** with database triggers provides excellent performance
- **Passwordless authentication** improves security and user experience
- **Entity inheritance** provides soft delete and change tracking automatically
- **Standardized responses** ensure API consistency
- **Comprehensive middleware** provides rate limiting, security headers, request tracing, and session management
- **Background email queue** with retry logic improves reliability and non-blocking delivery
- **Correlation ID tracking** enables distributed tracing across the application
- **Health checks** provide monitoring and Kubernetes-compatible orchestration support
- **Refresh token rotation** enhances security against token theft and replay attacks
- **Centralized exception handling** simplifies controller code and ensures consistent error responses
- **Request/response logging** with sensitive data filtering improves observability
- **Per-endpoint timeouts** prevent long-running requests from consuming resources
- **Input sanitization** with automatic XSS protection via validation attributes
- **Barcode validation** with multi-format support and checksum verification (EAN-13, EAN-8, UPC-A, UPC-E, Code-128)
- **Open Food Facts integration** enriches product data with barcode lookup and nutrition information
- **CancellationToken support** throughout for proper async operation handling and timeouts
- **Response compression** (Brotli/Gzip) improves performance for large payloads
- **CORS support** enables web client integration

This architecture prioritizes:
- **Performance**: Aggressive caching, response compression, efficient async operations
- **Security**: JWT authentication with token rotation, rate limiting, security headers, input sanitization, constant-time comparisons, sanitized logging
- **Data Quality**: Barcode validation with checksum verification, input sanitization, comprehensive validation attributes
- **Observability**: Correlation IDs, request logging, health checks, structured logging with Serilog
- **Resilience**: Retry logic, timeout enforcement, graceful degradation, health monitoring
- **Maintainability**: Clear separation of concerns, consistent patterns, centralized error handling
- **Developer Experience**: Simple patterns, minimal boilerplate, easy to extend, comprehensive documentation
- **DevOps Readiness**: Kubernetes-compatible health probes, version endpoint, configurable timeouts, container-friendly design
