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

