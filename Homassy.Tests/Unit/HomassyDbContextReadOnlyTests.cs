using Homassy.API.Context;
using Homassy.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Homassy.Tests.Unit;

/// <summary>
/// The Functions layer opens a context per operation. Read-only operations take one from
/// <see cref="HomassyDbContextFactoryExtensions.CreateForReading"/> so their rows are not held in
/// a change tracker that will never be saved through.
/// </summary>
public class HomassyDbContextReadOnlyTests
{
    private static IDbContextFactory<HomassyDbContext> Factory => TestConfiguration.DbContextFactory;

    [Fact]
    public void CreateForReading_DoesNotTrackQueryResults()
    {
        using var context = Factory.CreateForReading();

        Assert.Equal(
            QueryTrackingBehavior.NoTrackingWithIdentityResolution,
            context.ChangeTracker.QueryTrackingBehavior);
    }

    [Fact]
    public void CreateForReading_KeepsIdentityResolution()
    {
        using var context = Factory.CreateForReading();

        // Plain NoTracking would materialise a row reached twice (through an Include, say) as two
        // objects. Identity resolution keeps one instance per key, so behaviour matches a
        // tracking query and callers that compare references are unaffected.
        Assert.NotEqual(QueryTrackingBehavior.NoTracking, context.ChangeTracker.QueryTrackingBehavior);
    }

    [Fact]
    public void CreateForReading_RefusesToSave()
    {
        using var context = Factory.CreateForReading();

        // Nothing is tracked, so a save would report success and write nothing. Better to fail.
        var exception = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        Assert.Contains("CreateForReading", exception.Message);
    }

    [Fact]
    public async Task CreateForReading_RefusesToSaveAsynchronously()
    {
        using var context = Factory.CreateForReading();

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public void CreateDbContext_StillTracks()
    {
        using var context = Factory.CreateDbContext();

        Assert.Equal(QueryTrackingBehavior.TrackAll, context.ChangeTracker.QueryTrackingBehavior);
    }
}
