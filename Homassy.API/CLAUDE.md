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
│   └── ProductController.cs (in development)
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
├── Exceptions/           Custom exception classes
│   ├── AuthException.cs
│   ├── BadRequestException.cs
│   ├── UserNotFoundException.cs
│   └── FamilyNotFoundException.cs
├── Extensions/           Extension methods
├── Functions/            Business logic layer (replaces traditional services)
│   ├── UserFunctions.cs
│   ├── FamilyFunctions.cs
│   └── ProductFunctions.cs
├── Infrastructure/       Infrastructure components (triggers, etc.)
│   └── DatabaseTriggerInitializer.cs
├── Middleware/           Custom middleware
│   ├── RateLimitingMiddleware.cs
│   └── SessionInfoMiddleware.cs
├── Migrations/           EF Core database migrations
├── Models/               DTOs and request/response models
│   ├── Auth/            Authentication models
│   ├── Common/          Shared models (ApiResponse, etc.)
│   ├── Family/          Family DTOs
│   ├── Product/         Product DTOs
│   ├── RateLimit/       Rate limiting models
│   └── User/            User DTOs
├── Security/            Security utilities
│   └── SecureCompare.cs
└── Services/            Application services
    ├── CacheManagementService.cs
    ├── ConfigService.cs
    ├── EmailService.cs
    ├── JwtService.cs
    └── RateLimitCleanupService.cs
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

    public void DeleteRekord()
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

    public void DeleteRekord(int? modifiedBy = null)
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

**Status:** Currently under development - patterns may evolve.

---

## Cross-Cutting Concerns

### Middleware Pipeline

The middleware pipeline is configured in a specific order in `Program.cs`:

```csharp
app.Use(async (context, next) => { /* Response Headers Middleware */ });
app.UseHttpsRedirection();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseAuthorization();
app.UseMiddleware<SessionInfoMiddleware>();
app.MapControllers();
```

**Order matters:**
1. **Response Headers** - Adds security headers (CSP, X-Frame-Options, HSTS, etc.)
2. **HTTPS Redirection** - Forces HTTPS
3. **Rate Limiting** - Throttles requests
4. **Authorization** - JWT validation
5. **Session Info** - Extracts user context from JWT claims
6. **Controllers** - Route to endpoints

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

Two background services run continuously:

**1. CacheManagementService**
- Monitors `TableRecordChanges` for database changes
- Invalidates affected caches
- Periodically refreshes caches
- Ensures cache consistency

**2. RateLimitCleanupService**
- Periodically cleans up expired rate limit entries
- Prevents memory leaks
- Configurable cleanup interval

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
public class CreateResourceRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; }

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

---

## Summary

Homassy.API is a modern ASP.NET Core Web API with a unique architecture optimized for performance and developer productivity. Key takeaways:

- **Controller → Functions** pattern simplifies architecture
- **In-memory caching** with database triggers provides excellent performance
- **Passwordless authentication** improves security and user experience
- **Entity inheritance** provides soft delete and change tracking automatically
- **Standardized responses** ensure API consistency
- **Comprehensive middleware** provides rate limiting, security headers, and session management

This architecture prioritizes:
- **Performance**: Aggressive caching reduces database load
- **Security**: JWT authentication, rate limiting, security headers, constant-time comparisons
- **Maintainability**: Clear separation of concerns, consistent patterns
- **Developer Experience**: Simple patterns, minimal boilerplate, easy to extend
