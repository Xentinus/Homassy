using Homassy.API.Models.Activity;

namespace Homassy.Tests.Unit;

public class ActivityCursorTests
{
    [Fact]
    public void EncodeThenTryDecode_RoundTripsTimestampAndGuid()
    {
        // DateTime.UtcNow rather than a fixed literal: the point is that whatever tick precision
        // "now" happens to carry survives the round trip, not that a round number does.
        var timestamp = DateTime.UtcNow;
        var publicId = Guid.NewGuid();

        var cursor = ActivityCursor.Encode(timestamp, publicId);

        Assert.True(ActivityCursor.TryDecode(cursor, out var decodedTimestamp, out var decodedPublicId));
        Assert.Equal(timestamp.Ticks, decodedTimestamp.Ticks);
        Assert.Equal(publicId, decodedPublicId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryDecode_NullOrEmpty_ReturnsFalseWithoutThrowing(string? cursor)
    {
        var result = ActivityCursor.TryDecode(cursor, out var timestamp, out var publicId);

        Assert.False(result);
        Assert.Equal(default, timestamp);
        Assert.Equal(default, publicId);
    }

    [Fact]
    public void TryDecode_NotBase64_ReturnsFalse()
    {
        // '!' is outside the base64url alphabet, so this can never be a valid payload.
        var result = ActivityCursor.TryDecode("not-base64!!", out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryDecode_ValidBase64ButNotTicksColonGuid_ReturnsFalse()
    {
        // Valid base64url, but the decoded payload is plain prose with no ':' separator at all -
        // TryDecode must reject the shape, not just fail to parse a number.
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("this is not a cursor"))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var result = ActivityCursor.TryDecode(payload, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void Encode_ProducesAUrlSafeString()
    {
        // Run many guids/timestamps so the '+'/'/' cases in the base64 alphabet actually get hit
        // rather than relying on one sample that might not exercise them.
        var timestamp = new DateTime(2026, 9, 8, 12, 34, 56, 789, DateTimeKind.Utc);

        for (var i = 0; i < 200; i++)
        {
            var cursor = ActivityCursor.Encode(timestamp.AddTicks(i), Guid.NewGuid());

            Assert.DoesNotContain('+', cursor);
            Assert.DoesNotContain('/', cursor);
            Assert.DoesNotContain('=', cursor);
        }
    }
}
