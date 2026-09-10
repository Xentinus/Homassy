using Homassy.API.Services;
using Homassy.API.Services.Background;

namespace Homassy.Tests.Unit;

/// <summary>Tests for FamilyInsightsCacheCleanupService's hosting lifecycle (start/stop). The
/// sweep logic itself is exercised directly and thoroughly in
/// <see cref="FamilyInsightsCacheTests"/>'s "Sweep: CleanupExpiredEntries" region - this class
/// only covers the BackgroundService wiring, mirroring TokenCleanupServiceTests.</summary>
public class FamilyInsightsCacheCleanupServiceTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var exception = Record.Exception(() => new FamilyInsightsCacheCleanupService(new FamilyInsightsCache()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_StopsGracefully()
    {
        var service = new FamilyInsightsCacheCleanupService(new FamilyInsightsCache());
        var cts = new CancellationTokenSource();

        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(100);

        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        Assert.True(startTask.IsCompleted || startTask.IsCanceled || startTask.IsCompletedSuccessfully);
    }
}
