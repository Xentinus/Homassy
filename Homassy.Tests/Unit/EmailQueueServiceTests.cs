using Homassy.API.Enums;
using Homassy.API.Models.Background;
using Homassy.API.Services.Background;

namespace Homassy.Tests.Unit;

public class EmailQueueServiceTests
{
    private static EmailQueueService CreateService(int capacity = 100)
    {
        return new EmailQueueService(capacity);
    }

    private static EmailTask CreateTask(string email = "test@example.com", EmailType type = EmailType.Verification, Language language = Language.English)
    {
        return new EmailTask(email, "123456", UserTimeZone.UTC, language, type);
    }

    #region TryQueueEmailAsync Tests

    [Fact]
    public async Task TryQueueEmailAsync_WhenValidTask_ReturnsTrue()
    {
        var service = CreateService();
        var task = CreateTask();

        var result = await service.TryQueueEmailAsync(task);

        Assert.True(result);
    }

    [Fact]
    public async Task TryQueueEmailAsync_WhenMultipleTasks_AllReturnTrue()
    {
        var service = CreateService();

        var result1 = await service.TryQueueEmailAsync(CreateTask("test1@example.com"));
        var result2 = await service.TryQueueEmailAsync(CreateTask("test2@example.com"));
        var result3 = await service.TryQueueEmailAsync(CreateTask("test3@example.com"));

        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
    }

    [Fact]
    public async Task TryQueueEmailAsync_WhenNullTask_ThrowsArgumentNullException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.TryQueueEmailAsync(null!));
    }

    [Fact]
    public async Task TryQueueEmailAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var service = CreateService();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.TryQueueEmailAsync(CreateTask(), cts.Token));
    }

    #endregion

    #region EmailType Tests

    [Theory]
    [InlineData(EmailType.Verification)]
    [InlineData(EmailType.Registration)]
    public async Task TryQueueEmailAsync_WhenDifferentEmailTypes_ReturnsTrue(EmailType type)
    {
        var service = CreateService();
        var task = new EmailTask("test@example.com", "123456", UserTimeZone.UTC, Language.English, type);

        var result = await service.TryQueueEmailAsync(task);

        Assert.True(result);
    }

    #endregion

    #region TimeZone Tests

    [Theory]
    [InlineData(UserTimeZone.UTC)]
    [InlineData(UserTimeZone.CentralEuropeStandardTime)]
    [InlineData(UserTimeZone.EasternStandardTime)]
    public async Task TryQueueEmailAsync_WhenDifferentTimeZones_ReturnsTrue(UserTimeZone timeZone)
    {
        var service = CreateService();
        var task = new EmailTask("test@example.com", "123456", timeZone, Language.English, EmailType.Verification);

        var result = await service.TryQueueEmailAsync(task);

        Assert.True(result);
    }

    [Fact]
    public async Task TryQueueEmailAsync_WhenNullTimeZone_ReturnsTrue()
    {
        var service = CreateService();
        var task = new EmailTask("test@example.com", "123456", null, Language.English, EmailType.Verification);

        var result = await service.TryQueueEmailAsync(task);

        Assert.True(result);
    }

    #endregion

    #region Language Tests

    [Theory]
    [InlineData(Language.English)]
    [InlineData(Language.Hungarian)]
    [InlineData(Language.German)]
    public async Task TryQueueEmailAsync_WhenDifferentLanguages_ReturnsTrue(Language language)
    {
        var service = CreateService();
        var task = new EmailTask("test@example.com", "123456", UserTimeZone.UTC, language, EmailType.Verification);

        var result = await service.TryQueueEmailAsync(task);

        Assert.True(result);
    }

    #endregion

    #region Capacity Tests

    [Fact]
    public void Constructor_WhenDefaultCapacity_DoesNotThrow()
    {
        var exception = Record.Exception(() => new EmailQueueService());

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_WhenCustomCapacity_DoesNotThrow()
    {
        var exception = Record.Exception(() => new EmailQueueService(50));

        Assert.Null(exception);
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public async Task TryQueueEmailAsync_WhenQueueFullAndTimeout_ReturnsFalse()
    {
        var service = CreateService(1);
        await service.TryQueueEmailAsync(CreateTask());

        var result = await service.TryQueueEmailAsync(CreateTask("another@example.com"));

        Assert.False(result);
    }

    #endregion
}
