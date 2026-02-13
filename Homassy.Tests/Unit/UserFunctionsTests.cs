using System.Net.Http;
using Homassy.API.Context;
using Homassy.API.Functions;
using Homassy.API.Models.User;
using Homassy.API.Services;
using Homassy.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Homassy.Tests.Unit;

public class UserFunctionsTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HomassyWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserFunctionsTests(HomassyWebApplicationFactory factory)
    {
        _factory = factory;
        // Create a client to ensure the server is started
        _client = _factory.CreateClient();
        
        // Ensure static services are configured for unit tests that bypass the factory
        EnsureConfigurationInitialized();
    }

    private void EnsureConfigurationInitialized()
    {
        // Build configuration from the test settings
        var projectDir = Directory.GetCurrentDirectory();
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(projectDir, "..", "Homassy.API", "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(projectDir, "appsettings.Testing.json"), optional: true)
            .Build();

        HomassyDbContext.SetConfiguration(configuration);
        ConfigService.Initialize(configuration);
        // JwtService.Initialize removed - using Kratos for authentication
    }

    [Fact]
    public async Task CreateUserAsync_ValidRequest_CreatesUser()
    {
        // Arrange
        var userFunctions = new UserFunctions();
        var uniqueEmail = $"test-create-{Guid.NewGuid()}@example.com";

        var request = new CreateUserRequest
        {
            Email = uniqueEmail,
            Name = "Test User",
            DisplayName = "Tester"
        };

        try
        {
            // Act
            var user = await userFunctions.CreateUserAsync(request);

            // Assert
            Assert.NotNull(user);
            Assert.Equal(uniqueEmail.ToLowerInvariant(), user.Email);
            Assert.Equal("Test User", user.Name);
            Assert.NotEqual(Guid.Empty, user.PublicId);
        }
        finally
        {
            // Cleanup
            await _factory.CleanupTestUserAsync(uniqueEmail);
        }
    }

    [Fact]
    public async Task CreateUserAsync_EmailIsNormalized_ToLowerCase()
    {
        // Arrange
        var userFunctions = new UserFunctions();
        var uniqueEmail = $"TEST-UPPER-{Guid.NewGuid()}@EXAMPLE.COM";

        var request = new CreateUserRequest
        {
            Email = uniqueEmail,
            Name = "Test User"
        };

        try
        {
            // Act
            var user = await userFunctions.CreateUserAsync(request);

            // Assert
            Assert.Equal(uniqueEmail.ToLowerInvariant(), user.Email);
        }
        finally
        {
            // Cleanup
            await _factory.CleanupTestUserAsync(uniqueEmail);
        }
    }

    [Fact]
    public void GetUserByEmailAddress_ExistingUser_ReturnsUser()
    {
        // Arrange
        var userFunctions = new UserFunctions();
        var (scope, context) = _factory.CreateScopedDbContext();

        try
        {
            var existingUser = context.Users.FirstOrDefault();
            if (existingUser == null)
            {
                // Skip test if no users exist
                return;
            }

            // Act
            var user = userFunctions.GetUserByEmailAddress(existingUser.Email);

            // Assert
            Assert.NotNull(user);
            Assert.Equal(existingUser.Email, user.Email);
            Assert.Equal(existingUser.Id, user.Id);
        }
        finally
        {
            scope.Dispose();
        }
    }

    [Fact]
    public void GetUserByEmailAddress_NonExistingUser_ReturnsNull()
    {
        // Arrange
        var userFunctions = new UserFunctions();
        var nonExistentEmail = $"nonexistent-{Guid.NewGuid()}@example.com";

        // Act
        var user = userFunctions.GetUserByEmailAddress(nonExistentEmail);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public void GetUserByEmailAddress_NullOrEmpty_ReturnsNull()
    {
        // Arrange
        var userFunctions = new UserFunctions();

        // Act & Assert
        Assert.Null(userFunctions.GetUserByEmailAddress(null));
        Assert.Null(userFunctions.GetUserByEmailAddress(""));
        Assert.Null(userFunctions.GetUserByEmailAddress("   "));
    }

    [Fact]
    public void GetUserByPublicId_NonExistingId_ReturnsNull()
    {
        // Arrange
        var userFunctions = new UserFunctions();
        var nonExistentGuid = Guid.NewGuid();

        // Act
        var user = userFunctions.GetUserByPublicId(nonExistentGuid);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public void GetUserByPublicId_NullGuid_ReturnsNull()
    {
        // Arrange
        var userFunctions = new UserFunctions();

        // Act
        var user = userFunctions.GetUserByPublicId(null);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public void GetUserById_NullId_ReturnsNull()
    {
        // Arrange
        var userFunctions = new UserFunctions();

        // Act
        var user = userFunctions.GetUserById(null);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public void GetUsersByIds_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var userFunctions = new UserFunctions();

        // Act
        var users = userFunctions.GetUsersByIds([]);

        // Assert
        Assert.NotNull(users);
        Assert.Empty(users);
    }

    [Fact]
    public void GetUsersByIds_NullList_ReturnsEmptyList()
    {
        // Arrange
        var userFunctions = new UserFunctions();

        // Act
        var users = userFunctions.GetUsersByIds(null!);

        // Assert
        Assert.NotNull(users);
        Assert.Empty(users);
    }

    [Fact]
    public void GetAllUserDataByEmail_NullOrEmpty_ReturnsNull()
    {
        // Arrange
        var userFunctions = new UserFunctions();

        // Act & Assert
        Assert.Null(userFunctions.GetAllUserDataByEmail(null));
        Assert.Null(userFunctions.GetAllUserDataByEmail(""));
        Assert.Null(userFunctions.GetAllUserDataByEmail("   "));
    }
}
