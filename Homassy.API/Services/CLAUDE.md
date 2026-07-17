# Services — Homassy.API

> Split out of [../CLAUDE.md](../CLAUDE.md). Application services, background/hosted services, health checks, and Serilog logging.

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

Several hosted services run as `IHostedService` / `BackgroundService`:

**1. CacheManagementService**
- Monitors `TableRecordChanges` for database changes
- Invalidates affected caches when data is modified
- Periodically refreshes caches to maintain consistency
- Ensures cache synchronization with database state

**2. RateLimitCleanupService**
- Periodically cleans up expired rate limit entries
- Prevents memory leaks from abandoned rate limit buckets
- Maintains rate limiting performance

**3. GracefulShutdownService**
- Listens for `ApplicationStopping` lifetime event
- Waits a configurable drain period (`GracefulShutdown:TimeoutSeconds`) before process exits
- Allows in-flight requests to complete during rolling restarts
- Configured via `appsettings.json`: `"GracefulShutdown": { "Enabled": true, "TimeoutSeconds": 10 }`

**4. StatisticsRefreshWorker** _(in `Services/Background/`)_
- Refreshes the global-statistics cache (`StatisticsService`) once on startup, then nightly at 02:00 UTC
- Computes platform-wide totals via a scoped `HomassyDbContext`

**5. TokenCleanupService** _(in `Services/Background/`)_
- Runs periodically to clean up stale data
- Scoped database access per execution

**Registration:**
```csharp
builder.Services.AddHostedService<CacheManagementService>();
builder.Services.AddHostedService<RateLimitCleanupService>();
builder.Services.AddSingleton<StatisticsService>();
builder.Services.AddHostedService<StatisticsRefreshWorker>();
builder.Services.AddHostedService<GracefulShutdownService>();
// TokenCleanupService also registered as a hosted background service
```

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

### Health Checks

The application implements ASP.NET Core Health Checks for monitoring and orchestration:

#### Registered Health Checks

**1. Database (PostgreSQL)**
- Check type: `AddNpgSql()`
- Tags: `["db", "ready"]`
- Tests database connectivity
- Part of readiness probe for deployment orchestration

**2. OpenFoodFactsHealthCheck**
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
- Dependency monitoring (database, external APIs)
- Early detection of infrastructure issues

---

