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
- **JWT-based authentication** with passwordless email verification
- **In-memory caching** with database trigger-based invalidation
- **Controller → Functions** pattern (no traditional repository layer)

**Frontend (Planned):**
- **Vue.js 3** with Nuxt framework
- **Nuxt UI** component library
- **Progressive Web App (PWA)** capabilities

### 🔐 Security
- 🔑 Passwordless authentication (6-digit email codes)
- 🎫 JWT access and refresh tokens
- 🚦 Two-tier rate limiting (global + endpoint-specific)
- 🛡️ Comprehensive security headers (CSP, HSTS, X-Frame-Options, etc.)
- ⏱️ Timing attack protection with constant-time comparisons

### 🎯 Functionality
- 👤 **User Management** - Profiles, settings, profile pictures
- 👨‍👩‍👧‍👦 **Family Management** - Create families, join via invite codes
- 📦 **Product Management** - Track products and storage locations
- 🛒 **Shopping Lists** - Collaborative family shopping lists
- 📍 **Locations** - Manage stores and storage locations

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
│   ├── Middleware/       🔧 Rate limiting, session info
│   └── CLAUDE.md         📚 Detailed architecture documentation
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
| **Authentication** | JWT Bearer Tokens |
| **Email** | MailKit 4.14.1 |
| **Logging** | Serilog 9.0.0 |
| **API Versioning** | Asp.Versioning 8.1.0 |

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
| **License** | MIT |

## 🚀 Getting Started

> **Note:** Currently, only the backend API is available for setup. Frontend setup instructions will be added once development begins.

### Prerequisites
- ✅ .NET 10 SDK
- ✅ PostgreSQL 14+
- ✅ SMTP server (for email delivery)

### Backend Installation

**1. Clone the repository**
```bash
git clone https://github.com/Xentinus/Homassy.git
cd Homassy
```

**2. Configure database connection**

Create an `appsettings.Development.json` file in the `Homassy.API` folder:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=homassy;Username=postgres;Password=yourpassword"
  },
  "Jwt": {
    "SecretKey": "your-secret-key-min-32-characters",
    "Issuer": "HomassyAPI",
    "Audience": "HomassyClient",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 30
  },
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "your-email@gmail.com",
    "FromName": "Homassy"
  }
}
```

**3. Run database migrations**
```bash
cd Homassy.API
dotnet ef database update
```

**4. Start the application**
```bash
dotnet run
```

The API will be available at: `https://localhost:5001` 🎉

### 📚 API Documentation

In development mode, OpenAPI (Swagger) documentation is available at `/openapi/v1.json`

## 🔌 API Examples

### Authentication Flow

**1. Request verification code**
```bash
POST /api/v1.0/auth/request-code
{
  "email": "user@example.com"
}
```

**2. Verify code and login**
```bash
POST /api/v1.0/auth/verify-code
{
  "email": "user@example.com",
  "verificationCode": "123456"
}
```

**3. Use the token**
```bash
GET /api/v1.0/auth/me
Authorization: Bearer <access-token>
```

### Response Format

All API responses follow a standardized format:

```json
{
  "success": true,
  "data": { ... },
  "message": "Operation successful",
  "errors": null,
  "timestamp": "2025-12-02T10:30:00Z"
}
```

## 🧑‍💻 Development Guidelines

Detailed architecture, patterns, and development guidelines are available in [Homassy.API/CLAUDE.md](Homassy.API/CLAUDE.md).

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

MIT License - see [LICENSE.txt](LICENSE.txt)

Copyright (c) 2025 Béla Kellner

## 📧 Contact

GitHub: [@Xentinus](https://github.com/Xentinus)

---

## 🗺️ Roadmap

### ✅ Phase 1: Backend API (Current)
- [x] Core architecture setup
- [x] Authentication system (passwordless)
- [x] User management
- [x] Family management
- [ ] Product management (in progress)
- [ ] Shopping list features
- [ ] Location management
- [ ] API documentation finalization

### 📋 Phase 2: Frontend Web App (Planned)
- [ ] Nuxt + Vue.js 3 setup
- [ ] Nuxt UI integration
- [ ] Authentication UI
- [ ] User profile management
- [ ] Family dashboard
- [ ] Product inventory UI
- [ ] Shopping list interface
- [ ] PWA capabilities

### 🚀 Phase 3: Deployment & Production (Future)
- [ ] CI/CD pipeline
- [ ] Docker containerization
- [ ] Production deployment
- [ ] Mobile app consideration

---

⚠️ **Note:** This project is currently under active development. The backend API is being built first, followed by the frontend web application.
