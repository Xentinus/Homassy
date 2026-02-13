using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

/// <summary>
/// Account lockout integration tests.
/// Note: Account lockout is now handled by Ory Kratos.
/// These tests are no longer applicable to the backend API.
/// </summary>
public class AccountLockoutIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public AccountLockoutIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AccountLockout_HandledByKratos_TestsNotApplicable()
    {
        // Account lockout is now handled by Ory Kratos identity management.
        // The Kratos configuration in kratos.yml defines lockout behavior.
        // See: selfservice.methods.code.config.max_attempts
        _output.WriteLine("Account lockout is now managed by Ory Kratos.");
        _output.WriteLine("Lockout configuration is in Homassy.Kratos/kratos.yml");
        Assert.True(true);
    }
}
