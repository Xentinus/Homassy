using System.Net.Http;
using Homassy.API.Context;
using Homassy.API.Functions;
using Homassy.API.Models.User;
using Homassy.API.Services;
using Homassy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Homassy.Tests.Unit;

public class UserFunctionsTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HomassyWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly IDbContextFactory<HomassyDbContext> _contextFactory;

    public UserFunctionsTests(HomassyWebApplicationFactory factory)
    {
        _factory = factory;
        // Create a client to ensure the server is started
        _client = _factory.CreateClient();

        // Ensure static services are configured for unit tests that bypass the factory
        EnsureConfigurationInitialized();

        // UserFunctions is scoped, so it cannot be resolved from the root provider; the context
        // factory it needs is a singleton, so the tests build the instance themselves.
        _contextFactory = _factory.Services.GetRequiredService<IDbContextFactory<HomassyDbContext>>();
    }

    private static void EnsureConfigurationInitialized()
    {
        // Shared with the other unit tests that touch the static configuration hooks, so every
        // one of them installs the same (real) configuration — see TestConfiguration.
        HomassyDbContext.SetConfiguration(TestConfiguration.Configuration);
        ConfigService.Initialize(TestConfiguration.Configuration);
    }

    [Fact]
    public async Task CreateUserAsync_ValidRequest_CreatesUser()
    {
        // Arrange
        var userFunctions = new UserFunctions(_contextFactory);
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
        var userFunctions = new UserFunctions(_contextFactory);
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

    /// <summary>
    /// Creates the user it looks up, rather than picking <c>Users.FirstOrDefault()</c>.
    /// </summary>
    /// <remarks>
    /// The old version asserted on whatever row the database happened to return first, which
    /// made it a coin flip: on an empty database it returned early and asserted nothing, and
    /// once other tests had inserted users it could pick one whose email is not stored in the
    /// normalised form <see cref="UserFunctions.GetUserByEmailAddress"/> looks for, and fail
    /// for a reason that has nothing to do with the lookup. That normalisation gap is real and
    /// tracked in Xentinus/Homassy#136; this test should not be the thing that reports it.
    /// </remarks>
    [Fact]
    public async Task GetUserByEmailAddress_ExistingUser_ReturnsUser()
    {
        var userFunctions = new UserFunctions(_contextFactory);
        var email = $"lookup-{Guid.NewGuid():N}@example.com";

        var created = await userFunctions.CreateUserAsync(new CreateUserRequest
        {
            Email = email,
            Name = "Lookup Target",
            DisplayName = "Lookup"
        });

        try
        {
            var user = userFunctions.GetUserByEmailAddress(email);

            Assert.NotNull(user);
            Assert.Equal(created.Email, user.Email);
            Assert.Equal(email.ToLowerInvariant(), user.Email);
        }
        finally
        {
            await _factory.CleanupTestUserAsync(email);
        }
    }

    [Fact]
    public async Task GetUserByEmailAddress_IgnoresCasingOfTheArgument()
    {
        var userFunctions = new UserFunctions(_contextFactory);
        var email = $"casing-{Guid.NewGuid():N}@example.com";

        await userFunctions.CreateUserAsync(new CreateUserRequest
        {
            Email = email,
            Name = "Casing Target",
            DisplayName = "Casing"
        });

        try
        {
            Assert.NotNull(userFunctions.GetUserByEmailAddress(email.ToUpperInvariant()));
        }
        finally
        {
            await _factory.CleanupTestUserAsync(email);
        }
    }

    [Fact]
    public void GetUserByEmailAddress_NonExistingUser_ReturnsNull()
    {
        // Arrange
        var userFunctions = new UserFunctions(_contextFactory);
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
        var userFunctions = new UserFunctions(_contextFactory);

        // Act & Assert
        Assert.Null(userFunctions.GetUserByEmailAddress(null));
        Assert.Null(userFunctions.GetUserByEmailAddress(""));
        Assert.Null(userFunctions.GetUserByEmailAddress("   "));
    }

    [Fact]
    public void GetUserByPublicId_NonExistingId_ReturnsNull()
    {
        // Arrange
        var userFunctions = new UserFunctions(_contextFactory);
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
        var userFunctions = new UserFunctions(_contextFactory);

        // Act
        var user = userFunctions.GetUserByPublicId(null);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public void GetUserById_NullId_ReturnsNull()
    {
        // Arrange
        var userFunctions = new UserFunctions(_contextFactory);

        // Act
        var user = userFunctions.GetUserById(null);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public void GetUsersByIds_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var userFunctions = new UserFunctions(_contextFactory);

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
        var userFunctions = new UserFunctions(_contextFactory);

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
        var userFunctions = new UserFunctions(_contextFactory);

        // Act & Assert
        Assert.Null(userFunctions.GetAllUserDataByEmail(null));
        Assert.Null(userFunctions.GetAllUserDataByEmail(""));
        Assert.Null(userFunctions.GetAllUserDataByEmail("   "));
    }
}
