# Homassy.Tests - Test Project Architecture Documentation

> **Note:** This is a living document that is updated as the project evolves.
> It is not versioned - changes are made directly to reflect the current state of the project.

## Table of Contents

1. [Overview](#overview)
2. [Technology Stack](#technology-stack)
3. [Project Structure](#project-structure)
4. [Test Categories](#test-categories)
5. [Infrastructure Components](#infrastructure-components)
6. [Test Patterns & Conventions](#test-patterns--conventions)
7. [Configuration](#configuration)
8. [Running Tests](#running-tests)
9. [Development Guidelines](#development-guidelines)

---

## Overview

Homassy.Tests is the test project for the Homassy.API application. It provides comprehensive test coverage through both **Unit Tests** and **Integration Tests**. The project uses **xUnit** as the testing framework and follows a structured approach to testing with shared infrastructure components.

### Key Architectural Decisions

- **xUnit Framework** – Modern, extensible testing framework with excellent async support
- **Mock Kratos Service** – `IKratosService` is replaced with `MockKratosService` (in-memory simulation); no real Kratos instance is needed for tests
- **Direct DB User Creation** – Integration test users are created directly in the database (not via HTTP registration API), bypassing the full Kratos flow
- **Real PostgreSQL** – Integration tests use an actual PostgreSQL database, not an in-memory substitute
- **WebApplicationFactory** – ASP.NET Core's built-in integration testing infrastructure
- **IClassFixture Pattern** – Shared factory instance across all tests in a class for efficiency
- **`try/finally` Cleanup** – Every integration test cleans up its own data

---

## Technology Stack

- **.NET 10.0**
- **xUnit 2.9.3** – Testing framework
- **xUnit.runner.visualstudio 3.1.0** – Visual Studio test adapter
- **Microsoft.AspNetCore.Mvc.Testing 10.0.0** – `WebApplicationFactory` support
- **Microsoft.NET.Test.Sdk 17.14.1** – Test SDK
- **coverlet.collector 6.0.4** – Code coverage collection

---

## Project Structure

```
Homassy.Tests/
├── Infrastructure/
│   ├── HomassyWebApplicationFactory.cs   WebApplicationFactory + MockKratosService
│   └── TestAuthHelper.cs                 Creates/authenticates test users
├── Integration/
│   ├── AccountLockoutIntegrationTests.cs  Stub (lockout delegated to Kratos)
│   ├── AuthControllerTests.cs             Auth endpoint tests
│   ├── FamilyControllerTests.cs           Family CRUD tests
│   ├── GracefulShutdownTests.cs           Shutdown signal tests
│   ├── HealthControllerTests.cs           Health check endpoint tests
│   ├── HttpsRedirectTests.cs              HTTPS redirect middleware tests
│   ├── LocationControllerPaginationTests.cs  Pagination tests for locations
│   ├── LocationControllerTests.cs            Location CRUD tests
│   ├── OpenFoodFactsControllerTests.cs       OpenFoodFacts proxy tests
│   ├── ProductControllerPaginationTests.cs   Pagination tests for products
│   ├── ProductControllerTests.cs             Product CRUD tests
│   ├── SelectValueControllerTests.cs         Select value endpoint tests
│   ├── ShoppingListControllerPaginationTests.cs  Pagination tests
│   ├── ShoppingListControllerTests.cs            Shopping list tests
│   └── UserControllerTests.cs                    User profile tests
├── Unit/
│   ├── AccountLockoutServiceTests.cs         AccountLockoutService logic
│   ├── BarcodeValidationServiceTests.cs      Barcode validation
│   ├── CancellationTokenTests.cs             CancellationToken propagation
│   ├── CorrelationIdMiddlewareTests.cs       Correlation ID middleware
│   ├── GlobalExceptionMiddlewareTests.cs     Exception → ApiResponse mapping
│   ├── ImageProcessingServiceTests.cs        Image resize/processing
│   ├── InputSanitizationServiceTests.cs      Input sanitization
│   ├── OpenFoodFactsHealthCheckTests.cs      OpenFoodFacts health check
│   ├── RateLimitServiceTests.cs              Rate limit logic
│   ├── RateLimitingMiddlewareTests.cs        Rate limiting middleware
│   ├── RefreshTokenRotationTests.cs          Token cleanup logic
│   ├── RequestLoggingMiddlewareTests.cs      Request logging middleware
│   ├── RequestTimeoutMiddlewareTests.cs      Timeout middleware
│   ├── SanitizedStringAttributeTests.cs      [SanitizedString] attribute
│   ├── ShoppingListActivityMonitorServiceTests.cs  Activity monitor service
│   ├── TimeZoneFunctionsTests.cs             Timezone conversion
│   ├── TokenCleanupServiceTests.cs           Token cleanup service
│   ├── UnitFunctionsTests.cs                 Measurement unit conversions
│   ├── UserFunctionsTests.cs                 User business logic
│   └── ValidBarcodeAttributeTests.cs         [ValidBarcode] attribute
├── appsettings.Testing.json    Test configuration (not in source control)
└── Homassy.Tests.csproj
```

---

## Test Categories

### Unit Tests (`/Unit`)

Test individual classes and methods in isolation. Most do **not** require `HomassyWebApplicationFactory` – they instantiate the class under test directly.

**Characteristics:**
- No HTTP requests
- Instantiate services/middleware directly with test dependencies
- Fast execution
- `IDisposable` for cleanup where static state is involved (e.g. `RateLimitService`)

**Coverage includes:**
- Business logic (`UserFunctions`, `TimeZoneFunctions`, `UnitFunctions`)
- Services (`AccountLockoutService`, `RateLimitService`, `InputSanitizationService`, `BarcodeValidationService`, `ImageProcessingService`)
- Middleware (`GlobalExceptionMiddleware`, `RateLimitingMiddleware`, `CorrelationIdMiddleware`, `RequestLoggingMiddleware`, `RequestTimeoutMiddleware`)
- Validation attributes (`[SanitizedString]`, `[ValidBarcode]`)
- Background services (`TokenCleanupService`, `ShoppingListActivityMonitorService`)
- Health checks (`OpenFoodFactsHealthCheck`)

### Integration Tests (`/Integration`)

Verify complete HTTP request/response cycles against a real PostgreSQL database, with Kratos mocked.

**Characteristics:**
- Full HTTP layer tested via `HttpClient`
- Real PostgreSQL database (no in-memory substitute)
- Kratos replaced by `MockKratosService` (no real Kratos needed)
- Test users created directly in DB, not via API registration
- Authentication via `X-Session-Token: mock-session-{kratosIdentityId}`

**Coverage includes:**
- All controller endpoints (CRUD, pagination, validation, auth checks)
- Health endpoints
- HTTPS redirect behavior
- Graceful shutdown

---

## Infrastructure Components

### MockKratosService

An in-memory implementation of `IKratosService` that replaces the real Kratos client for all tests. Registered as a singleton in `HomassyWebApplicationFactory`, it stores sessions in a `ConcurrentDictionary<string, KratosSession>`.

```csharp
public class MockKratosService : IKratosService
{
    private readonly ConcurrentDictionary<string, KratosSession> _testSessions = new();

    public void RegisterSession(string sessionToken, KratosSession session) { ... }
    public void ClearSessions() { ... }

    // IKratosService methods:
    public Task<KratosSession?> GetSessionAsync(string? cookie, string? sessionToken, ...) { ... }
    public Task<KratosIdentity?> GetIdentityAsync(string identityId, ...) { ... }
    public Task<KratosIdentity?> UpdateIdentityTraitsAsync(string identityId, KratosTraits traits, ...) { ... }
    public Task<bool> DeleteIdentitySessionsAsync(string identityId, ...) { ... }
    public Task<KratosIdentity?> CreateIdentityAsync(KratosTraits traits, bool verifyEmail, ...) { ... }
    // ...
}
```

**Session lookup logic:**
- Exact match on token key
- Prefix match for `mock-session-{identityId}` tokens

### HomassyWebApplicationFactory

Custom `WebApplicationFactory<Program>` that configures the test environment:

**What it does:**
1. Clears default configuration sources and loads:
   - `Homassy.API/appsettings.json` (base)
   - `Homassy.Tests/appsettings.Testing.json` (overrides)
   - Environment variables
2. Replaces `IKratosService` with `MockKratosService` (singleton)
3. Sets `ASPNETCORE_ENVIRONMENT` to `"Testing"`
4. Initializes `HomassyDbContext.SetConfiguration()` and `ConfigService.Initialize()` before host creation

**Helper methods:**

| Method | Description |
|--------|-------------|
| `RegisterTestSession(kratosId, email, displayName, userId)` | Registers a mock Kratos session for an existing DB user |
| `GetUserIdByEmail(email)` | Returns internal `User.Id` by email |
| `CreateScopedDbContext()` | Returns `(IServiceScope, HomassyDbContext)` for direct DB access |
| `CleanupTestUserAsync(email)` | Removes user, profile, notification prefs from DB + clears mock Kratos session |

### TestAuthHelper

Creates test users and manages mock Kratos authentication for integration tests.

```csharp
public class TestAuthHelper
{
    public async Task<(string email, AuthResponse auth)> CreateAndAuthenticateUserAsync(string testPrefix);
    public void SetAuthToken(string token);       // Adds X-Session-Token header
    public void SetMockSessionToken(string token); // Same as SetAuthToken
    public void SetMockSessionCookie(string cookie); // Adds ory_kratos_session cookie
    public void ClearAuthToken();                 // Alias for ClearAuth()
    public void ClearAuth();                      // Removes X-Session-Token, Cookie, Authorization
    public async Task CleanupUserAsync(string email);
}
```

**`CreateAndAuthenticateUserAsync` flow:**
1. Generates unique email: `{testPrefix}-{guid}@test.homassy.local`
2. Creates `User` directly in PostgreSQL (with `KratosIdentityId = Guid.NewGuid()`)
3. Creates `UserProfile` with default HUF/Hungarian/CentralEurope settings
4. Calls `factory.RegisterTestSession(kratosIdentityId, ...)` on `MockKratosService`
5. Returns `(email, AuthResponse { AccessToken = "mock-session-{kratosIdentityId}" })`

**`AuthResponse` shape:**
```csharp
public class AuthResponse
{
    public string AccessToken { get; set; }   // "mock-session-{kratosIdentityId}"
    public string RefreshToken { get; set; }  // "mock-refresh-{kratosIdentityId}"
}
```

---

## Test Patterns & Conventions

### Test Class Structure (Integration)

```csharp
public class MyControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly TestAuthHelper _authHelper;
    private readonly ITestOutputHelper _output;

    public MyControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _authHelper = new TestAuthHelper(factory, _client);
        _output = output;
    }
}
```

### Test Class Structure (Unit – no factory needed)

```csharp
public class MyServiceTests : IDisposable
{
    private readonly MyService _service;

    public MyServiceTests()
    {
        _service = new MyService(/* direct dependencies */);
    }

    public void Dispose()
    {
        // cleanup static state if needed
    }
}
```

### Test Naming Convention

`MethodName_Scenario_ExpectedResult`

```csharp
[Fact]
public async Task GetProfile_AuthenticatedUser_ReturnsProfileData()

[Fact]
public async Task UpdateProfile_InvalidData_ReturnsBadRequest()

[Fact]
public void IsLockedOut_WhenLockedOutUntilIsNull_ReturnsFalse()
```

### Test Organization with Regions

```csharp
#region GET /api/v1.0/user/profile
[Fact]
public async Task GetProfile_AuthenticatedUser_ReturnsProfileData() { }
#endregion

#region PUT /api/v1.0/user/profile
[Fact]
public async Task UpdateProfile_ValidData_ReturnsSuccess() { }
#endregion
```

### Cleanup Pattern (Integration Tests)

Always clean up test data in a `finally` block:

```csharp
[Fact]
public async Task SomeEndpoint_Scenario_ExpectedResult()
{
    string? testEmail = null;
    try
    {
        // Arrange
        var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("my-test");
        testEmail = email;
        _authHelper.SetAuthToken(auth.AccessToken);

        // Act
        var response = await _client.GetAsync("/api/v1.0/endpoint");
        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {body}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    finally
    {
        _authHelper.ClearAuth();
        if (testEmail != null)
            await _authHelper.CleanupUserAsync(testEmail);
    }
}
```

### Theory Tests

```csharp
[Theory]
[InlineData(Currency.Huf, "HUF")]
[InlineData(Currency.Eur, "EUR")]
public void MapCurrency_ValidEnum_ReturnsCorrectString(Currency currency, string expected)
{
    var result = CurrencyFunctions.MapToString(currency);
    Assert.Equal(expected, result);
}
```

### Output Logging

```csharp
_output.WriteLine($"Status: {response.StatusCode}");
_output.WriteLine($"Response: {responseBody}");
```

---

## Configuration

### `appsettings.Testing.json`

Not in source control. Required structure:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=homassy;Username=...;Password=..."
  },
  "Kratos": {
    "PublicUrl": "http://localhost:4433",
    "AdminUrl": "http://localhost:4434",
    "SessionCookieName": "ory_kratos_session",
    "WebhookSecret": "test-secret"
  },
  "ProductSettings": {
    "ExpiringSoonThresholdDays": 7
  },
  "RateLimiting": {
    "GlobalMaxAttempts": "1000",
    "GlobalWindowMinutes": "1",
    "EndpointMaxAttempts": "500",
    "EndpointWindowMinutes": "1"
  },
  "Security": {
    "AccountLockout": {
      "MaxFailedAttempts": 5,
      "LockoutDurationMinutes": 15
    }
  },
  "RegistrationEnabled": true
}
```

> **Note:** No JWT configuration is needed. Authentication is handled by `MockKratosService` using `X-Session-Token` headers. The Kratos URLs in config are present for structural completeness but are never actually called (the mock intercepts all calls).

### Configuration Priority

1. `Homassy.API/appsettings.json` (base)
2. `Homassy.Tests/appsettings.Testing.json` (overrides)
3. Environment variables

---

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run only unit tests
dotnet test --filter "FullyQualifiedName~Homassy.Tests.Unit"

# Run only integration tests
dotnet test --filter "FullyQualifiedName~Homassy.Tests.Integration"

# Run a specific test class
dotnet test --filter "FullyQualifiedName~UserControllerTests"

# Run a specific test method
dotnet test --filter "FullyQualifiedName~UserControllerTests.GetProfile_AuthenticatedUser_ReturnsProfileData"

# Run with code coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

### Visual Studio

- **Test Explorer**: View → Test Explorer
- Run all tests, specific test class, or individual tests
- Debug tests with breakpoints

### Code Coverage

```bash
dotnet tool install -g dotnet-coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

---

## Development Guidelines

### Adding New Integration Tests

1. Add test class with `IClassFixture<HomassyWebApplicationFactory>`
2. Inject `HomassyWebApplicationFactory` and `ITestOutputHelper`
3. Create `TestAuthHelper` in constructor
4. Use `try/finally` with `_authHelper.ClearAuth()` and `CleanupUserAsync()`
5. Use `_authHelper.SetAuthToken(auth.AccessToken)` before authenticated requests

```csharp
public class MyControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly TestAuthHelper _authHelper;
    private readonly ITestOutputHelper _output;

    public MyControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _authHelper = new TestAuthHelper(factory, _client);
        _output = output;
    }

    [Fact]
    public async Task GetSomething_AuthenticatedUser_ReturnsData()
    {
        string? testEmail = null;
        try
        {
            var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("mysuffix");
            testEmail = email;
            _authHelper.SetAuthToken(auth.AccessToken);

            var response = await _client.GetAsync("/api/v1.0/something");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            _authHelper.ClearAuth();
            if (testEmail != null)
                await _authHelper.CleanupUserAsync(testEmail);
        }
    }
}
```

### Adding New Unit Tests

Most unit tests do **not** need `HomassyWebApplicationFactory`. Instantiate the service under test directly:

```csharp
public class MyServiceTests
{
    [Fact]
    public void MyMethod_GivenInput_ReturnsExpected()
    {
        // Arrange
        var service = new MyService(/* dependencies */);

        // Act
        var result = service.MyMethod(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
```

If the service uses static state, implement `IDisposable` to reset it.

### Authentication in Tests

**Authenticated requests:**
```csharp
var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("suffix");
_authHelper.SetAuthToken(auth.AccessToken);
// auth.AccessToken == "mock-session-{kratosIdentityId}"
// Header sent: X-Session-Token: mock-session-{kratosIdentityId}
```

**Unauthenticated requests (test 401):**
```csharp
// Don't call SetAuthToken
var response = await _client.GetAsync("/api/v1.0/protected");
Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
```

**Cookie-based auth:**
```csharp
_authHelper.SetMockSessionCookie($"mock-session-{kratosIdentityId}");
```

### Direct DB Access in Tests

```csharp
var (scope, context) = _factory.CreateScopedDbContext();
await using var _ = scope as IAsyncDisposable; // auto-dispose

var user = context.Users.FirstOrDefault(u => u.Email == email);
_output.WriteLine($"User exists: {user != null}");
```

### Test Data Management

**DO:**
- Use `{testPrefix}-{Guid.NewGuid():N}@test.homassy.local` email format (handled by `TestAuthHelper`)
- Always clean up in `finally` blocks
- Use unique GUIDs for any test-created data IDs

**DON'T:**
- Leave test data in the database
- Rely on test execution order
- Share mutable state between test instances

### Important Notes

- **No JWT** – the system uses Kratos session tokens. There are no JWT access tokens, refresh token endpoints, or JWT configuration in tests
- **MockKratosService** intercepts all `IKratosService` calls – no real Kratos process is needed
- **`AccountLockoutIntegrationTests`** is a stub – account lockout is delegated to Kratos configuration
- **Registration via API is not tested** – users are inserted directly into the DB for speed and reliability
