# Homassy.Data - Shared Data Library

> **Note:** This is a living document that is updated as the project evolves.
> It is not versioned - changes are made directly to reflect the current state of the project.

## Table of Contents

1. [Overview](#overview)
2. [Why this project exists](#why-this-project-exists)
3. [Project Structure](#project-structure)
4. [What belongs here, and what does not](#what-belongs-here-and-what-does-not)
5. [The audit-user hook](#the-audit-user-hook)
6. [Migrations](#migrations)
7. [Dependencies](#dependencies)

---

## Overview

`Homassy.Data` is a plain class library (`Microsoft.NET.Sdk`, **not** `.Sdk.Web`) holding the
database model and the logic that more than one process runs against it. Three projects reference
it, and none of them references another:

| Project | What it takes from here |
|---------|-------------------------|
| `Homassy.API` | the context, the entities, the enums, the exceptions, the shared helpers |
| `Homassy.Notifications` | the same, plus the cache-free helpers its workers call |
| `Homassy.Migrator` | the context, the entities, the enums, the Kratos identity models |

`Homassy.Email` deliberately references none of it: it has no database of its own and takes only
`Extensions/SerilogExtensions.cs` as a linked source file.

---

## Why this project exists

`Homassy.Notifications` used to take a `ProjectReference` on `Homassy.API` itself (#91). The tell
was in its `.csproj`:

```xml
<ErrorOnDuplicatePublishOutputFiles>false</ErrorOnDuplicatePublishOutputFiles>
```

One ASP.NET Core web project referencing another produces colliding publish output - both
contribute a runtime config and a static-assets manifest - and the build only succeeded because
the error was switched off rather than because the collision was resolved. That property is gone
now, and `dotnet publish` on either project is clean by default. **That is the test for whether
this split is still intact.**

What the workers actually needed was small: the context, the entities, the enums, and a handful of
pure helpers. What they got was everything - 17 controllers, 7 middleware components, the SignalR
hubs, the Kratos client, the OpenFoodFacts client, the image-processing stack and the health
checks - so the notifications image carried the whole API assembly and its transitive package set,
and every API change invalidated the notifications Docker layer cache.

The subtler cost was the entity caches. `ProductFunctions`, `UserFunctions` and their siblings hold
process-wide static caches that `CacheManagementService` initialises **in the API only**. A worker
that constructed one of those classes got cache code that could never report anything but
`Inited == false`. It worked because the methods being called happened to be pure - but nothing in
the type system said so, and the next cache-backed method called from a worker would have silently
read an empty cache. Splitting the cache-free half out is what makes that impossible rather than
merely unlikely.

---

## Project Structure

```
Homassy.Data/
├── Context/
│   ├── AuditUser.cs                           Who the ambient operation acts as (see below)
│   ├── HomassyDbContext.cs                    Entity config, query filters, audit stamping
│   ├── HomassyDbContextFactory.cs             Design-time factory, for EF tooling only
│   └── HomassyDbContextFactoryExtensions.cs   CreateForReading() - the no-tracking context
├── Entities/             Database entity models (EF Core)
│   └── Activity/  Common/  Family/  Location/  Product/  ShoppingList/  User/
│       (Family/ also holds CalendarNote — a day note belongs to a family, not to a calendar, #60)
├── Enums/                Application enumerations, incl. the 947-member ProductCategory
├── Exceptions/           Domain exceptions, each carrying the status code the API maps it to
│   └── HttpStatus.cs     The five status constants, so this library needs no ASP.NET Core
├── Extensions/
│   ├── SerilogExtensions.cs        Shared minimum-level policy (linked into Homassy.Email too)
│   └── UserTimeZoneExtensions.cs   UserTimeZone → IANA id, used on both sides of the boundary
├── Functions/            Cache-free logic both the API and the workers run
│   ├── ActivityRecorder.cs          The write half of the activity feed
│   ├── AutomationSchedule.cs        When an automation is next due, in the owner's timezone
│   ├── InventoryGridProjection.cs   The grid DTOs the realtime broadcasts carry
│   ├── NotificationFunctions.cs     Notification centre reads/writes + the retention sweep
│   └── ReminderLeadTimes.cs         Reads the calendar reminder lead times out of their JSON column
├── Migrations/           EF Core database migrations
├── Models/
│   ├── Activity/         Activity feed DTOs
│   ├── Barcode/          Barcode validation result
│   ├── Common/           ApiResponse, PagedResult, SelectValue, RecordChange, VersionInfo
│   ├── ExternalCalendar/ CachedICalEvent - the cached shape the reminder worker reads
│   ├── Internal/         The service-to-service broadcast requests
│   ├── Inventory/        InventoryGridProductInfo / InventoryGridItemInfo
│   ├── Kratos/           Kratos session, identity and config models
│   └── Notification/     NotificationEnvelope, NotificationInfo, NotificationPage
├── Security/
│   └── Cryptography.cs   Share-code generation, called from the Family entity
└── Validation/
    ├── BarcodeValidationService.cs   Checksum verification per barcode format
    ├── IBarcodeValidationService.cs
    └── ValidBarcodeAttribute.cs      Used by the Product entity and by the request DTOs
```

---

## What belongs here, and what does not

**Here:** the data model, and code that is *genuinely static and cache-free* - the same inputs give
the same outputs in any process, with no ambient state that one host initialises and another does
not.

**Not here:**

- **Anything cache-backed.** The static entity caches in `Homassy.API/Functions/` stay in the API,
  which is the process that owns them. This is the rule the split exists to enforce.
- **Anything web.** No controllers, no middleware, no hubs, no `Microsoft.AspNetCore.*`. The
  exceptions carry HTTP status codes as plain integers (`HttpStatus`) precisely so that referencing
  ASP.NET Core for five constants is unnecessary.
- **Request and response DTOs.** Those are the API's contract and live in `Homassy.API/Models/`.
  What lives here are the DTOs that *cross a service boundary* - the internal broadcast requests,
  the inventory grid projections, the cached calendar event - where two processes have to agree on
  a shape.

A helper that is about to be shared is usually a sign to check the first rule: if it reads a cache,
it is not shared logic, it is API logic with a worker-shaped hole in it.

---

## The audit-user hook

`HomassyDbContext.SaveChanges` stamps `RecordChangeEntity` rows with the acting user's id. That id
used to come from `Homassy.API`'s `SessionInfo`, which was the one thing keeping the context tied
to the web layer.

`Context/AuditUser.cs` is the seam now: an `AsyncLocal<int?>` this library owns, which `SessionInfo`
writes when it resolves a Kratos session and clears when the request ends. `SessionInfo` keeps
everything else about the request identity - the session, the public id, the family, the language -
and none of it reaches the context.

In a host with no request the value is null, which is the correct answer there: a row a background
worker writes was not changed by a person.

---

## Migrations

Migration files live in `Homassy.Data/Migrations/`, next to the context they describe.
`dotnet ef` still needs a startup project that can build a configured context, and that is
`Homassy.API`:

```bash
dotnet ef migrations add <MigrationName> --project Homassy.Data --startup-project Homassy.API
dotnet ef migrations list --project Homassy.Data --startup-project Homassy.API
```

They are applied at deploy time by `Homassy.Migrator`, never by the API on startup.

---

## Dependencies

| Package | Why |
|---------|-----|
| `Microsoft.EntityFrameworkCore` | the context and the entity configuration |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | the provider the migrations and column types are written against |
| `Microsoft.EntityFrameworkCore.Design` | the design-time factory, so `dotnet ef` can target this project |
| `Microsoft.Extensions.Configuration.Json` / `.EnvironmentVariables` | the design-time factory reads a connection string |
| `Serilog` | the shared logging policy and the log lines in `Functions/` |

`System.Security.Cryptography.Xml` is pinned but unused: `EntityFrameworkCore.Design` pulls
`Microsoft.Build.Tasks.Core`, which otherwise resolves a version with eight high-severity
advisories. `Homassy.Migrator` carries the same pin for the same reason.
