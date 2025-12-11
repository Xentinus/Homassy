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

- **xUnit Framework**: Modern, extensible testing framework with excellent async support
- **Real Database Integration Tests**: Uses actual PostgreSQL database for integration testing
- **WebApplicationFactory**: ASP.NET Core's built-in integration testing infrastructure
- **Shared Test Infrastructure**: Common helpers and factories for test setup/teardown
- **Test Isolation**: Each test properly cleans up created data to avoid cross-test interference
- **IClassFixture Pattern**: Shared factory instances across tests for efficiency

---

## Technology Stack

### Framework & Runtime
- **.NET 10.0**
- **xUnit 2.9.3** - Testing framework
- **xUnit.runner.visualstudio 3.1.0** - Visual Studio test adapter

### Integration Testing
- **Microsoft.AspNetCore.Mvc.Testing 10.0.0** - WebApplicationFactory support
- **Microsoft.NET.Test.Sdk 17.14.1** - Test SDK

### Code Coverage
- **coverlet.collector 6.0.4** - Code coverage collection

---

## Project Structure

```
Homassy.Tests/
??? Infrastructure/                 Shared test infrastructure
?   ??? HomassyWebApplicationFactory.cs   Custom WebApplicationFactory for integration tests
?   ??? TestAuthHelper.cs                 Authentication helper for test user management
??? Integration/                    Integration tests (API endpoint testing)
?   ??? AuthControllerTests.cs           Authentication flow tests
?   ??? UserControllerTests.cs           User profile/settings tests
?   ??? FamilyControllerTests.cs         Family management tests
?   ??? LocationControllerTests.cs       Location (storage/shopping) tests
?   ??? ProductControllerTests.cs        Product catalog tests
?   ??? ShoppingListControllerTests.cs   Shopping list tests
??? Unit/                           Unit tests (isolated function testing)
?   ??? UserFunctionsTests.cs            User business logic tests
?   ??? TimeZoneFunctionsTests.cs        Timezone conversion tests
?   ??? UnitFunctionsTests.cs            Measurement unit tests
??? appsettings.Testing.json        Test-specific configuration (not in source control)
??? appsettings.Testing.Example.json Example configuration template
??? Homassy.Tests.csproj            Project file
```

---

## Test Categories

### Unit Tests (`/Unit`)

Unit tests focus on isolated testing of individual functions and classes without external dependencies (where possible).

**Characteristics:**
- Test individual methods in isolation
- May require database for data-dependent logic
- Fast execution
- No HTTP requests

**Example Test Classes:**
- `UserFunctionsTests` - Tests user creation, lookup, email normalization
- `TimeZoneFunctionsTests` - Tests timezone conversion logic
- `UnitFunctionsTests` - Tests measurement unit conversions

### Integration Tests (`/Integration`)

Integration tests verify the complete API behavior including HTTP layer, authentication, and database interactions.

**Characteristics:**
- Test full HTTP request/response cycle
- Use real database (PostgreSQL)
- Test authentication flows
- Verify API response formats
- Slower execution

**Example Test Classes:**
- `AuthControllerTests` - Registration, login, token refresh, logout
- `UserControllerTests` - Profile retrieval and updates
- `FamilyControllerTests` - Family CRUD operations
- `ProductControllerTests` - Product management
- `ShoppingListControllerTests` - Shopping list and item operations

---

## Infrastructure Components

### HomassyWebApplicationFactory

Custom `WebApplicationFactory<Program>` that configures the test environment:

```csharp
public class HomassyWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Loads appsettings.Testing.json
        // Sets environment to "Testing"
        // Initializes static services
    }
}
```

**Key Features:**

1. **Custom Configuration Loading**
   - Loads base `appsettings.json` from API project
   - Overlays `appsettings.Testing.json` for test-specific settings
   - Supports environment variables

2. **Static Service Initialization**
   - Re-initializes `HomassyDbContext`, `ConfigService`, `EmailService`, `JwtService`
   - Ensures test configuration is used

3. **Database Helper Methods**

   | Method | Description |
   |--------|-------------|
   | `GetVerificationCodeForEmail(email)` | Retrieves verification code from DB for auth tests |
   | `GetUserIdByEmail(email)` | Gets internal user ID for test operations |
   | `CreateScopedDbContext()` | Creates a scoped DbContext for direct DB operations |
   | `CleanupTestUserAsync(email)` | Removes test user and all related records |

### TestAuthHelper

Helper class that simplifies authenticated test scenarios:

```csharp
public class TestAuthHelper
{
    public async Task<(string Email, AuthResponse Auth)> CreateAndAuthenticateUserAsync(string? nameSuffix = null);
    public void SetAuthToken(string accessToken);
    public void ClearAuthToken();
    public async Task CleanupUserAsync(string email);
}
```

