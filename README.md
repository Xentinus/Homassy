<div align="center">
  <img src=".github/HomassyLogo.svg" alt="Homassy Logo" width="200"/>
  <h1>Homassy</h1>

  <p>Home storage management system for families: household inventory, shopping lists, product tracking, and automated inventory workflows.</p>

  <p>
    <img src="https://img.shields.io/badge/license-AGPL--3.0-blue.svg" alt="License: AGPL-3.0"/>
    <img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET 10"/>
    <img src="https://img.shields.io/badge/Nuxt-4.2-00DC82" alt="Nuxt 4"/>
    <img src="https://img.shields.io/badge/PostgreSQL-16-4169E1" alt="PostgreSQL 16"/>
  </p>
</div>

## Contents

- [Overview](#overview)
- [Getting started](#getting-started)
- [Key features](#key-features)
- [Project structure](#project-structure)
- [Technology stack](#technology-stack)
- [Entity inheritance hierarchy](#entity-inheritance-hierarchy)
- [Deployment](#deployment)
- [License](#license)

## Overview

Homassy manages household inventory, shopping lists, product tracking, and automated inventory workflows for families.

The system is full-stack and split into a few services: an ASP.NET Core API for the business logic, a dedicated email microservice, a dedicated push-notifications microservice, and a Vue 3 (Nuxt 4) web frontend. In production a Caddy reverse proxy puts all of them behind a single domain (see [Deployment](#deployment)). Authentication is handled by a self-hosted Ory Kratos instance using passwordless email codes.

## Getting started

### Prerequisites

- Docker and Docker Compose to run the whole stack, or, to run services individually: the .NET 10 SDK, Node.js 22, and PostgreSQL 16.

### Quick start (Docker)

```bash
git clone https://github.com/Xentinus/Homassy.git
cd Homassy
cp .env.example .env   # fill in the secrets (DB password, Kratos secrets, SMTP, VAPID keys)
docker compose up -d
```

This starts PostgreSQL, the migrator, Kratos, the API, the email and notifications services, and the web app. `docker-compose.override.yml` is merged automatically and points the frontend at the local service ports. Once the containers are healthy:

- Web app: http://localhost:3000
- API: http://localhost:5226
- Kratos (public): http://localhost:4433

In development each service is reachable on its own localhost port. The single-domain Caddy proxy is production-only.

### Local development (individual services)

```bash
# Frontend (hot reload on :3000)
cd Homassy.Web && npm ci && npm run dev

# API (:5226)
cd Homassy.API && dotnet run

# Email / Notifications microservices
cd Homassy.Email && dotnet run
cd Homassy.Notifications && dotnet run

# Tests
cd Homassy.Tests && dotnet test
```

Database migrations run through the migrator CLI: `cd Homassy.Migrator && dotnet run -- migrate` (other commands: `migrate-to-kratos`, `verify-kratos`, `kratos-stats`, all supporting `--dry-run`).

### Configuration

All configuration is through environment variables in `.env`. `.env.example` documents the full set with comments: database connection, Kratos secrets and URLs, SMTP credentials, VAPID push keys, and the frontend `NUXT_PUBLIC_*` URLs. `.env` is gitignored; never commit real secrets.

## Key features

### Backend

- .NET 10 Web API, PostgreSQL with Entity Framework Core.
- Ory Kratos for self-hosted identity, with passwordless email-code login (no JWT on the frontend).
- `Homassy.Email`: email microservice with a bounded queue and retry-based delivery.
- `Homassy.Notifications`: push-notifications microservice (WebPush/VAPID) with background workers for expirations, activity, automation, and email summaries.
- In-memory caching invalidated by PostgreSQL triggers rather than manual cache busting.
- Controller then Functions layering, with no separate repository layer.
- Cross-cutting middleware: exception handling, CORS, Brotli/Gzip compression, request logging, and per-request `X-Correlation-ID` tracing.
- Health-check endpoints with dependency probes, and CancellationToken support throughout.

### Frontend

- Vue 3.5 on Nuxt 4, TypeScript, Pinia for state.
- Nuxt UI v4 components with a custom `mocha` palette.
- i18n in English, German, and Hungarian, with browser detection and cookie persistence.
- Installable PWA (web manifest, standalone display, service worker).
- Mobile-first, with pull-to-refresh on the main data pages (`usePullToRefresh` + `PullToRefreshIndicator`) and swipe-to-delete / swipe-to-edit on shopping-list cards (`useSwipeActions`, pointer-based with axis lock and haptics; confirm modals as a safety net).
- SSR plus client-side rendering, camera-based barcode/QR scanning with camera-availability detection.

### Security

- Passwordless authentication via Ory Kratos, session-based with configurable lifespans.
- Two-tier rate limiting (global and per-endpoint) with standard headers.
- Security headers (CSP, HSTS, X-Frame-Options, and others).
- Input sanitization with XSS filtering, and image upload validated by magic number and integrity checks.
- CORS with a configurable allow-list, constant-time comparisons for timing-attack protection, and per-endpoint request timeouts.
- API-key authentication for internal service-to-service calls.

### Functionality

- User management: profiles, settings, profile pictures, activity feed.
- Family management: create a family, join by share code through an approval-gated request (request, then approve or reject), member management, family pictures.
- Product inventory with consumption tracking and images.
- Collaborative family shopping lists with purchase tracking (tap a card to quick-purchase, swipe to delete or edit).
- Shopping locations (stores) and storage locations (home, freezer).
- Optional geolocation that highlights the items to buy at the store you are currently in and, while the app is open, fires a local "you have N items to buy here" notification. This is foreground-only; a PWA cannot do background geofencing.
- A calendar combining product expirations and shopping-list deadlines.
- Household statistics (product, shopping-item, and family counts) with nightly caching.
- Barcode lookup via the Open Food Facts API, with camera scanning for multiple formats.
- Search-result highlighting across product and location views.
- Browser-side image compression and cropping.
- Push notifications: alerts at 7 AM for products expiring within 14 days, weekly summaries on Mondays, low-stock and automation alerts, family join-request alerts, and real-time alerts when a family member changes a shared shopping list or inventory.
- Transactional email: OTP messages in English, Hungarian, and German for login, registration, verification, and recovery.
- Item automation: rules for automatic consumption, usage reminders, shopping-list additions, and low-stock thresholds, with multi-day scheduling.

### Data quality

- Barcode validation with checksum verification for EAN-13, EAN-8, UPC-A, UPC-E, and Code-128, with automatic format detection at the API boundary.
- Image validation with format detection (JPEG, PNG, WebP) and dimension limits.

## Project structure

```
Homassy/
├── Homassy.API/           ASP.NET Core Web API (backend)
│   ├── Controllers/       HTTP endpoints (thin layer)
│   ├── Functions/         Business logic and data access
│   ├── Entities/          Database models (EF Core)
│   ├── Models/            DTOs and request/response objects
│   ├── Context/           DbContext and session management
│   ├── Services/          Infrastructure services
│   ├── Middleware/        Exception handling, CORS, compression, logging, rate limiting
│   ├── docs/              Split reference docs (security, features, dev guidelines)
│   └── CLAUDE.md          Architecture documentation (entry point + doc map)
├── Homassy.Email/         Transactional email microservice
│   ├── Endpoints/         Kratos webhook, send-email, weekly-summary, automation-notification
│   ├── Services/          Content, queue, sender, and template-renderer services
│   ├── Templates/         Embedded HTML email templates
│   ├── Workers/           EmailWorkerService (background delivery with retry)
│   └── CLAUDE.md          Architecture documentation
├── Homassy.Notifications/ Push-notifications microservice
│   ├── Endpoints/         Test-push, test-email, low-stock-push
│   ├── Services/          WebPush, content, family notifier, expiration, email client
│   ├── Workers/           Scheduler, activity monitors, join-request monitor, automation worker, email summaries
│   └── CLAUDE.md          Architecture documentation
├── Homassy.Web/           Vue 3 + Nuxt 4 web app (frontend)
│   ├── app/
│   │   ├── pages/         File-based routing
│   │   ├── components/    Reusable Vue components
│   │   ├── composables/   Composition API helpers (api/ holds the client wrappers)
│   │   ├── stores/        Pinia state (auth)
│   │   ├── layouts/       Page layouts (auth, public)
│   │   ├── middleware/    Route guards
│   │   ├── types/         TypeScript definitions
│   │   └── utils/         Utility functions
│   ├── i18n/              Translation files (en, de, hu)
│   ├── public/            Static assets and PWA icons
│   ├── nuxt.config.ts     Nuxt configuration
│   └── Dockerfile         Multi-stage Docker build
├── Homassy.Proxy/         Caddy reverse proxy
│   └── Caddyfile          Single-domain routing (/api/v*, /hubs/*, /kratos/*, and the web app)
├── Homassy.Migrator/      Database migration tool
│   ├── Migrations/        One-time Kratos user migration
│   ├── Program.cs         CLI entry point (migrate, migrate-to-kratos, verify-kratos, kratos-stats)
│   └── Dockerfile         Run-and-exit container
├── Homassy.Kratos/        Ory Kratos configuration
│   ├── kratos.yml             Base configuration (HTTP courier to Homassy.Email)
│   ├── kratos.production.yml  Production overrides
│   ├── identity.schema.json   Identity schema (traits, notification preferences)
│   └── webhook_body.jsonnet   Courier webhook payload template
├── Homassy.Tests/         Test suite (xUnit)
│   ├── Integration/       Integration tests
│   ├── Unit/              Unit tests
│   ├── Infrastructure/    Test helpers
│   └── CLAUDE.md          Testing documentation
└── README.md              This file
```

## Technology stack

### Backend (Homassy.API)

| Category | Technology |
|----------|------------|
| Framework | ASP.NET Core 10.0 |
| Database | PostgreSQL + EF Core 10.0 |
| Authentication | Ory Kratos (self-hosted identity) |
| Logging | Serilog 9.0.0 (structured) |
| API versioning | Asp.Versioning 8.1.0 |
| Health checks | Microsoft.Extensions.Diagnostics.HealthChecks |
| Compression | Brotli + Gzip (built-in) |
| Testing | xUnit 2.9.3 + WebApplicationFactory |
| External APIs | Open Food Facts API v2 |

### Email service (Homassy.Email)

| Category | Technology |
|----------|------------|
| Framework | ASP.NET Core 10.0 (Minimal API) |
| SMTP client | MailKit 4.9 (STARTTLS) |
| Queue | System.Threading.Channels (bounded, cap 500) |
| Logging | Serilog (structured) |
| Languages | English, Hungarian, German |

### Notifications service (Homassy.Notifications)

| Category | Technology |
|----------|------------|
| Framework | ASP.NET Core 10.0 (Minimal API) |
| Push notifications | WebPush (VAPID) |
| Background workers | .NET BackgroundService (6 workers) |
| DB access | EF Core (shared via ProjectReference to Homassy.API) |
| Logging | Serilog (structured) |

### Frontend (Homassy.Web)

| Category | Technology |
|----------|------------|
| Framework | Vue 3.5.25 + Nuxt 4.2.2 |
| UI library | @nuxt/ui 4.3.0 (Radix Vue) |
| Type safety | TypeScript 5.9.3 |
| State management | Pinia (@pinia/nuxt 0.11.3) |
| Internationalization | @nuxtjs/i18n 10.2.1 (en, de, hu) |
| Image processing | vue-advanced-cropper 2.8.9 |
| Barcode scanning | vue-qrcode-reader 5.7.3 |
| Icons | @iconify (Heroicons, Lucide) |
| Date/time | @internationalized/date 3.10.1 |
| Image compression | browser-image-compression 2.0.2 |
| Runtime | Node.js 22-alpine |

### Infrastructure

| Category | Technology |
|----------|------------|
| Containerization | Docker + Docker Compose |
| Database | PostgreSQL 16 |
| Identity | Ory Kratos (self-hosted) |
| Reverse proxy | Caddy 2 (single-domain routing, production) |
| Email delivery | Homassy.Email microservice (MailKit SMTP) |
| Push notifications | Homassy.Notifications microservice (WebPush VAPID) |
| Web server | Kestrel (ASP.NET Core) |
| Node server | Node.js 22 (Nuxt SSR) |

## Entity inheritance hierarchy

```
BaseEntity (Id, PublicId)
  └── SoftDeleteEntity (IsDeleted)
      └── RecordChangeEntity (RecordChange JSON)
          └── User, Family, FamilyJoinRequest, Product, ProductInventoryItem,
              ItemAutomation, ShoppingList, Location, Activity, ...
```

Every entity inherits an internal integer ID, a public GUID (which keeps internal IDs out of the API and prevents enumeration), soft delete, and automatic change tracking.

## Deployment

Deployment runs through GitHub Actions into the GitHub Container Registry (GHCR) and then onto a VPS. There is no local deploy script.

The flow (`.github/workflows/deploy.yml`):

1. A push to `master`, or a manual `workflow_dispatch`, builds the five service images for `linux/amd64`.
2. The images go to `ghcr.io/xentinus/<service>`, tagged with a shared `YYYYMMDD-HHmmss` timestamp and with `latest`, as private packages linked to this repository.
3. The `deploy` job waits for manual approval of the `production` environment.
4. After approval it copies `docker-compose.production.yml`, the Kratos config, the Caddy config, and a generated `.env` to the VPS, then runs `docker compose pull` and `up -d --wait` with the timestamped images.

Built images (`ghcr.io/xentinus/...`): `homassyapi`, `homassyweb`, `homassyemail`, `homassynotifications`, `homassymigrator`. The proxy uses the public `caddy:2-alpine` image, so it is not built here.

In production everything is served from one domain. The Caddy reverse proxy (`Homassy.Proxy/Caddyfile`) listens on host port 3000, behind a Cloudflare tunnel that terminates TLS, and routes `/api/v*` and `/hubs/*` to the API, `/kratos/*` to Kratos, and everything else to the web app. Only the proxy and PostgreSQL publish host ports; the other containers are reachable only on the internal Docker network.

Required GitHub configuration (Settings, then Environments or Secrets and variables):

| Where | Name | Purpose |
|-------|------|---------|
| `production` environment secret | `VPS_HOST`, `VPS_USER`, `VPS_PORT`, `VPS_PASSWORD` | SSH access to the VPS (username and password) |
| `production` environment secret | `GHCR_USERNAME`, `GHCR_PAT` | `read:packages` token so the VPS can pull the private images |
| `production` environment secret | `PROD_ENV` | The full production `.env` (multiline), everything from `.env.example` except the deploy-only block |
| Repository variable | `NUXT_PUBLIC_API_BASE`, `NUXT_PUBLIC_KRATOS_URL` | Baked into the web image at build time |

Add required reviewers to the `production` environment; that approval is the deploy gate.

To roll back, set `IMAGE_TAG` in `/opt/homassy/.env` on the VPS to an earlier timestamp and run `docker compose -f docker-compose.production.yml up -d`.

## License

AGPL-3.0. See [LICENSE.txt](LICENSE.txt).

Copyright (c) 2025 Béla Kellner
