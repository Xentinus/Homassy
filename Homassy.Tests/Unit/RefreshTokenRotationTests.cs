using Xunit.Abstractions;

namespace Homassy.Tests.Unit;

/// <summary>
/// Refresh token rotation tests.
/// Note: Refresh token rotation is now handled by Ory Kratos.
/// These tests are no longer applicable.
/// </summary>
public class RefreshTokenRotationTests
{
    private readonly ITestOutputHelper _output;

    public RefreshTokenRotationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void RefreshTokenRotation_HandledByKratos_TestsNotApplicable()
    {
        // Refresh token rotation is now handled by Ory Kratos.
        // Sessions are managed entirely by Kratos.
        _output.WriteLine("Refresh token rotation is now managed by Ory Kratos.");
        _output.WriteLine("Session management configuration is in Homassy.Kratos/kratos.yml");
        Assert.True(true);
    }
}