**Key Features:**

1. **CreateAndAuthenticateUserAsync**
   - Creates unique test user email
   - Cleans up any existing user with same email
   - Registers user via API
   - Retrieves verification code from database
   - Verifies code and returns auth tokens
   - Returns tuple of (email, AuthResponse)

2. **Token Management**
   - `SetAuthToken()` - Sets Bearer token on HttpClient
   - `ClearAuthToken()` - Removes authorization header

3. **Cleanup**
   - `CleanupUserAsync()` - Delegates to factory cleanup

---

## Test Patterns & Conventions

### Test Class Structure

Integration tests use `IClassFixture` for shared factory:

```csharp
public class AuthControllerTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public AuthControllerTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
    }
}
```

### Test Naming Convention

Tests follow the pattern: `MethodName_Scenario_ExpectedResult`

```csharp
[Fact]
public async Task RequestVerificationCode_ValidEmail_ReturnsSuccess()

[Fact]
public async Task Register_InvalidEmail_ReturnsBadRequest()

[Fact]
public void GetUserByEmailAddress_NonExistingUser_ReturnsNull()
```

### Test Organization with Regions

Tests are grouped using `#region` blocks:

```csharp
#region Request Verification Code Tests
[Fact]
public async Task RequestVerificationCode_ValidEmail_ReturnsSuccess() { }
#endregion

#region Register Tests
[Fact]
public async Task Register_ValidRequest_ReturnsSuccess() { }
#endregion
```

### Cleanup Pattern

Always cleanup test data in `finally` block:

```csharp
[Fact]
public async Task SomeTest()
{
    string? testEmail = null;
    try
    {
        var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("test-suffix");
        testEmail = email;
        
        // Test logic here
    }
    finally
    {
        _authHelper.ClearAuthToken();
        if (testEmail != null)
            await _authHelper.CleanupUserAsync(testEmail);
    }
}
```

### Output Logging

Use `ITestOutputHelper` for test diagnostics:

```csharp
_output.WriteLine($"Status Code: {response.StatusCode}");
_output.WriteLine($"Response Body: {responseBody}");
```

### Theory Tests for Multiple Inputs

Use `[Theory]` with `[InlineData]` for parameterized tests:

```csharp
[Theory]
[InlineData(UserTimeZone.UTC, "UTC")]
[InlineData(UserTimeZone.CentralEuropeStandardTime, "Central Europe Standard Time")]
public void GetTimeZoneId_ValidTimeZone_ReturnsCorrectId(UserTimeZone timeZone, string expectedId)
{
    var result = TimeZoneFunctions.GetTimeZoneId(timeZone);
    Assert.Equal(expectedId, result);
}
```

---

## Configuration

### appsettings.Testing.json

Test-specific configuration file (should NOT be in source control - add to .gitignore):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=homassy_test;Username=...;Password=..."
  },
  "Jwt": {
    "Issuer": "Homassy.API",
    "Audience": "Homassy.Client",
    "SecretKey": "your-test-secret-key-64-chars-minimum...",
    "AccessTokenExpirationMinutes": 30,
    "RefreshTokenExpirationDays": 14
  },
  "RateLimiting": {
    "GlobalMaxAttempts": "1000",
    "EndpointMaxAttempts": "500"
  },
  "RegistrationEnabled": true
}
```

### appsettings.Testing.Example.json

Template file with placeholder values (safe for source control):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=homassy_test;Username=your_username;Password=your_password"
  },
  "Jwt": {
    "SecretKey": "your-secret-key-at-least-64-characters-long-for-security-purposes-here"
  }
}
```

### Configuration Priority

1. Base: `Homassy.API/appsettings.json`
2. Override: `Homassy.Tests/appsettings.Testing.json`
3. Override: Environment variables

---

## Running Tests

### Visual Studio

- **Test Explorer**: View ? Test Explorer
- Run all tests, specific test class, or individual tests
- Debug tests with breakpoints

### Command Line

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~AuthControllerTests"

# Run with code coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

### Code Coverage

Install coverage tool:
```bash
dotnet tool install -g dotnet-coverage
```

