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
│   ├── TestPushEndpoint.cs             # POST /push/test
│   ├── LowStockPushEndpoint.cs         # POST /push/low-stock
│   └── TestEmailEndpoint.cs            # POST /email/test
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
│   ├── FamilyPushNotifier.cs           # Shared recipient resolution + push dispatch
│   ├── InventoryExpirationService.cs   # Expiring/expired item queries
│   ├── EmailServiceClient.cs           # HTTP client → Homassy.Email
│   └── InventoryBroadcastServiceClient.cs  # HTTP client → Homassy.API internal broadcast (realtime relay)
└── Workers/
    ├── PushNotificationSchedulerService.cs   # Hourly, Mon 07:00 → weekly push
    ├── ShoppingListActivityMonitorService.cs  # 5 min → shopping list push
    ├── InventoryActivityMonitorService.cs     # 5 min → inventory (készlet) push
    ├── FamilyJoinRequestMonitorService.cs     # 1 min → family join request push
    ├── ItemAutomationWorkerService.cs         # 5 min → item automation execution
    └── EmailWeeklySummaryService.cs           # Hourly, Mon 07:00 → weekly email
```

---

## Architecture

### Request Flow

```
Homassy.API  →  POST /push/test  →  Homassy.Notifications  →  WebPush (browser)
                  (proxied)

Homassy.Notifications  →  POST /email/weekly-summary         →  Homassy.Email  →  SMTP
Homassy.Notifications  →  POST /email/automation-notification →  Homassy.Email  →  SMTP
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
| POST | `/push/low-stock` | X-Api-Key | Send a low-stock push (and email) to a user |
| POST | `/email/test` | X-Api-Key | Send a test weekly summary email to a user |
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
- Sends immediate notifications when a family shopping list is created or deleted
- Tracks in-memory shopping list sessions (keyed by ShoppingListId); when item activity
  (add/edit/delete/purchase) goes idle for 5 minutes, sends one notification per non-zero action
  type to family members who did not contribute
- Uses the shared `FamilyPushNotifier` for recipient resolution and dispatch

### InventoryActivityMonitorService
- Runs every 5 minutes
- Tracks in-memory inventory sessions keyed by FamilyId; when inventory activity
  (create/update/delete/consume) goes idle for 5 minutes, sends one notification per non-zero action
  type (count-only, no product names) to family members who did not contribute
- Uses the shared `FamilyPushNotifier`

### FamilyJoinRequestMonitorService
- Runs every minute (join requests are time-sensitive)
- Polls the `Activities` table since the previous run for `FamilyJoinRequestCreate`/`Approve`/`Decline` events
- New request → notifies every existing family member (excluding the requester)
- Approve/Decline → notifies the requester (resolves the join request to user + family)
- Uses the shared `FamilyPushNotifier`

### ItemAutomationWorkerService
- Runs every 5 minutes
- Processes due item automation rules (`NextExecutionAt <= now`):
  `AutoConsume` (decrements inventory, logs consumption), `NotifyOnly` (reminder),
  and `AddToShoppingList` (adds an item to the target list), then recalculates `NextExecutionAt`
  in the user's timezone
- Also evaluates `LowStockAddToShoppingList` rules: when total non-consumed stock for a product
  drops below the threshold it adds the product to the shopping list and marks the rule triggered;
  triggered rules are re-armed once stock is replenished above the threshold
- Records an `ItemAutomationExecution` for each rule and sends push + email notifications to the
  owner via `IWebPushService` and `EmailServiceClient`
- After an `AutoConsume` commit it relays the inventory change to `Homassy.API` via
  `InventoryBroadcastServiceClient` (→ `POST /api/v1/internal/inventory/broadcast`) so connected
  Készletek grids update live; this process hosts no SignalR hub, so it cannot broadcast directly

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

All internal service-to-service auth uses ONE global key: config key `InternalApi:ApiKey`,
fed from the global `.env` `INTERNAL_API_KEY` via compose. Every service (API, Notifications,
Email) validates and sends the same value.

### API Key Middleware (`ApiKeyMiddleware`)
- Validates `X-Api-Key` header against `InternalApi:ApiKey` config value
- Uses constant-time comparison (`CryptographicOperations.FixedTimeEquals`) to prevent timing attacks
- `/health/*` paths are exempt

### Outbound clients (`EmailServiceClient`, `InventoryBroadcastServiceClient`)
- Both send `X-Api-Key` = `InternalApi:ApiKey` (the global key) to Email and to the API's internal
  broadcast endpoint respectively; base URLs come from `EmailService:BaseUrl` / `HomassyApi:BaseUrl`
- Failures are logged and swallowed — a realtime relay must never break the worker

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
  "InternalApi": {
    "ApiKey": ""
  },
  "EmailService": {
    "BaseUrl": "http://homassy-email:8080"
  },
  "HomassyApi": {
    "BaseUrl": "http://homassy-api:8080"
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
