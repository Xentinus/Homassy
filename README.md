# 🏠 Homassy

> **Home Storage Management System** - A modern full-stack application for managing household inventory, shopping lists, and family organization.

## 📖 Overview

Homassy is a modern full-stack system designed to simplify household inventory management, shopping lists, and product tracking for families. The system consists of a high-performance ASP.NET Core backend API and will include a Vue.js (Nuxt UI) web application frontend.

**Current Status:** 🚧 Backend API is currently under active development. Frontend development will begin once the API is complete.

## ✨ Key Features

### 🏗️ Architecture

**Backend (Current Focus):**
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

**Frontend (Planned):**
- **Vue.js 3** with Nuxt framework
- **Nuxt UI** component library
- **Progressive Web App (PWA)** capabilities

### 🔐 Security
- 🔑 Passwordless authentication (6-digit email codes)
- 🎫 JWT access and refresh tokens with rotation and theft detection
- 🔄 Refresh token rotation with grace period for improved security
- 🚦 Two-tier rate limiting (global + endpoint-specific) with standard headers
- 🛡️ Comprehensive security headers (CSP, HSTS, X-Frame-Options, etc.)
- 🧹 Input sanitization with automatic XSS attack prevention
- 🌐 CORS support with configurable allowed origins
- ⏱️ Timing attack protection with constant-time comparisons
- ⏳ Request timeout protection with per-endpoint configuration
- 🛑 Global exception handling with standardized error responses