Generate coverage report:
```bash
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

---

## Development Guidelines

### Adding New Integration Tests

1. **Create Test Class with Fixture**
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

2. **Follow Test Structure**
   ```csharp
   [Fact]
   public async Task Endpoint_Scenario_ExpectedResult()
   {
       string? testEmail = null;
       try
       {
           // Arrange
           var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("test");
           testEmail = email;
           _authHelper.SetAuthToken(auth.AccessToken);

           // Act
           var response = await _client.GetAsync("/api/v1.0/endpoint");
           var responseBody = await response.Content.ReadAsStringAsync();

           _output.WriteLine($"Status: {response.StatusCode}");
           _output.WriteLine($"Response: {responseBody}");

           // Assert
           Assert.Equal(HttpStatusCode.OK, response.StatusCode);
       }
       finally
       {
           _authHelper.ClearAuthToken();
           if (testEmail != null)
               await _authHelper.CleanupUserAsync(testEmail);
       }
   }
   ```

3. **Group Related Tests with Regions**

### Adding New Unit Tests

1. **Create Test Class**
   ```csharp
   public class MyFunctionsTests : IClassFixture<HomassyWebApplicationFactory>
   {
       private readonly HomassyWebApplicationFactory _factory;

       public MyFunctionsTests(HomassyWebApplicationFactory factory)
       {
           _factory = factory;
           EnsureConfigurationInitialized();
       }

       private void EnsureConfigurationInitialized()
       {
           // Initialize static services if needed
       }
   }
   ```

2. **Write Focused Tests**
   ```csharp
   [Fact]
   public void MethodName_Input_ExpectedOutput()
   {
       // Arrange
       var functions = new MyFunctions();

       // Act
       var result = functions.MyMethod(input);

       // Assert
       Assert.Equal(expected, result);
   }
   ```

### Test Data Management

**DO:**
- Use unique identifiers (GUIDs) in test data
- Always clean up created test data
- Use `try/finally` for guaranteed cleanup
- Clean up existing data before creating (for idempotent tests)

**DON'T:**
- Leave test data in the database
- Use hardcoded IDs that might conflict
- Rely on test execution order
- Share mutable state between tests

### Authentication in Tests

**For endpoints requiring authentication:**
```csharp
var (email, auth) = await _authHelper.CreateAndAuthenticateUserAsync("suffix");
_authHelper.SetAuthToken(auth.AccessToken);
// ... make authenticated requests
_authHelper.ClearAuthToken();
```

**For testing unauthenticated access:**
```csharp
// Don't set auth token
var response = await _client.GetAsync("/api/v1.0/protected-endpoint");
Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
```

### Debugging Failed Tests

1. **Use Output Logging**
   ```csharp
   _output.WriteLine($"Response: {responseBody}");
   ```

2. **Check Database State**
   ```csharp
   var (scope, context) = _factory.CreateScopedDbContext();
   var user = context.Users.FirstOrDefault(u => u.Email == email);
   _output.WriteLine($"User exists: {user != null}");
   scope.Dispose();
   ```

3. **Verify Test Configuration**
   - Check `appsettings.Testing.json` exists
   - Verify database connection string
   - Ensure `RegistrationEnabled` is true

---

## Common Test Scenarios

### Testing Successful Operations

```csharp
[Fact]
public async Task CreateResource_ValidData_ReturnsCreatedResource()
{
    // Arrange
    var request = new CreateResourceRequest { Name = "Test" };

    // Act
    var response = await _client.PostAsJsonAsync("/api/v1.0/resource", request);

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var content = await response.Content.ReadFromJsonAsync<ApiResponse<ResourceResponse>>();
    Assert.True(content?.Success);
    Assert.NotNull(content?.Data);
}
```

### Testing Validation Errors

```csharp
[Fact]
public async Task CreateResource_InvalidData_ReturnsBadRequest()
{
    // Arrange
    var request = new CreateResourceRequest { Name = "" }; // Invalid

    // Act
    var response = await _client.PostAsJsonAsync("/api/v1.0/resource", request);

    // Assert
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}
```

### Testing Not Found

```csharp
[Fact]
public async Task GetResource_NonExistent_ReturnsNotFound()
{
    // Arrange
    var nonExistentId = Guid.NewGuid();

    // Act
    var response = await _client.GetAsync($"/api/v1.0/resource/{nonExistentId}");

    // Assert
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

### Testing Authorization

```csharp
[Fact]
public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
{
    // Act (no auth token set)
    var response = await _client.GetAsync("/api/v1.0/protected");

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

---

## Summary

Homassy.Tests provides comprehensive testing infrastructure for the Homassy.API application:

- **Integration Tests**: Full API testing with real database
- **Unit Tests**: Isolated function testing
- **Shared Infrastructure**: Reusable factories and helpers
- **Clean Test Data Management**: Proper setup and teardown patterns
- **xUnit Best Practices**: Theory tests, fixtures, output helpers

Key principles:
- **Test Isolation**: Each test manages its own data
- **Real Database**: Integration tests use actual PostgreSQL
- **Authentication Helpers**: Simplified authenticated test scenarios
- **Consistent Patterns**: Follow established conventions for maintainability
