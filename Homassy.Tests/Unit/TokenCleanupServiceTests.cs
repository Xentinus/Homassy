using Homassy.API.Services.Background;

namespace Homassy.Tests.Unit;

/// <summary>
/// Tests for TokenCleanupService.
/// Note: TokenCleanupService is now a no-op since Kratos handles session management.
/// These tests verify the service starts and stops gracefully.
/// </summary>
public class TokenCleanupServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCancelled_StopsGracefully()
    {
        var service = new TokenCleanupService();
        var cts = new CancellationTokenSource();

        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(100);

        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        Assert.True(startTask.IsCompleted || startTask.IsCanceled || startTask.IsCompletedSuccessfully);
    }

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var exception = Record.Exception(() => new TokenCleanupService());

        Assert.Null(exception);
    }
}
