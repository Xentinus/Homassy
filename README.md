# Homassy

**Otthoni tárolás kezelő rendszer** - Home Storage Management System

## Áttekintés

A Homassy egy modern, ASP.NET Core alapú backend rendszer, amely otthoni készletek, bevásárlólisták és termékek kezelését könnyíti meg családok számára.

## Főbb jellemzők

### Architektúra
- **.NET 10.0** Web API
- **PostgreSQL** adatbázis Entity Framework Core ORM-mel
- **JWT alapú hitelesítés** jelszó nélküli email-kódos bejelentkezéssel
- **In-memory cache** adatbázis trigger alapú érvénytelenítéssel
- **Controller → Functions** minta (hagyományos repository minta nélkül)

### Biztonság
- Jelszó nélküli hitelesítés (6 számjegyű email kód)
- JWT access és refresh tokenek
- Kétszintű rate limiting (globális + végpont specifikus)
- Átfogó biztonsági headerek (CSP, HSTS, X-Frame-Options, stb.)
- Timing attack védelem

### Funkcionalitás
- Felhasználó kezelés (profil, beállítások, profilkép)
- Család kezelés (családlétrehozás, csatlakozás, meghívókód)
- Termék kezelés és tárolás
- Bevásárlólista kezelés
- Helyszínek (boltok, tárolók) kezelése

## Projekt struktúra

```
Homassy/
├── Homassy.API/          ASP.NET Core Web API projekt
│   ├── Controllers/      HTTP végpontok (vékony réteg)
│   ├── Functions/        Üzleti logika + adatelérés
│   ├── Entities/         Adatbázis modellek (Entity Framework)
│   ├── Models/           DTO-k és request/response objektumok
│   ├── Context/          DbContext és session kezelés
│   ├── Services/         Infrastruktúra szolgáltatások
│   ├── Middleware/       Rate limiting, session info
│   └── CLAUDE.md         Részletes angol nyelvű dokumentáció
└── README.md             Ez a fájl
```

## Technológiai stack

| Kategória | Technológia |
|-----------|-------------|
| **Backend** | ASP.NET Core 10.0 |
| **Adatbázis** | PostgreSQL + EF Core 10.0 |
| **Hitelesítés** | JWT Bearer Tokens |
| **Email** | MailKit 4.14.1 |
| **Logging** | Serilog 9.0.0 |
| **API Versioning** | Asp.Versioning 8.1.0 |
| **Licenc** | MIT |

## Kezdés

### Előfeltételek
- .NET 10 SDK
- PostgreSQL 14+
- SMTP szerver (email küldéshez)

### Telepítés

1. **Repository klónozása**
   ```bash
   git clone https://github.com/Xentinus/Homassy.git
   cd Homassy
   ```

2. **Adatbázis kapcsolat beállítása**

   Hozz létre egy `appsettings.Development.json` fájlt a `Homassy.API` mappában:
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

3. **Adatbázis migrációk futtatása**
   ```bash
   cd Homassy.API
   dotnet ef database update
   ```

4. **Alkalmazás indítása**
   ```bash
   dotnet run
   ```

Az API elérhető lesz: `https://localhost:5001`

### API dokumentáció

Development módban az OpenAPI (Swagger) dokumentáció elérhető a `/openapi/v1.json` végponton.

## API példák

### Bejelentkezés

**1. Kérj verifikációs kódot**
```bash
POST /api/v1.0/auth/request-code
{
  "email": "user@example.com"
}
```

**2. Ellenőrizd a kódot és jelentkezz be**
```bash
POST /api/v1.0/auth/verify-code
{
  "email": "user@example.com",
  "verificationCode": "123456"
}
```

**3. Használd a tokent**
```bash
GET /api/v1.0/auth/me
Authorization: Bearer <access-token>
```

### Minden válasz formátuma

```json
{
  "success": true,
  "data": { ... },
  "message": "Művelet sikeres",
  "errors": null,
  "timestamp": "2025-12-02T10:30:00Z"
}
```

## Fejlesztési irányelvek

A részletes architektúra, minták és fejlesztési útmutatók a [Homassy.API/CLAUDE.md](Homassy.API/CLAUDE.md) fájlban találhatók angol nyelven.

### Alapelvek
- **Vékony controllerek**: Csak HTTP kezelés, validáció, response formázás
- **Functions osztályok**: Teljes üzleti logika és adatelérés
- **Cache-first**: In-memory cache előnyben részesítése
- **Soft delete**: Minden entitás támogatja a soft delete-et
- **Standardizált válaszok**: Minden végpont `ApiResponse<T>` formátumot használ

## Entitás öröklési hierarchia

```
BaseEntity (Id, PublicId)
  └── SoftDeleteEntity (IsDeleted)
      └── RecordChangeEntity (RecordChange JSON)
          └── User, Family, Product, ShoppingList, Location, ...
```

## Licensz

MIT License - lásd [LICENSE.txt](LICENSE.txt)

## Kapcsolat

GitHub: [@Xentinus](https://github.com/Xentinus)

---

**Megjegyzés:** A projekt jelenleg fejlesztés alatt áll.
