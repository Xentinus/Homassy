<div align="center">
  <img src=".github/HomassyLogo.svg" alt="Homassy Logo" width="200"/>
  <h1>Homassy</h1>
</div>

> **Home Storage Management System** - A modern full-stack application for managing household inventory, shopping lists, and family organization.

## 📖 Overview

Homassy is a modern full-stack system designed to simplify household inventory management, shopping lists, and product tracking for families. The system consists of a high-performance ASP.NET Core backend API and a modern Vue.js 3 (Nuxt 4) web application frontend.

## ✨ Key Features

### 🏗️ Architecture

**Backend:**
- **.NET 10.0** Web API with modern C# patterns
- **PostgreSQL** database with Entity Framework Core
- **JWT-based authentication** with passwordless email verification and token rotation
- **In-memory caching** with database trigger-based invalidation
- **Controller → Functions** pattern (no traditional repository layer)
- **Production-ready middleware** - Exception handling, CORS, compression, logging
- **Response compression** - Brotli and Gzip for optimized bandwidth
- **Request correlation** - X-Correlation-ID tracking across all requests
- **Background services** - Email queue, token cleanup with automated maintenance
- **Health monitoring** - Endpoint health checks with dependency monitoring
- **Full async cancellation** - CancellationToken support across all operations

**Frontend:**
- **Vue.js 3.5.25** with **Nuxt 4.2.2** framework
- **@nuxt/ui 4.3.0** - Modern component library built on Radix Vue
- **Pinia** state management for reactive data flow
- **TypeScript 5.9.3** - Full type safety across the application
- **@nuxtjs/i18n** - Multi-language support (English, German, Hungarian)
- **Progressive Web App (PWA)** - Web manifest with standalone mode
- **Responsive design** - Mobile-first approach with touch optimization
- **SSR & Client-side rendering** - Optimized performance with Nuxt 4
- **Camera Integration** - Camera availability detection and barcode/QR Code scanning support

### 🔐 Security
- 🔑 Passwordless authentication (6-digit email codes)
- 🎫 JWT access and refresh tokens with rotation and theft detection
- 🔄 Refresh token rotation with grace period for improved security
- 🚦 Two-tier rate limiting (global + endpoint-specific) with standard headers
- 🛡️ Comprehensive security headers (CSP, HSTS, X-Frame-Options, etc.)
- 🧹 Input sanitization with automatic XSS attack prevention
- 🖼️ Secure image upload with magic number validation and integrity checks
- 🌐 CORS support with configurable allowed origins
- ⏱️ Timing attack protection with constant-time comparisons
- ⏳ Request timeout protection with per-endpoint configuration
- 🛑 Global exception handling with standardized error responses

### 🎯 Functionality
- 👤 **User Management** - Profiles, settings, profile pictures, activity feed
- 👨‍👩‍👧‍👦 **Family Management** - Create families, join via invite codes, family pictures
- 📦 **Product Management** - Complete product inventory with consumption tracking and images
- 🛒 **Shopping Lists** - Collaborative family shopping lists with purchase tracking
- 📍 **Locations** - Shopping locations (stores) and storage locations (home/freezer)
- 🔍 **Product Lookup** - Barcode scanning via Open Food Facts API integration
- 📊 **Select Values** - Dynamic dropdown options for forms
- 📱 **Barcode Scanning** - Camera-based scanning with multi-format support, camera availability detection
- 🔎 **Search Highlighting** - Visual highlighting of search results across product and location components
- 🖼️ **Image Processing** - Browser-side compression and cropping
- 🌍 **Internationalization** - Full i18n support with 3 languages (English, German, Hungarian)
- 🔔 **Notifications** - User notification preferences

### 📊 Data Quality
- ✅ Advanced barcode validation with checksum verification (EAN-13, EAN-8, UPC-A, UPC-E, Code-128)
- 🔍 Automatic format detection and validation at API boundary
- 🌍 International barcode standard support (European and North American formats)
- 📸 Image validation with format detection (JPEG, PNG, WebP) and dimension constraints

## 📁 Project Structure

