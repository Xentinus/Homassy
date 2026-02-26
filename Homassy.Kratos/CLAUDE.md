# Homassy.Kratos - Configuration Documentation

> **Note:** This is a living document that is updated as the project evolves.
> It is not versioned - changes are made directly to reflect the current state of the project.

## Table of Contents

1. [Overview](#overview)
2. [Files](#files)
3. [Identity Schema](#identity-schema)
4. [Authentication Methods](#authentication-methods)
5. [Self-service Flows](#self-service-flows)
6. [Session & Cookie Configuration](#session--cookie-configuration)
7. [Email Delivery (Courier)](#email-delivery-courier)
8. [webhook_body.jsonnet](#webhook_bodyjsonnet)
9. [Database](#database)
10. [Security](#security)
11. [Config Files](#config-files)
12. [Integration with Homassy](#integration-with-homassy)
13. [Environment Differences](#environment-differences)

---

## Overview

This directory contains all configuration for [Ory Kratos](https://www.ory.sh/kratos/), the open-source identity and user management server used by Homassy. Kratos handles:

- **Passwordless authentication** via email one-time codes (OTP)
- **WebAuthn / Passkey** authentication
- **Account recovery** and **email verification**
- **Session management** (issuance, validation, revocation)
- **Identity storage** (user traits stored in Kratos, synced to Homassy.API)
- **Email delivery** via HTTP webhook to Homassy.Email

Homassy.API does **not** handle authentication logic directly – it delegates entirely to Kratos. The API validates incoming requests by calling the Kratos session check endpoint (`/sessions/whoami`) through `KratosSessionMiddleware`.

---

## Files

| File | Purpose |
|------|---------|
| `kratos.yml` | Base configuration (development defaults) |
| `kratos.development.yml` | Development environment overrides (longer lifespans, verbose logging) |
| `kratos.production.yml` | Complete production configuration with production URLs |
| `identity.schema.json` | JSON Schema defining the Homassy identity (user traits) |
| `webhook_body.jsonnet` | Jsonnet template transforming Kratos courier data → Homassy.Email request |
| `init-kratos-schema.sql` | SQL to create the isolated `kratos` PostgreSQL schema |

---

## Identity Schema

**File:** `identity.schema.json`

Defines the shape of every Kratos identity (user). All fields live under `traits`.

### Required Fields

| Field | Type | Constraints |
|-------|------|-------------|
| `email` | string (email) | 3–320 chars; used for code auth, verification, recovery |
| `name` | string | 1–255 chars; full name |
| `display_name` | string | 1–100 chars; display name shown in UI |

### Optional Fields

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `profile_picture_base64` | string | – | Max 5 MB base64 encoded |
| `date_of_birth` | string (date) | – | ISO 8601 date |
| `gender` | string | – | Max 50 chars |
| `default_currency` | string (enum) | `HUF` | `HUF`, `EUR`, `USD`, `GBP`, `CHF`, `PLN`, `CZK`, `RON` |
| `default_timezone` | string (enum) | `CentralEuropeStandardTime` | 6 IANA-compatible timezone names |
| `default_language` | string (enum) | `hu` | `hu`, `de`, `en` |
| `family_id` | integer | – | Min 1; links identity to a Homassy family |
| `notification_preferences` | object | all `true` | See below |

### `notification_preferences` Object

```json
{
  "email_notifications": true,
  "push_notifications": true,
  "expiring_products_reminder": true,
  "shopping_list_updates": true
}
```

### Email Credential Configuration

The `email` field is configured as the identifier for both **code** (OTP) and **WebAuthn** authentication methods, and as the address for **verification** and **recovery** flows:

```json
"ory.sh/kratos": {
  "credentials": {
    "webauthn": { "identifier": true },
    "code": { "identifier": true, "via": "email" }
  },
  "verification": { "via": "email" },
  "recovery": { "via": "email" }
}
```

### Schema Constraints

- `additionalProperties: false` – no extra fields allowed on `traits`
- `required: ["email", "name", "display_name"]`

---

## Authentication Methods

### Code (OTP / Passwordless)

Email-based one-time code authentication. The user enters their email, receives a code, and enters it to log in or register.

```yaml
methods:
  code:
    passwordless_enabled: true
    enabled: true
    config:
      lifespan: 15m   # 30m in development
```

- Code lifespan: **15 minutes** (production), **30 minutes** (development)
- Used for: login, registration, recovery, verification

### WebAuthn (Passkey / Passwordless)

Biometric or hardware key authentication. Configured as fully passwordless (no password fallback).

```yaml
methods:
  webauthn:
    enabled: true
    config:
      passwordless: true
      rp:
        display_name: Homassy
        id: localhost          # "kellner.dev" in production
        origins:
          - http://localhost:3000   # "https://homassy.kellner.dev" in production
```

---

## Self-service Flows

### Lifespans

| Flow | Development | Production |
|------|-------------|------------|
| Login | 30m | 10m |
| Registration | 30m | 10m |
| Recovery | 2h | 1h |
| Verification | 2h | 1h |
| Settings (privileged) | – | 15m |

### UI URLs (`/auth/*` on the frontend)

| Flow | UI URL path |
|------|------------|
| Login | `/auth/login` |
| Registration | `/auth/register` |
| Recovery | `/auth/recovery` |
| Verification | `/auth/verify` |
| Settings | `/auth/settings` |
| Error | `/auth/error` |

### Flow Details

- **Login** – code (OTP) or WebAuthn; redirects to frontend root after
- **Registration** – code or WebAuthn; can be disabled in Homassy.API via the `RegistrationEnabled` flag in `appsettings.json`
- **Recovery** – code-based; `notify_unknown_recipients: false` (don't leak user existence)
- **Verification** – code-based; `notify_unknown_recipients: false`; redirects to frontend root after verification
- **Settings** – requires `aal1` (active session); privileged session age 15m
- **Logout** – redirects to `/auth/login` after

---

## Session & Cookie Configuration

```yaml
session:
  lifespan: 720h      # 30 days (production), 168h / 7 days (development)
  cookie:
    name: ory_kratos_session
    persistent: true
    same_site: Lax
    domain: localhost   # ".kellner.dev" in production (leading dot = all subdomains)

cookies:
  domain: localhost     # ".kellner.dev" in production
  same_site: Lax
```

**Key details:**
- Session cookie name: `ory_kratos_session`
- Sessions last **30 days** in production (720 hours)
- Production cookie domain uses `.kellner.dev` (leading dot = valid across all subdomains)
- `SameSite: Lax` – cookie sent on top-level navigations, not cross-site POST
- Homassy.API also accepts sessions via `X-Session-Token` header (for API/mobile clients)

---

## Email Delivery (Courier)

Kratos uses the **HTTP delivery strategy** (not SMTP directly). When it needs to send an email, it POSTs to the Homassy.Email service via webhook.

```yaml
courier:
  delivery_strategy: http
  http:
    request_config:
      url: http://homassy-email:8080/kratos/webhook
      method: POST
      body: file:///etc/config/kratos/webhook_body.jsonnet
      headers:
        Content-Type: application/json
        X-Api-Key: REPLACE_EMAIL_SERVICE_API_KEY
```

**Important:** `REPLACE_EMAIL_SERVICE_API_KEY` must match the `Email:ApiKey` value in Homassy.Email's configuration. This is injected at runtime via Docker Compose environment variables.

**Triggered for:**
- Login code emails
- Registration code emails
- Email verification codes
- Account recovery codes

---

## webhook_body.jsonnet

**File:** `webhook_body.jsonnet`

A [Jsonnet](https://jsonnet.org/) template that transforms Kratos's internal `httpDataModel` into the JSON payload expected by `POST /kratos/webhook` on Homassy.Email.

### Why Jsonnet?

Kratos's internal data model differs between flow types. The `registration_code_valid` template has traits at the top level of `template_data`, while `login_code_valid` nests them under `template_data.identity`. The Jsonnet template normalizes these into a consistent shape.

### Output Structure

```json
{
  "to": "user@example.com",
  "template_type": "login_code_valid",
  "template_data": {
    "to": "user@example.com",
    "login_code": "123456",
    "registration_code": null,
    "verification_code": null,
    "recovery_code": null,
    "identity": {
      "id": "kratos-identity-uuid",
      "traits": {
        "email": "user@example.com",
        "name": "John",
        "display_name": "John Doe",
        "default_language": "hu"
      }
    }
  }
}
```

### Trait Extraction Logic

The Jsonnet handles the structural difference between flow types by checking both possible trait locations:

```jsonnet
name:
  if "template_data" in ctx && "traits" in ctx.template_data && "name" in ctx.template_data.traits
  then ctx.template_data.traits.name              // registration flow (traits at top level)
  else if "template_data" in ctx && "identity" in ctx.template_data && ...
  then ctx.template_data.identity.traits.name      // login/verify/recover flows (traits nested)
  else null
```

Fields extracted and forwarded: `email`, `name`, `display_name`, `default_language`

---

## Database

**File:** `init-kratos-schema.sql`

```sql
CREATE SCHEMA IF NOT EXISTS kratos;
```

Kratos stores all its data (identities, sessions, flows, courier messages) in an isolated `kratos` PostgreSQL schema, completely separated from the main `public` schema used by Homassy.API.

**Connection:** Provided at runtime via the `DSN` environment variable:
```
postgres://user:password@host:5432/homassy
```

Kratos manages its own migrations automatically on startup via `kratos migrate sql`.

---

## Security

### Secrets

```yaml
secrets:
  cookie:
    - <cookie-signing-secret>    # Signs session cookies (HMAC)
  cipher:
    - <32-character-cipher-key>  # Encrypts sensitive data at rest
```

- **Cookie secrets** sign session cookies (HMAC). Rotating them invalidates all existing sessions.
- **Cipher secrets** encrypt sensitive identity data at rest using XChaCha20-Poly1305.
- In production, injected via `SECRETS_COOKIE` and `SECRETS_CIPHER` environment variables.

### Cipher Algorithm

```yaml
ciphers:
  algorithm: xchacha20-poly1305
```

### Password Hashing (Argon2)

Used internally by Kratos for credential data. Production uses stronger parameters:

| Setting | Development | Production |
|---------|-------------|------------|
| `parallelism` | 1 | 2 |
| `memory` | 128 MB | 256 MB |
| `iterations` | 2 | 3 |
| `salt_length` | 16 | 16 |
| `key_length` | 16 | 32 |

---

## Config Files

### `kratos.yml` – Base (Development Defaults)

The primary config file used in development. Contains:
- Development URLs (`localhost:3000`, `localhost:4433`, `localhost:4434`)
- Debug-level logging with `leak_sensitive_values: false`
- Default lifespans (15m codes, 720h sessions, 10m flows)
- Courier HTTP webhook to `http://homassy-email:8080/kratos/webhook`

### `kratos.development.yml` – Development Overrides

Merged on top of `kratos.yml` for development. Overrides:
- Log level: `debug`, format: `text`, `leak_sensitive_values: true`
- Longer flow lifespans: login/registration 30m, recovery/verification 2h
- Code lifespan: 30m
- Session lifespan: 168h (7 days)

### `kratos.production.yml` – Production Config

A complete standalone production config (not merged with `kratos.yml`). Key differences:
- Production URLs (`https://homassy.kellner.dev`, `https://homassy-kratos.kellner.dev`)
- Log level: `warning`, format: `json`
- Cookie domain: `.kellner.dev` (subdomain wildcard)
- Stronger Argon2 settings
- Many values overridable via environment variables (documented inline with comments)

### Environment Variable Overrides (Production)

| Env var | What it overrides |
|---------|-------------------|
| `DSN` | Database connection string |
| `SERVE_PUBLIC_BASE_URL` | Kratos public API URL |
| `SERVE_PUBLIC_CORS_ALLOWED_ORIGINS_0` | CORS allowed origin |
| `SELFSERVICE_DEFAULT_BROWSER_RETURN_URL` | Default redirect after flows |
| `SELFSERVICE_METHODS_WEBAUTHN_CONFIG_RP_ID` | WebAuthn relying party ID |
| `SECRETS_COOKIE` | Cookie signing secret |
| `SECRETS_CIPHER` | Encryption cipher key |
| `SESSION_COOKIE_DOMAIN` | Session cookie domain |
| `COOKIES_DOMAIN` | General cookie domain |

---

## Integration with Homassy

### Ports

| Port | Service |
|------|---------|
| `4433` | Kratos Public API (used by Homassy.Web and Homassy.API) |
| `4434` | Kratos Admin API (used only by Homassy.API internally) |

### Homassy.API

1. **Session validation** – `KratosSessionMiddleware` calls `GET http://kratos:4433/sessions/whoami` with the session token from the `X-Session-Token` header or `ory_kratos_session` cookie. The result is stored in `HttpContext.Items["KratosSession"]` and accessed via `HttpContext.GetKratosSession()`.

2. **Identity management** – `KratosAdminService` calls the Admin API (`http://kratos:4434`) to create, update, and delete identities, and to invalidate sessions.

3. **Registration control** – `RegistrationEnabled` flag in `appsettings.json`. When `false`, registration requests are rejected at the API level independently of Kratos config.

### Homassy.Web

The frontend calls the Kratos Public API directly for all auth flows (login, registration, recovery, verification). It never calls Homassy.API for authentication – only for application data after a valid session is established.

### Homassy.Email

```
Kratos Courier
  → POST http://homassy-email:8080/kratos/webhook
  → X-Api-Key: <api key matching Email:ApiKey config>
  → Body: rendered by webhook_body.jsonnet
  → Homassy.Email enqueues and delivers the email
```

---

## Environment Differences

| Setting | Development | Production |
|---------|-------------|------------|
| Public base URL | `http://localhost:4433` | `https://homassy-kratos.kellner.dev` |
| Frontend URL | `http://localhost:3000` | `https://homassy.kellner.dev` |
| WebAuthn RP ID | `localhost` | `kellner.dev` |
| Cookie domain | `localhost` | `.kellner.dev` |
| Log level | `debug` (text) | `warning` (json) |
| Leak sensitive values | `true` | `false` |
| Session lifespan | 168h (7 days) | 720h (30 days) |
| Code lifespan | 30m | 15m |
| Login / Registration flow | 30m | 10m |
| Recovery / Verification flow | 2h | 1h |
| Argon2 memory | 128 MB | 256 MB |
| Argon2 key length | 16 | 32 |