### 🎯 Functionality
- 👤 **User Management** - Profiles, settings, profile pictures
- 👨‍👩‍👧‍👦 **Family Management** - Create families, join via invite codes
- 📦 **Product Management** - Complete product inventory with consumption tracking
- 🛒 **Shopping Lists** - Collaborative family shopping lists with purchase tracking
- 📍 **Locations** - Shopping locations (stores) and storage locations (home)
- 🔍 **Product Lookup** - Barcode scanning via Open Food Facts API integration
- 📊 **Select Values** - Dynamic dropdown options for forms

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
├── Homassy.Tests/        🧪 Test Suite (xUnit)
│   ├── Integration/      ✅ Integration tests (100+ tests)
│   ├── Unit/             🔬 Unit tests
│   ├── Infrastructure/   🛠️ Test helpers and utilities
│   └── CLAUDE.md         📖 Testing documentation
├── Homassy.Web/          🎨 Vue.js Web App (Frontend - Planned)
│   └── (Coming soon)
└── README.md             📖 This file
```

## 🛠️ Technology Stack

### Backend (Current)
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

### Frontend (Planned)
| Category | Technology |
|----------|------------|
| **Framework** | Vue.js 3 + Nuxt |
| **UI Library** | Nuxt UI |
| **Type Safety** | TypeScript |
| **State Management** | Pinia (planned) |

### General
| Category | Value |
|----------|-------|
| **License** | AGPL-3.0 |

## 📚 API Documentation

In development mode, OpenAPI (Swagger) documentation is available at `/openapi/v1.json`

## 🔌 Available Endpoints

**Authentication & User**
- `POST /api/v1.0/auth/request-code` - Request verification code
- `POST /api/v1.0/auth/verify-code` - Verify code and login
- `POST /api/v1.0/auth/refresh` - Refresh access token
- `POST /api/v1.0/auth/logout` - Logout
- `GET /api/v1.0/auth/me` - Get current user info
- `POST /api/v1.0/auth/register` - Register new user

**Products & Inventory**
- `GET /api/v1.0/product` - Get all products
- `POST /api/v1.0/product` - Create product
- `POST /api/v1.0/product/multiple` - Create multiple products
- `GET /api/v1.0/product/{publicId}` - Get detailed product with inventory
- `PUT /api/v1.0/product/{publicId}` - Update product
- `DELETE /api/v1.0/product/{publicId}` - Delete product
- `POST /api/v1.0/product/{publicId}/favorite` - Toggle favorite
- `POST /api/v1.0/product/{publicId}/inventory` - Add inventory item
- `POST /api/v1.0/product/inventory/quick/multiple` - Quick add multiple inventory items
- `POST /api/v1.0/product/inventory/move` - Move inventory items between storage locations
- `DELETE /api/v1.0/product/inventory/multiple` - Delete multiple inventory items
- `POST /api/v1.0/product/{publicId}/inventory/{itemId}/consume` - Mark as consumed
- `POST /api/v1.0/product/inventory/consume/multiple` - Consume multiple inventory items

**Open Food Facts Integration**
- `GET /api/v1.0/openfoodfacts/{barcode}` - Look up product by barcode

**Shopping Lists**
- `GET /api/v1.0/shoppinglist` - Get all shopping lists
- `POST /api/v1.0/shoppinglist` - Create shopping list
- `GET /api/v1.0/shoppinglist/{publicId}` - Get detailed list with items
- `PUT /api/v1.0/shoppinglist/{publicId}` - Update shopping list
- `DELETE /api/v1.0/shoppinglist/{publicId}` - Delete shopping list
- `POST /api/v1.0/shoppinglist/{listId}/item` - Add item to list
- `POST /api/v1.0/shoppinglist/item/multiple` - Create multiple shopping list items
- `PUT /api/v1.0/shoppinglist/{listId}/item/{itemId}` - Update item
- `DELETE /api/v1.0/shoppinglist/{listId}/item/{itemId}` - Delete item
- `DELETE /api/v1.0/shoppinglist/item/multiple` - Delete multiple shopping list items
- `POST /api/v1.0/shoppinglist/item/quick-purchase` - Quick purchase item (creates inventory)
- `POST /api/v1.0/shoppinglist/item/quick-purchase/multiple` - Quick purchase multiple items

**Locations**
- `GET /api/v1.0/location/shopping` - Get shopping locations (stores)
- `POST /api/v1.0/location/shopping` - Create shopping location
- `POST /api/v1.0/location/shopping/multiple` - Create multiple shopping locations
- `PUT /api/v1.0/location/shopping/{publicId}` - Update shopping location
- `DELETE /api/v1.0/location/shopping/{publicId}` - Delete shopping location
- `DELETE /api/v1.0/location/shopping/multiple` - Delete multiple shopping locations
- `GET /api/v1.0/location/storage` - Get storage locations
- `POST /api/v1.0/location/storage` - Create storage location
- `POST /api/v1.0/location/storage/multiple` - Create multiple storage locations
- `PUT /api/v1.0/location/storage/{publicId}` - Update storage location
- `DELETE /api/v1.0/location/storage/{publicId}` - Delete storage location
- `DELETE /api/v1.0/location/storage/multiple` - Delete multiple storage locations

**Select Values (Dynamic Dropdowns)**
- `GET /api/v1.0/selectvalue/{type}` - Get select options for entity type
  - Types: `ShoppingLocation`, `StorageLocation`, `Product`, `ProductInventoryItem`, `ShoppingList`

**Version**
- `GET /api/Version` - Get API version information (build version, type, and date)

**Health Checks**
- `GET /health` - Overall health status with dependency checks
- `GET /health/ready` - Readiness check (database, cache, dependencies)
- `GET /health/live` - Liveness check (basic API availability)

**Family Management**
- `GET /api/v1.0/family` - Get family info
- `POST /api/v1.0/family` - Create family
- `POST /api/v1.0/family/join` - Join family with invite code
- `POST /api/v1.0/family/leave` - Leave current family

## 🧑‍💻 Development Guidelines

Detailed architecture, patterns, and development guidelines are available in [Homassy.API/CLAUDE.md](Homassy.API/CLAUDE.md).

> **Note:** The CLAUDE.md documentation is Claude-generated for Claude developers and is occasionally updated by the project maintainer.

### Core Principles
- 🎯 **Thin controllers**: Only HTTP handling, validation, and response formatting
- 💼 **Functions classes**: Complete business logic and data access
- 🚀 **Cache-first**: Prefer in-memory cache over database queries
- 🗑️ **Soft delete**: All entities support soft deletion
- 📋 **Standardized responses**: Every endpoint returns `ApiResponse<T>` format

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

AGPL-3.0 License - see [LICENSE.txt](LICENSE.txt) and [COPYING.TXT](COPYING.TXT)

Copyright (c) 2025 Béla Kellner

## 📧 Contact

GitHub: [@Xentinus](https://github.com/Xentinus)

---

⚠️ **Note:** This project is currently under active development. The backend API is being built first, followed by the frontend web application.
