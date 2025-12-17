using Homassy.API.Enums;
using Homassy.API.Models.Background;
using Homassy.API.Services.Background;

namespace Homassy.Tests.Unit;

public class EmailBackgroundServiceTests
{
    private static EmailQueueService CreateQueueService(int capacity = 10)
    {
        return new EmailQueueService(capacity);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_StopsGracefully()
    {
        var queueService = CreateQueueService();
        var service = new EmailBackgroundService(queueService);
        var cts = new CancellationTokenSource();

        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);

        await cts.CancelAsync();

        await service.StopAsync(CancellationToken.None);

        Assert.True(executeTask.IsCompleted || executeTask.IsCanceled || executeTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task TryQueueEmailAsync_WhenTaskQueued_ReturnsTrue()
    {
        var queueService = CreateQueueService();
        var emailTask = new EmailTask("test@example.com", "123456", UserTimeZone.UTC, EmailType.Verification);

        var result = await queueService.TryQueueEmailAsync(emailTask);

        Assert.True(result);
    }

    [Fact]
    public async Task TryQueueEmailAsync_WhenMultipleTasksQueued_AllReturnTrue()
    {
        var queueService = CreateQueueService();

        var result1 = await queueService.TryQueueEmailAsync(new EmailTask("test1@example.com", "111111", UserTimeZone.UTC, EmailType.Verification));
        var result2 = await queueService.TryQueueEmailAsync(new EmailTask("test2@example.com", "222222", UserTimeZone.UTC, EmailType.Registration));
        var result3 = await queueService.TryQueueEmailAsync(new EmailTask("test3@example.com", "333333", UserTimeZone.CentralEuropeStandardTime, EmailType.Verification));

        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
    }

    [Fact]
    public void Constructor_WhenValidQueueService_DoesNotThrow()
    {
        var queueService = CreateQueueService();

        var exception = Record.Exception(() => new EmailBackgroundService(queueService));

        Assert.Null(exception);
    }

    [Fact]
    public async Task StartAsync_WhenCalled_DoesNotThrow()
    {
        var queueService = CreateQueueService();
        var service = new EmailBackgroundService(queueService);
        var cts = new CancellationTokenSource();

        var exception = await Record.ExceptionAsync(async () =>
        {
            var startTask = service.StartAsync(cts.Token);
            await Task.Delay(50);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);
        });

        Assert.Null(exception);
    }
}
