# Homassy.Notifications - Service Architecture Documentation

> **Note:** This is a living document that is updated as the project evolves.
> It is not versioned - changes are made directly to reflect the current state of the project.

## Table of Contents

1. [Overview](#overview)
2. [Technology Stack](#technology-stack)
3. [Project Structure](#project-structure)
4. [Architecture](#architecture)
5. [Endpoints](#endpoints)
6. [Background Workers](#background-workers)
7. [Health Checks](#health-checks)
8. [Security](#security)
9. [Configuration](#configuration)
10. [Development Guidelines](#development-guidelines)

---

## Overview

Homassy.Notifications is a standalone microservice responsible for all notification delivery — both push notifications and email summaries. It is called internally only (never directly from the frontend).

### Key Architectural Decisions

- **Minimal ASP.NET Core** – No controllers. Uses Minimal API endpoints (`app.MapPost(...)`) for simplicity
- **ProjectReference to Homassy.API** – Shares entities and `HomassyDbContext` directly (same pattern as `Homassy.Migrator`)
- **Push notifications moved here** – All WebPush logic migrated from `Homassy.API`; the API proxies `/push/test` to this service
- **Weekly email summaries** – Sends inventory expiration summaries via `Homassy.Email` every Monday at 07:00 local time

---

## Technology Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10 (Minimal API) |
| ORM | Entity Framework Core 10 (via Homassy.API project ref) |
| Database | PostgreSQL (Npgsql) |
| Push | WebPush (VAPID) |
| Logging | Serilog (Console sink) |
| Health Checks | Microsoft.Extensions.Diagnostics.HealthChecks |

---

## Project Structure

```
Homassy.Notifications/
├── CLAUDE.md                           # This file
├── Dockerfile                          # Multi-stage Linux container
├── Homassy.Notifications.csproj        # .NET 10 Web SDK, references Homassy.API
├── Program.cs                          # Minimal API bootstrap
├── appsettings.json                    # Base configuration
├── appsettings.Development.json        # Development overrides
├── Endpoints/
│   └── TestPushEndpoint.cs             # POST /push/test
├── HealthChecks/
│   ├── DatabaseHealthCheck.cs          # DB connectivity check
│   └── WebPushHealthCheck.cs           # VAPID config presence check
├── Middleware/
│   └── ApiKeyMiddleware.cs             # X-Api-Key header validation
├── Models/
│   └── TestPushRequest.cs              # { UserId } request record
├── Services/
│   ├── IWebPushService.cs              # Push notification interface
│   ├── WebPushService.cs               # VAPID push implementation
│   ├── PushNotificationContentService.cs  # Localised notification text
│   ├── InventoryExpirationService.cs   # Expiring/expired item queries
│   └── EmailServiceClient.cs           # HTTP client → Homassy.Email
└── Workers/
    ├── PushNotificationSchedulerService.cs   # Hourly, Mon 07:00 → weekly push
    ├── ShoppingListActivityMonitorService.cs  # 5 min → shopping list push
    └── EmailWeeklySummaryService.cs           # Hourly, Mon 07:00 → weekly email
```

---

## Architecture

### Request Flow

```
Homassy.API  →  POST /push/test  →  Homassy.Notifications  →  WebPush (browser)
                  (proxied)

Homassy.Notifications  →  POST /email/weekly-summary  →  Homassy.Email  →  SMTP
```

### DB Access

The service takes a `ProjectReference` to `Homassy.API.csproj` and reuses `HomassyDbContext` and all entities directly. This avoids duplication and ensures schema parity.

Setup in `Program.cs`:
```csharp
HomassyDbContext.SetConfiguration(builder.Configuration);
builder.Services.AddDbContext<HomassyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

---

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/push/test` | X-Api-Key | Send a test push to a user |
| GET | `/health/live` | None | Liveness probe (always 200) |
| GET | `/health/ready` | None | Readiness probe (DB + WebPush) |

---

## Background Workers

### PushNotificationSchedulerService
- Runs every hour
- On Mondays at 07:00 **local time**, sends weekly push summaries to eligible users
- Uses `InventoryExpirationService` to get expiring product counts

### ShoppingListActivityMonitorService
- Runs every 5 minutes
- Tracks in-memory shopping list sessions
- Sends push notification when a new item is added while another device is active

### EmailWeeklySummaryService
- Runs every hour
- On Mondays at 07:00 **local time**, sends weekly email summaries via `Homassy.Email`
- Deduplication via `UserNotificationPreferences.LastWeeklyEmailSentAt`

---

## Health Checks

| Check | Tag | Description |
|-------|-----|-------------|
| `DatabaseHealthCheck` | `ready` | Verifies DB connectivity via `HomassyDbContext.Database.CanConnectAsync()` |
| `WebPushHealthCheck` | `ready` | Verifies `WebPush:VapidSubject`, `VapidPublicKey`, `VapidPrivateKey` are set |

Endpoints:
- `/health/live` — always returns 200 (no checks)
- `/health/ready` — runs all `ready`-tagged checks

---

## Security

### API Key Middleware (`ApiKeyMiddleware`)
- Validates `X-Api-Key` header against `Notifications:ApiKey` config value
- Uses constant-time comparison (`CryptographicOperations.FixedTimeEquals`) to prevent timing attacks
- `/health/*` paths are exempt

---

## Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=...;Username=...;Password=..."
  },
  "WebPush": {
    "VapidSubject": "mailto:admin@example.com",
    "VapidPublicKey": "",
    "VapidPrivateKey": ""
  },
  "Notifications": {
    "ApiKey": ""
  },
  "EmailService": {
    "BaseUrl": "http://homassy-email:8080",
    "ApiKey": ""
  }
}
```

---

## Development Guidelines

- **No controllers** — use Minimal API `app.MapPost(...)` pattern
- **Namespace**: `Homassy.Notifications.*`
- **DB access**: only via scoped `HomassyDbContext` — never use `new HomassyDbContext(...)` directly in services
- **Logging**: always use `ILogger<T>` injection, never `Console.Write`
- **API Key**: the Notifications service is internal-only — enforce auth on all non-health endpoints
- **Background workers**: extend `BackgroundService`, use `IServiceScopeFactory` for scoped dependencies inside workers
