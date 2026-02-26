# Homassy.Migrator - Tool Documentation

> **Note:** This is a living document that is updated as the project evolves.
> It is not versioned - changes are made directly to reflect the current state of the project.

## Table of Contents

1. [Overview](#overview)
2. [Technology Stack](#technology-stack)
3. [Project Structure](#project-structure)
4. [Commands](#commands)
5. [EF Core Migrations](#ef-core-migrations)
6. [Kratos User Migration](#kratos-user-migration)
7. [Database Readiness Wait](#database-readiness-wait)
8. [Configuration](#configuration)
9. [Docker](#docker)
10. [Shell Script](#shell-script)
11. [Exit Codes](#exit-codes)
12. [Development Guidelines](#development-guidelines)

---

## Overview

Homassy.Migrator is a standalone console application (run-and-exit) responsible for:

1. **Running EF Core database migrations** – applies pending schema migrations to the PostgreSQL database
2. **Migrating existing users to Ory Kratos** – creates Kratos identities for users who were onboarded before Kratos was integrated

It is designed to run as a short-lived Docker container during deployments, before the main API starts. It is **not** a long-running service.

The migrator references `Homassy.API.csproj` directly to reuse entities, `HomassyDbContext`, and enum types – it does not duplicate any data model code.

---

## Technology Stack

- **.NET 10.0** – Console application (`OutputType: Exe`)
- **EF Core 10.0 + Npgsql** – database migrations and queries (shared with Homassy.API)
- **Microsoft.Extensions.Configuration** – loads `appsettings.json` and environment variables
- **HttpClient** – calls Kratos Admin API directly (no Kratos SDK)
- **Project reference** to `Homassy.API` – reuses `HomassyDbContext`, entities, enums, and Kratos models

---

## Project Structure

```
Homassy.Migrator/
├── Migrations/
│   └── KratosUserMigration.cs   One-time Kratos user migration logic
├── Scripts/
│   └── migrate-users-to-kratos.sh   Helper shell script for running migration locally
├── Dockerfile
├── Homassy.Migrator.csproj
└── Program.cs                   Entry point; parses commands, waits for DB, dispatches
```

> **Note:** EF Core migration files live in `Homassy.API/Migrations/`. The Migrator applies those migrations by loading `HomassyDbContext` from the API project.

---

## Commands

Passed as the first CLI argument. Default (no argument): `migrate`.

```bash
dotnet Homassy.Migrator.dll [command] [options]
```

| Command | Description |
|---------|-------------|
| `migrate` | Apply pending EF Core migrations **(default)** |
| `migrate-to-kratos` | Run EF migrations first, then migrate all unmigrated users to Kratos |
| `verify-kratos` | Verify how many users still lack a `KratosIdentityId` |
| `kratos-stats` | Print total / migrated / pending user counts |

**Options:**

| Option | Applicable to | Description |
|--------|--------------|-------------|
| `--dry-run` | `migrate-to-kratos` | Simulate the migration without writing any changes |

---

## EF Core Migrations

Applies all pending EF Core migrations from `Homassy.API/Migrations/` using `context.Database.MigrateAsync()`.

### Creating a New Migration

Migrations are created from the **Homassy.API project**, not the Migrator:

```bash
cd Homassy.API
dotnet ef migrations add <MigrationName> --project ../Homassy.API --startup-project ../Homassy.API
```

The Migrator picks them up automatically on next run.

---

## Kratos User Migration

**File:** `Migrations/KratosUserMigration.cs`

A one-time migration that creates Kratos identities for users who existed before Kratos was integrated. After this migration, all new users are created directly in Kratos via `KratosAdminService` in Homassy.API.

### Migration Flow

```
1. Check Kratos Admin API connectivity (GET /health/alive)
2. Load all users WHERE KratosIdentityId IS NULL (ordered by Id)
3. Process in batches of 50
   For each user:
     a. Check if identity already exists in Kratos by email
        → If yes: link existing identity (update KratosIdentityId), mark AlreadyMigrated
        → If no (and not dry-run): POST /admin/identities to create new identity
     b. Set user.KratosIdentityId = identity ID
4. SaveChangesAsync() after each batch
5. Print summary
```

### What Gets Migrated to Kratos Traits

From `User` entity: `Email`, `Name`, `FamilyId`

From `UserProfile` entity (if present):

| Profile field | Kratos trait | Notes |
|---------------|-------------|-------|
| `DisplayName` | `display_name` | – |
| `ProfilePictureBase64` | `profile_picture_base64` | – |
| `DateOfBirth` | `date_of_birth` | ISO 8601 string |
| `Gender` | `gender` | – |
| `DefaultCurrency` | `default_currency` | enum → `"HUF"`, `"EUR"`, etc. |
| `DefaultTimeZone` | `default_timezone` | enum → `"CentralEuropeStandardTime"`, etc. |
| `DefaultLanguage` | `default_language` | enum → `"hu"`, `"de"`, `"en"` |

### Migrated Users Are Marked as Verified

All identities created during migration have their email address pre-verified – no email confirmation is sent to migrated users:

```csharp
verifiable_addresses = [{ value, via: "email", verified: true, status: "completed" }]
```

### Batch Processing

- Default batch size: **50 users**
- `SaveChangesAsync()` called after each batch (not per-user)
- Console output per user: `✓` migrated, `⊙` already existed in Kratos, `✗` failed

### Dry Run

```bash
dotnet Homassy.Migrator.dll migrate-to-kratos --dry-run
```

Kratos connectivity and user lookup are real; no identities are created and no DB writes occur. Reports what would happen per user.

### Verification

```bash
dotnet Homassy.Migrator.dll verify-kratos
```

Counts users without `KratosIdentityId`. Lists up to 10 unmigrated users by ID and email. Returns exit code `3` if any remain.

### Stats

```bash
dotnet Homassy.Migrator.dll kratos-stats
```

Prints total / migrated / pending counts and percentage complete.

---

## Database Readiness Wait

Before executing any command, the migrator polls PostgreSQL until it is connectable:

- **Max attempts:** 30
- **Delay:** 2 seconds between attempts (~60 second total timeout)

Allows the container to start immediately in Docker Compose without needing explicit health check delays in Compose configuration.

```
Attempt 1/30: PostgreSQL not ready yet (Connection refused)
Attempt 2/30: PostgreSQL not ready yet (Connection refused)
✓ PostgreSQL is ready (attempt 3/30)
```

---

## Configuration

Reads from `appsettings.json` and environment variables (env vars take precedence).

| Key | Required | Default | Description |
|-----|----------|---------|-------------|
| `ConnectionStrings:DefaultConnection` | ✅ | – | PostgreSQL connection string |
| `Kratos:AdminUrl` | For Kratos commands | `http://localhost:4434` | Kratos Admin API base URL |

In Docker Compose, use double-underscore notation for nested keys: `ConnectionStrings__DefaultConnection`, `Kratos__AdminUrl`.

### Example `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=homassy;Username=homassy;Password=secret"
  },
  "Kratos": {
    "AdminUrl": "http://localhost:4434"
  }
}
```

---

## Docker

Multi-stage Dockerfile:

| Stage | Base image | Purpose |
|-------|-----------|---------|
| `build` | `dotnet/sdk:10.0` | Restore and build |
| `publish` | inherits build | `dotnet publish` |
| `final` | `dotnet/aspnet:10.0` | Lightweight runtime (not SDK) |

Both `Homassy.Migrator.csproj` and `Homassy.API.csproj` are restored before copying full source, for Docker layer caching.

### Typical Docker Compose Configuration

```yaml
homassy-migrator:
  build:
    context: .
    dockerfile: Homassy.Migrator/Dockerfile
  environment:
    - ConnectionStrings__DefaultConnection=Host=homassy-postgres;...
    - Kratos__AdminUrl=http://homassy-kratos:4434
  depends_on:
    - homassy-postgres
  command: ["migrate"]   # or "migrate-to-kratos"
  restart: "no"          # run once and exit
```

---

## Shell Script

**File:** `Scripts/migrate-users-to-kratos.sh`

Convenience wrapper for running the Kratos user migration locally (outside Docker).

```bash
./Scripts/migrate-users-to-kratos.sh [options]

  --dry-run    Simulate migration without making changes
  --verify     Only verify migration status
  --stats      Only show migration statistics
  -h, --help   Show help
```

### What the script does

1. Checks Docker is running
2. Ensures `homassy-postgres` container is up (starts it if not)
3. Ensures `homassy-kratos` container is up (starts it if not)
4. Verifies Kratos health via `GET http://localhost:4434/health/alive`
5. Builds the migrator in Release mode
6. Runs the appropriate command
7. For a full (non-dry-run) migration: prompts `"Are you sure? (yes/no)"` before proceeding, then auto-verifies after completion

---

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success – operation completed normally |
| `1` | Fatal error – unhandled exception, missing config, unknown command, or DB unreachable |
| `2` | Partial success – `migrate-to-kratos` completed but some users failed |
| `3` | Verification failed – `verify-kratos` found unmigrated users |

---

## Development Guidelines

### Adding a New One-Time Migration

1. Create a new class in `Migrations/` (e.g. `SomeDataMigration.cs`)
2. Add a new `case` in the `switch` in `Program.cs`
3. Wire up any required services/config directly in the case block
4. Document the new command and exit behaviour here

### Running Locally (without Docker)

```bash
cd Homassy.Migrator

dotnet run -- migrate                        # Apply EF Core migrations
dotnet run -- migrate-to-kratos --dry-run    # Dry-run Kratos migration
dotnet run -- kratos-stats                   # Show stats
dotnet run -- verify-kratos                  # Check migration status
```

Requires `appsettings.json` in the working directory with a valid `DefaultConnection` and `Kratos:AdminUrl`.

### Important Notes

- **EF migrations live in `Homassy.API/Migrations/`** – always add new migrations from the API project with `dotnet ef migrations add`; the Migrator only applies them
- **Kratos migration is idempotent** – if a user already exists in Kratos by email, it links the existing identity instead of creating a duplicate
- **No DI container, no ASP.NET pipeline** – plain console app; services are instantiated directly in `Program.cs`
- **Connection string masking** – passwords are redacted in console output via `DbConnectionStringBuilder` before logging
