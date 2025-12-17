using Homassy.API.Services.Background;
using Microsoft.Extensions.DependencyInjection;

namespace Homassy.Tests.Unit;

public class TokenCleanupServiceTests
{
    private static IServiceScopeFactory CreateMockScopeFactory()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_StopsGracefully()
    {
        var scopeFactory = CreateMockScopeFactory();
        var service = new TokenCleanupService(scopeFactory);
        var cts = new CancellationTokenSource();

        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(100);

        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        Assert.True(startTask.IsCompleted || startTask.IsCanceled || startTask.IsCompletedSuccessfully);
    }

    [Fact]
    public void Constructor_WithValidScopeFactory_DoesNotThrow()
    {
        var scopeFactory = CreateMockScopeFactory();

        var exception = Record.Exception(() => new TokenCleanupService(scopeFactory));

        Assert.Null(exception);
    }
}
