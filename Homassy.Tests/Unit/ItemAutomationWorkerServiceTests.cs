extern alias NotificationsProject;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using NotificationsProject::Homassy.Notifications.Services;
using NotificationsProject::Homassy.Notifications.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Homassy.Tests.Unit;

/// <summary>
/// Tests for ItemAutomationWorkerService lifecycle and
/// PushNotificationContentService automation notification content.
/// </summary>
public class ItemAutomationWorkerServiceTests
{
    // -------------------------------------------------------------------------
    // PushNotificationContentService.GetAutomationNotificationContent
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(Language.Hungarian, "Automatikus felhasználás")]
    [InlineData(Language.German, "Automatischer Verbrauch")]
    [InlineData(Language.English, "Automatic Consumption")]
    public void GetAutomationNotificationContent_ReturnsCorrectTitle(Language language, string expectedTitle)
    {
        var (title, _) = PushNotificationContentService.GetAutomationNotificationContent(
            language, "TestProduct", 2.5m, "Piece");

        Assert.Equal(expectedTitle, title);
    }

    [Fact]
    public void GetAutomationNotificationContent_Hungarian_BodyContainsProductAndQuantity()
    {
        var (_, body) = PushNotificationContentService.GetAutomationNotificationContent(
            Language.Hungarian, "Mosószer", 1.5m, "liter");

        Assert.Contains("Mosószer", body);
        Assert.Contains("1.5", body);
        Assert.Contains("liter", body);
    }

    [Fact]
    public void GetAutomationNotificationContent_German_BodyContainsProductAndQuantity()
    {
        var (_, body) = PushNotificationContentService.GetAutomationNotificationContent(
            Language.German, "Waschmittel", 3m, "Stück");

        Assert.Contains("Waschmittel", body);
        Assert.Contains("3", body);
        Assert.Contains("Stück", body);
    }

    [Fact]
    public void GetAutomationNotificationContent_English_BodyContainsProductAndQuantity()
    {
        var (_, body) = PushNotificationContentService.GetAutomationNotificationContent(
            Language.English, "Detergent", 2m, "piece");

        Assert.Contains("Detergent", body);
        Assert.Contains("2", body);
        Assert.Contains("piece", body);
    }

    [Fact]
    public void GetAutomationNotificationContent_UnknownLanguage_FallsBackToEnglish()
    {
        const Language unknownLanguage = (Language)999;

        var (title, body) = PushNotificationContentService.GetAutomationNotificationContent(
            unknownLanguage, "TestProduct", 1m, "unit");

        Assert.Equal("Automatic Consumption", title);
        Assert.Contains("TestProduct", body);
    }

    [Fact]
    public void GetAutomationNotificationContent_NeitherTitleNorBodyIsEmpty()
    {
        foreach (var language in Enum.GetValues<Language>())
        {
            var (title, body) = PushNotificationContentService.GetAutomationNotificationContent(
                language, "Product", 1m, "unit");

            Assert.False(string.IsNullOrWhiteSpace(title), $"Title should not be empty for language {language}");
            Assert.False(string.IsNullOrWhiteSpace(body), $"Body should not be empty for language {language}");
        }
    }

    // -------------------------------------------------------------------------
    // PushNotificationContentService.GetAutomationReminderContent
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(Language.Hungarian, "Felhasználási emlékeztető")]
    [InlineData(Language.German, "Verbrauchserinnerung")]
    [InlineData(Language.English, "Usage Reminder")]
    public void GetAutomationReminderContent_ReturnsCorrectTitle(Language language, string expectedTitle)
    {
        var (title, _) = PushNotificationContentService.GetAutomationReminderContent(
            language, "TestProduct");

        Assert.Equal(expectedTitle, title);
    }

    [Fact]
    public void GetAutomationReminderContent_Hungarian_BodyContainsProduct()
    {
        var (_, body) = PushNotificationContentService.GetAutomationReminderContent(
            Language.Hungarian, "Vitamin");

        Assert.Contains("Vitamin", body);
    }

    [Fact]
    public void GetAutomationReminderContent_German_BodyContainsProduct()
    {
        var (_, body) = PushNotificationContentService.GetAutomationReminderContent(
            Language.German, "Wasserfilter");

        Assert.Contains("Wasserfilter", body);
    }

    [Fact]
    public void GetAutomationReminderContent_English_BodyContainsProduct()
    {
        var (_, body) = PushNotificationContentService.GetAutomationReminderContent(
            Language.English, "Water Filter");

        Assert.Contains("Water Filter", body);
    }

    [Fact]
    public void GetAutomationReminderContent_UnknownLanguage_FallsBackToEnglish()
    {
        const Language unknownLanguage = (Language)999;

        var (title, body) = PushNotificationContentService.GetAutomationReminderContent(
            unknownLanguage, "TestProduct");

        Assert.Equal("Usage Reminder", title);
        Assert.Contains("TestProduct", body);
    }

    [Fact]
    public void GetAutomationReminderContent_NeitherTitleNorBodyIsEmpty()
    {
        foreach (var language in Enum.GetValues<Language>())
        {
            var (title, body) = PushNotificationContentService.GetAutomationReminderContent(
                language, "Product");

            Assert.False(string.IsNullOrWhiteSpace(title), $"Title should not be empty for language {language}");
            Assert.False(string.IsNullOrWhiteSpace(body), $"Body should not be empty for language {language}");
        }
    }

    // -------------------------------------------------------------------------
    // ItemAutomationWorkerService lifecycle
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            new ItemAutomationWorkerService(
                new NoOpServiceScopeFactory(),
                new NoOpWebPushService()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledBeforeFirstRun_StopsGracefully()
    {
        var service = new ItemAutomationWorkerService(
            new NoOpServiceScopeFactory(),
            new NoOpWebPushService());

        using var cts = new CancellationTokenSource();

        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(50);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.True(startTask.IsCompleted || startTask.IsCompletedSuccessfully);
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed class NoOpWebPushService : IWebPushService
    {
        public Task<bool> SendNotificationAsync(
            UserPushSubscription subscription,
            string title,
            string body,
            string? url = null,
            string? actionTitle = null,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public string GetVapidPublicKey() => "test-vapid-key";
    }

    private sealed class NoOpServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException(
            "Scope creation not supported in unit tests — the 5-minute delay prevents ProcessDueAutomationsAsync from being called.");
    }
}
