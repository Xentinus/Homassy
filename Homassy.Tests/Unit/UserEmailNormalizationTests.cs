using Homassy.Data.Entities.User;

namespace Homassy.Tests.Unit;

/// <summary>
/// Email normalisation used to be applied by each write path — CreateUserAsync, the profile
/// update, the Kratos sync — while the lookup normalised its argument before comparing. A row
/// that reached the table any other way kept its original casing, and the caller then got "no
/// such user" for a user that plainly existed. It is one setter now (#136).
/// </summary>
public class UserEmailNormalizationTests
{
    [Theory]
    [InlineData("User@Example.COM", "user@example.com")]
    [InlineData("  spaced@example.com  ", "spaced@example.com")]
    [InlineData("\tMixed-Case@Example.Com\n", "mixed-case@example.com")]
    [InlineData("already@canonical.com", "already@canonical.com")]
    public void NormalizeEmail_ReturnsTheCanonicalForm(string input, string expected)
    {
        Assert.Equal(expected, User.NormalizeEmail(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeEmail_OnAnEmptyValue_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, User.NormalizeEmail(input));
    }

    /// <summary>
    /// The property, not just the helper: the point of the fix is that no write path can store a
    /// non-canonical address, whether or not it remembered to normalise.
    /// </summary>
    [Fact]
    public void Email_NormalizesWhateverIsAssignedToIt()
    {
        var user = new User { Email = "  Select-StorageLocation-ABC@Test.Homassy.Local  ", Name = "Test User" };

        Assert.Equal("select-storagelocation-abc@test.homassy.local", user.Email);

        user.Email = "Renamed@EXAMPLE.com";

        Assert.Equal("renamed@example.com", user.Email);
    }

    /// <summary>
    /// A lookup key built from what a caller typed matches the stored value whatever case it
    /// arrived in — the two halves have to agree on "the same address" for either to be useful.
    /// </summary>
    [Fact]
    public void Email_MatchesALookupKeyBuiltFromAnyCasing()
    {
        var user = new User { Email = "Lookup.Target@Example.com", Name = "Test User" };

        Assert.Equal(user.Email, User.NormalizeEmail("LOOKUP.TARGET@EXAMPLE.COM"));
        Assert.Equal(user.Email, User.NormalizeEmail(" lookup.target@example.com "));
    }
}