```
Homassy/
├── Homassy.API/          🎯 ASP.NET Core Web API (Backend)
│   ├── Controllers/      🌐 HTTP endpoints (thin layer)
│   ├── Functions/        💼 Business logic + data access
│   ├── Entities/         🗄️ Database models (Entity Framework)
│   ├── Models/           📋 DTOs and request/response objects
│   ├── Context/          🔄 DbContext and session management
│   ├── Services/         ⚙️ Infrastructure services
│   ├── Middleware/       🔧 Exception handling, CORS, compression, logging, rate limiting
│   └── CLAUDE.md         📚 Detailed architecture documentation
├── Homassy.Web/          🎨 Vue.js 3 + Nuxt 4 Web App (Frontend)
│   ├── app/
│   │   ├── pages/        🔖 File-based routing (15+ pages)
│   │   ├── components/   🧩 Reusable Vue components (12 components)
│   │   ├── composables/  🎣 Composition API helpers
│   │   │   └── api/      📡 API client wrappers (11 services)
│   │   ├── stores/       🗃️ Pinia state management (auth)
│   │   ├── layouts/      📐 Page layouts (auth, public)
│   │   ├── middleware/   🛡️ Route guards (auth protection)
│   │   ├── types/        📝 TypeScript definitions (14 type files)
│   │   └── utils/        🔧 Utility functions
│   ├── i18n/             🌍 Translation files (en, de, hu)
│   ├── public/           📂 Static assets and PWA icons
│   ├── nuxt.config.ts    ⚙️ Nuxt configuration
│   └── Dockerfile        🐳 Multi-stage Docker build
├── Homassy.Migrator/     🔄 Database Migration Tool
│   ├── Program.cs
│   └── Dockerfile
├── Homassy.Tests/        🧪 Test Suite (xUnit)
│   ├── Integration/      ✅ Integration tests (100+ tests)
│   ├── Unit/             🔬 Unit tests
│   ├── Infrastructure/   🛠️ Test helpers and utilities
│   └── CLAUDE.md         📖 Testing documentation
└── README.md             📖 This file
```

## 🛠️ Technology Stack

### Backend
| Category | Technology |
|----------|------------|
| **Framework** | ASP.NET Core 10.0 |
| **Database** | PostgreSQL + EF Core 10.0 |
| **Authentication** | JWT Bearer Tokens (with rotation) |
| **Email** | MailKit 4.14.1 (async queue) |
| **Logging** | Serilog 9.0.0 (structured) |
| **API Versioning** | Asp.Versioning 8.1.0 |
| **Health Checks** | Microsoft.Extensions.Diagnostics.HealthChecks |
| **Compression** | Brotli + Gzip (built-in) |
| **Background Services** | IHostedService (email queue, token cleanup) |
| **Testing** | xUnit 2.9.3 + WebApplicationFactory |
| **External APIs** | Open Food Facts API v2 |

### Frontend
| Category | Technology |
|----------|------------|
| **Framework** | Vue.js 3.5.25 + Nuxt 4.2.2 |
| **UI Library** | @nuxt/ui 4.3.0 (Radix Vue) |
| **Type Safety** | TypeScript 5.9.3 |
| **State Management** | Pinia (@pinia/nuxt 0.11.3) |
| **Internationalization** | @nuxtjs/i18n 10.2.1 (en, de, hu) |
| **API Client** | nuxt-api-party 3.4.2 |
| **Image Processing** | vue-advanced-cropper 2.8.9 |
| **Barcode Scanning** | vue-qrcode-reader 5.7.3 |
| **Icons** | @iconify (Heroicons, Lucide) |
| **Date/Time** | @internationalized/date 3.10.1 |
| **Image Compression** | browser-image-compression 2.0.2 |
| **Runtime** | Node.js 22-alpine |

### Infrastructure
| Category | Technology |
|----------|------------|
| **Containerization** | Docker + Docker Compose |
| **Database** | PostgreSQL 16 |
| **Web Server** | Kestrel (ASP.NET Core) |
| **Node Server** | Node.js 22 (Nuxt SSR) |

## 🌳 Entity Inheritance Hierarchy

```
BaseEntity (Id, PublicId)
  └── SoftDeleteEntity (IsDeleted)
      └── RecordChangeEntity (RecordChange JSON)
          └── User, Family, Product, ShoppingList, Location, ...
```

All entities inherit a common base with:
- 🔢 Internal integer ID
- 🆔 Public GUID identifier (prevents ID enumeration)
- 🗑️ Soft delete support
- 📝 Automatic change tracking

## 📄 License

AGPL-3.0 License - see [LICENSE.txt](LICENSE.txt)

Copyright (c) 2025 Béla Kellner