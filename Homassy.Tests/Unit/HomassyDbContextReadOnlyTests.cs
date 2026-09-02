using Homassy.API.Context;
using Homassy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Homassy.Tests.Unit;

/// <summary>
/// The Functions layer opens a context per operation. Read-only operations take one from
/// <see cref="HomassyDbContext.ForReading"/> so their rows are not held in a change tracker
/// that will never be saved through.
/// </summary>
public class HomassyDbContextReadOnlyTests
{
    private static void EnsureConfigured() => TestConfiguration.EnsureDbContextConfigured();

    [Fact]
    public void ForReading_DoesNotTrackQueryResults()
    {
        EnsureConfigured();
        using var context = HomassyDbContext.ForReading();

        Assert.Equal(
            QueryTrackingBehavior.NoTrackingWithIdentityResolution,
            context.ChangeTracker.QueryTrackingBehavior);
    }

    [Fact]
    public void ForReading_KeepsIdentityResolution()
    {
        EnsureConfigured();
        using var context = HomassyDbContext.ForReading();

        // Plain NoTracking would materialise a row reached twice (through an Include, say) as two
        // objects. Identity resolution keeps one instance per key, so behaviour matches a
        // tracking query and callers that compare references are unaffected.
        Assert.NotEqual(QueryTrackingBehavior.NoTracking, context.ChangeTracker.QueryTrackingBehavior);
    }

    [Fact]
    public void ForReading_RefusesToSave()
    {
        EnsureConfigured();
        using var context = HomassyDbContext.ForReading();

        // Nothing is tracked, so a save would report success and write nothing. Better to fail.
        var exception = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        Assert.Contains("ForReading", exception.Message);
    }

    [Fact]
    public async Task ForReading_RefusesToSaveAsynchronously()
    {
        EnsureConfigured();
        using var context = HomassyDbContext.ForReading();

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public void DefaultConstructor_StillTracks()
    {
        EnsureConfigured();
        using var context = new HomassyDbContext();

        Assert.Equal(QueryTrackingBehavior.TrackAll, context.ChangeTracker.QueryTrackingBehavior);
    }
}
