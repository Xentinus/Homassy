# Homassy.Email - Service Architecture Documentation

> **Note:** This is a living document that is updated as the project evolves.
> It is not versioned - changes are made directly to reflect the current state of the project.

## Table of Contents

1. [Overview](#overview)
2. [Technology Stack](#technology-stack)
3. [Project Structure](#project-structure)
4. [Architecture](#architecture)
5. [Endpoints](#endpoints)
6. [Email Types & Internationalization](#email-types--internationalization)
7. [Template System](#template-system)
8. [Queue & Retry System](#queue--retry-system)
9. [Security](#security)
10. [Health Checks](#health-checks)
11. [Configuration](#configuration)
12. [Development Guidelines](#development-guidelines)

---

## Overview

Homassy.Email is a lightweight, standalone microservice responsible for sending transactional emails. It operates completely independent of the main Homassy.API and is designed to be called internally only (never directly from the frontend).

### Key Architectural Decisions

- **Minimal ASP.NET Core** – No controllers, no EF Core. Uses Minimal API endpoints (`app.MapPost(...)`) for simplicity
- **In-process Channel Queue** – Emails are enqueued into a bounded `System.Threading.Channels` queue before sending
- **Background Worker** – `EmailWorkerService` drains the queue asynchronously with retry logic
- **API Key Authentication** – All endpoints (except `/health/*`) require `X-Api-Key` header validated with constant-time comparison
- **Single HTML Template** – One embedded `CodeEmail.html` template rendered with simple `{{TOKEN}}` substitution
- **Multilingual** – Supports English, Hungarian (`hu`), and German (`de`) for all email content
- **Two entry points** – Kratos webhook (`/kratos/webhook`) and a generic send endpoint (`/email/send`)
- **SMTP via MailKit** – Connects per-email with STARTTLS, no persistent connection pool

---

## Technology Stack

- **.NET 10.0** – ASP.NET Core (Minimal API)
- **MailKit** – SMTP client for sending emails (STARTTLS)
- **Serilog** – Structured logging (console sink)
- **System.Threading.Channels** – In-process bounded queue (capacity: 500)
- **Microsoft.AspNetCore.Diagnostics.HealthChecks** – Health probes

---

## Project Structure

```
Homassy.Email/
├── Endpoints/             Minimal API endpoint handlers
│   ├── KratosWebhookEndpoint.cs   Receives Kratos courier webhook
│   └── SendEmailEndpoint.cs       Generic email send endpoint
├── Enums/
│   ├── EmailType.cs        LoginCode | RegistrationCode | VerificationCode | RecoveryCode
│   └── Language.cs         English | Hungarian | German
├── HealthChecks/
│   └── SmtpHealthCheck.cs  Tests live SMTP connectivity (tagged "ready")
├── Middleware/
│   └── ApiKeyMiddleware.cs  X-Api-Key validation (constant-time comparison)
├── Models/
│   ├── EmailMessage.cs         Internal queue record (To, Subject, HtmlBody, PlainTextBody, AttemptCount)
│   ├── KratosTemplateData.cs   Deserialized Kratos webhook payload
│   ├── KratosWebhookRequest.cs Top-level Kratos webhook request model
│   └── SendEmailRequest.cs     Generic send endpoint request model
├── Services/
│   ├── IEmailContentService.cs      Interface
│   ├── EmailContentService.cs       Builds subjects, greetings, messages (multilingual)
│   ├── IEmailQueueService.cs        Interface
│   ├── EmailQueueService.cs         Bounded channel queue (capacity 500, DropWrite on full)
│   ├── IEmailSenderService.cs       Interface
│   ├── EmailSenderService.cs        SMTP send via MailKit (scoped)
│   ├── ITemplateRendererService.cs  Interface
│   └── TemplateRendererService.cs   Loads embedded HTML template, replaces {{TOKEN}} placeholders
├── Templates/
│   └── CodeEmail.html   Single embedded HTML template for all code emails
├── Workers/
│   └── EmailWorkerService.cs   BackgroundService – drains queue, retries failed sends
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```

---

## Architecture

### Request Flow

```
Kratos Courier ──POST /kratos/webhook──▶ KratosWebhookEndpoint
                                               │
Homassy.API ────POST /email/send──────▶ SendEmailEndpoint
                                               │
                                               ▼
                                      IEmailContentService
                                      ITemplateRendererService
                                               │
                                        EmailMessage record
                                               │
                                               ▼
                                      IEmailQueueService
                                      (Channel, cap 500)
                                               │
                                               ▼
                                      EmailWorkerService
                                      (BackgroundService)
                                               │
                                               ▼
                                      IEmailSenderService
                                      (MailKit SMTP)
```

### Service Lifetimes

| Service | Lifetime | Reason |
|---------|----------|--------|
| `IEmailQueueService` | Singleton | Shared channel across all requests |
| `ITemplateRendererService` | Singleton | Template loaded once from embedded resource |
| `IEmailContentService` | Singleton | Pure functions, no state |
| `IEmailSenderService` | Scoped | New SMTP connection per email send |
| `EmailWorkerService` | Hosted Service | Runs for the lifetime of the application |

---

## Endpoints

### `POST /kratos/webhook`

Receives webhook calls from Ory Kratos Courier when it needs to send an authentication email. Returns `200 OK` always (even on unknown template types) to avoid Kratos retrying.

**Authentication:** `X-Api-Key` header required.

**Request body** (sent by Kratos, defined in `webhook_body.jsonnet`):
```json
{
  "to": "user@example.com",
  "template_type": "login_code_valid",
  "template_data": {
    "login_code": "123456",
    "registration_code": null,
    "verification_code": null,
    "recovery_code": null,
    "identity": {
      "traits": {
        "email": "user@example.com",
        "name": "John",
        "displayName": "John Doe",
        "defaultLanguage": "hu"
      }
    }
  }
}
```

**Supported `template_type` values:**

| template_type | EmailType | Expiry |
|---------------|-----------|--------|
| `login_code_valid` | `LoginCode` | 15 min |
| `registration_code_valid` | `RegistrationCode` | 15 min |
| `verification_code_valid` | `VerificationCode` | 60 min |
| `recovery_code_valid` | `RecoveryCode` | 60 min |

**Behavior:**
- Unknown `template_type` → logs warning, returns `200 OK` (silent discard)
- Missing code in payload → logs warning, returns `200 OK`
- Language is derived from `template_data.identity.traits.defaultLanguage`
- Name is derived from `traits.name` or `traits.displayName` (whichever is present)

---

### `POST /email/send`

Generic email send endpoint for direct use by Homassy.API or other internal services.

**Authentication:** `X-Api-Key` header required.

**Request body:**
```json
{
  "to": "user@example.com",
  "type": "login_code",
  "language": "hu",
  "params": {
    "name": "John",
    "code": "123456",
    "expiresInMinutes": 15
  }
}
```

**Supported `type` values:** `login_code`, `registration_code`, `verification_code`, `recovery_code`

**Supported `language` values:** `en` (default), `hu`, `de`

**Response:**
- `200 OK` – email enqueued (or dropped if queue full, logged as warning)
- `400 Bad Request` – unknown `type` value

---

### `GET /health/live`

Liveness probe. Always returns `200 OK` with no checks. Confirms the process is running.

### `GET /health/ready`

Readiness probe. Runs `SmtpHealthCheck` – attempts a live SMTP connection. Returns:
- `200 OK` – SMTP connection successful
- `503 Service Unavailable` – SMTP connection failed

---

## Email Types & Internationalization

Four email types are supported, each fully translated into three languages:

| EmailType | Use case | Default expiry |
|-----------|----------|----------------|
| `LoginCode` | Passwordless login code | 15 min |
| `RegistrationCode` | New account code verification | 15 min |
| `VerificationCode` | Email address verification | 60 min |
| `RecoveryCode` | Account recovery | 60 min |

**Supported languages:**

| Code | `Language` enum | Fallback |
|------|-----------------|----------|
| `en` (or unknown) | `Language.English` | ✅ default |
| `hu` | `Language.Hungarian` | – |
| `de` | `Language.German` | – |

**Translated content per email (via `EmailContentService`):**

- Subject line
- Greeting
- Message body (with optional `name` interpolation)
- Code label
- Expiry text
- Security note
- Footer copyright
- Footer auto-message
- Plain text fallback (for clients that don't render HTML)

---

## Template System

### HTML Template (`Templates/CodeEmail.html`)

A single HTML template is used for all email types. It is embedded as an assembly resource at build time (`EmbeddedResource` in `.csproj`) and loaded once by `TemplateRendererService` on startup.

**Token substitution** uses `{{TOKEN_NAME}}` syntax:

| Token | Description |
|-------|-------------|
| `{{GREETING}}` | Email greeting (e.g. "Welcome back!") |
| `{{MESSAGE}}` | Main message body |
| `{{CODE}}` | The 6-digit verification/recovery code |
| `{{CODE_LABEL}}` | Label above the code (e.g. "Verification Code") |
| `{{EXPIRES_AT}}` | Expiry text (e.g. "Expires in 15 minutes") |
| `{{SECURITY_NOTE}}` | Security reminder text |
| `{{FOOTER_COPYRIGHT}}` | Copyright line |
| `{{FOOTER_AUTO_MESSAGE}}` | "Do not reply" message |

**`TemplateRendererService`** loads the embedded resource on construction and replaces tokens on every `Render()` call via `string.Replace()`.

### Adding a New Token

1. Add `{{NEW_TOKEN}}` in `Templates/CodeEmail.html`
2. Add a corresponding content method to `IEmailContentService` and `EmailContentService`
3. Pass the token in the `renderer.Render(...)` dictionary in both `KratosWebhookEndpoint` and `SendEmailEndpoint`

### Adding a New Language

1. Add a value to `Enums/Language.cs`
2. Add a `case` for the new language code in `EmailContentService.ParseLanguage()`
3. Add translations for every string method in `EmailContentService` (subject, greeting, message, code label, expiry, security note, footer, plain text)

---

## Queue & Retry System

### EmailQueueService

An in-process bounded channel queue with the following properties:

- **Capacity:** 500 messages
- **Full mode:** `DropWrite` – if the queue is full, new messages are silently dropped (with a warning log)
- **Concurrency:** `SingleReader = true` (only `EmailWorkerService` reads), `SingleWriter = false` (multiple threads can write)

```csharp
Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500)
{
    FullMode = BoundedChannelFullMode.DropWrite,
    SingleReader = true,
    SingleWriter = false
});
```

### EmailMessage Record

```csharp
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string PlainTextBody,
    int AttemptCount = 0  // incremented on each retry
);
```

### EmailWorkerService (BackgroundService)

Continuously reads from the queue and sends emails. On failure, retries with exponential backoff:

| Attempt | Delay before retry |
|---------|--------------------|
| 1st failure | 2 seconds |
| 2nd failure | 4 seconds |
| 3rd failure (final) | No retry – logged as permanent error, message discarded |

**Max attempts:** 3

**On permanent failure:** logs an error with full exception details. The message is discarded (no dead-letter queue).

**Retry flow:**
```
Dequeue message
  → Send via IEmailSenderService
  → Success: log info, continue
  → Failure:
      AttemptCount < MaxAttempts: wait backoff, re-enqueue with AttemptCount+1
      AttemptCount >= MaxAttempts: log error, discard
```

---

## Security

### API Key Authentication (`ApiKeyMiddleware`)

All endpoints except `/health/*` require an `X-Api-Key` header.

**Features:**
- **Constant-time comparison** using `CryptographicOperations.FixedTimeEquals()` to prevent timing side-channel attacks
- Pads both keys to the same length before comparison
- Returns `401 Unauthorized` on missing or invalid key
- Returns `500 Internal Server Error` if `Email:ApiKey` is not configured
- Health endpoints (`/health/*`) are always exempt

**Usage (from Homassy.API or Kratos):**
```
X-Api-Key: your-secret-api-key-here
```

---

## Health Checks

### SmtpHealthCheck

Tests a live SMTP connection on every readiness probe call:

1. Connects to the configured SMTP server with STARTTLS
2. Authenticates if `Username` and `Password` are provided
3. Disconnects cleanly

| Result | Condition |
|--------|-----------|
| `Healthy` | Successful connect + auth + disconnect |
| `Unhealthy` | Any exception (connection refused, auth failed, timeout) |
| `Unhealthy` | `Email:SmtpServer` not configured |

**Endpoints:**
- `GET /health/live` – always `200 OK`, no checks run
- `GET /health/ready` – runs `SmtpHealthCheck` (tagged `"ready"`)

---

## Configuration

### `appsettings.json`

```json
{
  "Email": {
    "SmtpServer": "smtp.example.com",
    "SmtpPort": "587",
    "SenderEmail": "noreply@example.com",
    "Username": "smtp-user",
    "Password": "smtp-password",
    "ApiKey": "internal-api-key"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Configuration Keys

| Key | Required | Default | Description |
|-----|----------|---------|-------------|
| `Email:SmtpServer` | ✅ | – | SMTP server hostname |
| `Email:SmtpPort` | – | `587` | SMTP port (STARTTLS) |
| `Email:SenderEmail` | ✅ | – | From address |
| `Email:Username` | ✅ | – | SMTP auth username |
| `Email:Password` | ✅ | – | SMTP auth password |
| `Email:ApiKey` | ✅ | – | Internal API key for `X-Api-Key` header |

---

## Development Guidelines

### Adding a New Email Type

1. **Add to enum** – `Enums/EmailType.cs`
2. **Add content** – Implement all methods in `EmailContentService` for the new type (subject, greeting, message, code label, security note, plain text). Follow the existing `switch` expression pattern.
3. **Register the type string** – Add a mapping entry in both `KratosWebhookEndpoint.TemplateTypeMap` and `SendEmailEndpoint.TypeMap`
4. **Test** – Use `POST /email/send` with the new `type` value

### Sending an Email Manually

```http
POST http://homassy-email:8080/email/send
Content-Type: application/json
X-Api-Key: your-secret-api-key

{
  "to": "user@example.com",
  "type": "login_code",
  "language": "hu",
  "params": {
    "name": "Test User",
    "code": "123456",
    "expiresInMinutes": 15
  }
}
```

### Queue Monitoring

Log messages to watch for:

| Log message | Meaning |
|-------------|---------|
| `Email queue full – dropped email` | Queue at capacity (500), emails being dropped |
| `Permanent email failure` | Message exhausted all 3 retry attempts |
| `Email send failed ... Retrying` | Transient SMTP failure, will retry |

### Template Development

To modify the HTML template:
1. Edit `Templates/CodeEmail.html`
2. Use `{{TOKEN_NAME}}` for dynamic content
3. The template is embedded at build time – no file copy needed
4. Restart the service to pick up changes

### Middleware Pipeline

```
Incoming Request
    ↓
Serilog Request Logging (app.UseSerilogRequestLogging)
    ↓
ApiKeyMiddleware  (validates X-Api-Key, skips /health/*)
    ↓
Endpoint routing
    ↓
KratosWebhookEndpoint  (POST /kratos/webhook)
SendEmailEndpoint      (POST /email/send)
SmtpHealthCheck        (GET /health/ready)
Liveness               (GET /health/live)
```

### Important Notes

- **No database** – the queue is in-memory; pending emails are lost on process restart
- **No auth flows** – this service only sends emails; all auth logic lives in Kratos and Homassy.API
- **Always returns 200 from Kratos webhook** – prevents Kratos from retrying on unknown template types
- **Queue overflow = silent drop** – if the queue fills up (>500 pending), new emails are dropped and logged; this is acceptable for transactional codes (user can request a new one)
- **Per-send SMTP connection** – `EmailSenderService` is scoped and opens/closes a fresh SMTP connection for every email; there is no connection pool
