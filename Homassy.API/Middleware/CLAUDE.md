# Middleware — Homassy.API

> Per-middleware detail split out of [../CLAUDE.md](../CLAUDE.md). The pipeline **order** lives in the main file; this doc covers each middleware in depth.

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

**Matching rules** (`Homassy.API/Security/CorsOriginPolicy.cs`, the single `HomassyPolicy`
policy that the SignalR hubs also `RequireCors`):

- Outside Development the allowlist is the **only** way to grant an origin. The loopback
  shortcut — any scheme, any port on a loopback address, so dev servers need not be
  enumerated — is decided once at registration from `IWebHostEnvironment`, not inside the
  per-request predicate. It was previously ungated, which let any page on any loopback port
  make credentialed calls to production and read the responses.
- Scheme and host compare case-insensitively, the port compares **exactly**, and the path is
  ignored, so `https://app.example.com` does not match `https://app.example.com.evil.tld`.
- The `Origin` header is caller-controlled and need not be a URL. Every value goes through
  `Uri.TryCreate` plus an http/https scheme check: a malformed origin is a plain denial. It
  must never throw — CORS evaluation runs before `GlobalExceptionMiddleware`, so an exception
  there surfaces as an unhandled 500.
- Starting in a non-Development environment with an empty `Cors:AllowedOrigins` logs a
  warning: that is a broken deployment (nothing cross-origin works), not a stricter one.

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
    var product = await _productFunctions.GetProductAsync(id, cancellationToken);
    return Ok(ApiResponse<ProductResponse>.SuccessResponse(product));
}
```

### Forwarded Headers

`UseForwardedHeaders` runs as the **first** middleware, before response compression and rate
limiting, so that `Connection.RemoteIpAddress` and `Request.Scheme` are correct everywhere
downstream.

The trust boundary is configuration, not convention — `ForwardedHeaders` in `appsettings*.json`:

| Key | Meaning |
|-----|---------|
| `Enabled` | Off in development (no proxy in front of the API), on in production |
| `KnownNetworks` | CIDR ranges a forwarded chain is accepted from — the Docker bridge Caddy runs on (`172.16.0.0/12`), wired from `FORWARDED_HEADERS_KNOWN_NETWORK` |
| `KnownProxies` | Individual proxy addresses, for hosts no network entry covers |
| `ForwardLimit` | Upper bound on hops consumed from the chain; unwinding stops at the first address that is not a known proxy |

The framework defaults (loopback only) are cleared at startup, so an unconfigured deployment
trusts nothing rather than trusting everything.

`HttpContextExtensions.GetClientIpAddress()` reads **only** `Connection.RemoteIpAddress`. It
must never fall back to parsing `X-Forwarded-For` or `X-Real-IP`: those are attacker-controlled,
and trusting them lets a caller mint a fresh rate-limit bucket per request and choose the IP
written to the security logs.

### Rate Limiting

Two-tier rate limiting system via `RateLimitingMiddleware`:

**1. Global Rate Limiting**
- Per IP address across all endpoints
- Default: 100 requests per minute
- Configurable via `GlobalRateLimitRequests` and `GlobalRateLimitWindowMinutes`

**2. Endpoint-Specific Rate Limiting**
- Per IP per **route template** — not per request path
- Default: 30 requests per minute
- Configurable via `EndpointRateLimitRequests` and `EndpointRateLimitWindowMinutes`

**Key shape:** `global:{ip}` and `endpoint:{routeTemplate}:{ip}`. The route template comes from
the matched endpoint (hence the explicit `app.UseRouting()` before the middleware); everything
that matched no route shares the single `unmatched` bucket. `RateLimitService` keeps its buckets
in a process-wide dictionary, so keying on the raw path would let a caller grow it without bound
by walking made-up URLs.

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

