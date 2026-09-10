using System.Text.RegularExpressions;
using Homassy.API.Constants;

namespace Homassy.Tests.Unit;

/// <summary>
/// Structural guarantees over <see cref="BadgeCatalog"/>. Nothing here checks a particular badge -
/// the catalog is meant to grow - only the properties every entry has to keep for the badge grid,
/// the i18n keys and the durable earned rows to stay coherent.
/// </summary>
public class BadgeCatalogTests
{
    /// <summary>
    /// Ids reach the client, become part of i18n keys and DOM ids, and are stored verbatim on every
    /// <c>UserBadge</c> row - so they have to be safe in all three places.
    /// </summary>
    private static readonly Regex IdShape = new("^[a-z0-9-]+$", RegexOptions.Compiled);

    [Fact]
    public void All_IsNotEmpty()
    {
        Assert.NotEmpty(BadgeCatalog.All);
    }

    [Fact]
    public void All_IdsAreUnique()
    {
        var duplicates = BadgeCatalog.All
            .GroupBy(badge => badge.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void All_IdsAreLowercaseKebabCase()
    {
        var malformed = BadgeCatalog.All
            .Where(badge => !IdShape.IsMatch(badge.Id))
            .Select(badge => badge.Id)
            .ToList();

        Assert.Empty(malformed);
    }

    [Fact]
    public void All_ThresholdsArePositive()
    {
        Assert.All(BadgeCatalog.All, badge => Assert.True(badge.Threshold > 0, $"{badge.Id} has a threshold of {badge.Threshold}"));
    }

    /// <summary>
    /// The test that keeps "a new badge needs no frontend release" honest. A badge carrying only
    /// i18n keys renders as a blank tile on any client that has not shipped those keys yet, which
    /// is every client at the moment the badge is added.
    /// </summary>
    [Fact]
    public void All_CarryEnglishFallbackText()
    {
        Assert.All(BadgeCatalog.All, (badge) =>
        {
            Assert.False(string.IsNullOrWhiteSpace(badge.FallbackTitle), $"{badge.Id} has no fallback title");
            Assert.False(string.IsNullOrWhiteSpace(badge.FallbackDescription), $"{badge.Id} has no fallback description");
        });
    }

    [Fact]
    public void All_CarryAnIconAndTranslationKeys()
    {
        Assert.All(BadgeCatalog.All, (badge) =>
        {
            Assert.False(string.IsNullOrWhiteSpace(badge.IconName), $"{badge.Id} has no icon");
            Assert.False(string.IsNullOrWhiteSpace(badge.TitleKey), $"{badge.Id} has no title key");
            Assert.False(string.IsNullOrWhiteSpace(badge.DescriptionKey), $"{badge.Id} has no description key");
        });
    }

    /// <summary>
    /// Within one metric the tiers have to be strictly increasing, so "the next badge" is always a
    /// single well-defined thing. Two badges sharing a threshold would both unlock on the same
    /// counter value, which is two celebrations for one achievement and an ambiguous progress ring.
    /// </summary>
    [Fact]
    public void All_ThresholdsAreStrictlyIncreasingWithinEachMetric()
    {
        foreach (var metricGroup in BadgeCatalog.All.GroupBy(badge => badge.Metric))
        {
            var thresholds = metricGroup.Select(badge => badge.Threshold).OrderBy(threshold => threshold).ToList();
            var distinct = thresholds.Distinct().ToList();

            Assert.Equal(thresholds.Count, distinct.Count);
        }
    }

    [Fact]
    public void ById_KnownId_ReturnsTheDefinition()
    {
        var expected = BadgeCatalog.All[0];

        var actual = BadgeCatalog.ById(expected.Id);

        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Threshold, actual.Threshold);
    }

    /// <summary>
    /// Null, never a throw: the caller that matters is reading ids back out of the database, where
    /// an id retired from the catalog is ordinary and the right answer is to skip that row rather
    /// than fail the whole badge response.
    /// </summary>
    [Theory]
    [InlineData("no-such-badge")]
    [InlineData("")]
    [InlineData("Items-Added-100")]
    public void ById_UnknownId_ReturnsNull(string id)
    {
        Assert.Null(BadgeCatalog.ById(id));
    }
}
