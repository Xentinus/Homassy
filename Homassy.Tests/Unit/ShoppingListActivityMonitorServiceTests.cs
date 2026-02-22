using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Homassy.API.Services;
using Homassy.API.Services.Background;
using Microsoft.Extensions.DependencyInjection;

namespace Homassy.Tests.Unit;

/// <summary>
/// Tests for ShoppingListActivityMonitorService and the related
/// PushNotificationContentService shopping list notification content.
/// </summary>
public class ShoppingListActivityMonitorServiceTests
{
    // -------------------------------------------------------------------------
    // PushNotificationContentService.GetShoppingListActivityContent
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(Language.Hungarian, "Bevásárlólista frissítve", "Heti lista")]
    [InlineData(Language.German, "Einkaufsliste aktualisiert", "Wochenliste")]
    [InlineData(Language.English, "Shopping List Updated", "Weekly List")]
    public void GetShoppingListActivityContent_ReturnsCorrectTitle_ForLanguage(
        Language language, string expectedTitle, string listName)
    {
        var (title, _) = PushNotificationContentService.GetShoppingListActivityContent(language, listName, 1);

        Assert.Equal(expectedTitle, title);
    }

    [Fact]
    public void GetShoppingListActivityContent_Hungarian_BodyContainsListName()
    {
        const string listName = "Heti bevásárlás";

        var (_, body) = PushNotificationContentService.GetShoppingListActivityContent(Language.Hungarian, listName, 3);

        Assert.Contains(listName, body);
        Assert.Contains("3", body);
    }

    [Fact]
    public void GetShoppingListActivityContent_German_BodyContainsListName()
    {
        const string listName = "Wocheneinkauf";

        var (_, body) = PushNotificationContentService.GetShoppingListActivityContent(Language.German, listName, 2);

        Assert.Contains(listName, body);
        Assert.Contains("2", body);
    }

    [Fact]
    public void GetShoppingListActivityContent_English_BodyContainsListName()
    {
        const string listName = "Weekly Shopping";

        var (_, body) = PushNotificationContentService.GetShoppingListActivityContent(Language.English, listName, 5);

        Assert.Contains(listName, body);
        Assert.Contains("5", body);
    }

    [Fact]
    public void GetShoppingListActivityContent_SingleItem_UsesSingularForm()
    {
        var (_, bodyHu) = PushNotificationContentService.GetShoppingListActivityContent(Language.Hungarian, "Lista", 1);
        var (_, bodyDe) = PushNotificationContentService.GetShoppingListActivityContent(Language.German, "Liste", 1);
        var (_, bodyEn) = PushNotificationContentService.GetShoppingListActivityContent(Language.English, "List", 1);

        Assert.StartsWith("1 ", bodyHu);
        Assert.StartsWith("1 ", bodyDe);
        Assert.StartsWith("1 ", bodyEn);
    }

    [Fact]
    public void GetShoppingListActivityContent_MultipleItems_UsesCount()
    {
        var (_, bodyHu) = PushNotificationContentService.GetShoppingListActivityContent(Language.Hungarian, "Lista", 4);
        var (_, bodyDe) = PushNotificationContentService.GetShoppingListActivityContent(Language.German, "Liste", 4);
        var (_, bodyEn) = PushNotificationContentService.GetShoppingListActivityContent(Language.English, "List", 4);

        Assert.StartsWith("4 ", bodyHu);
        Assert.StartsWith("4 ", bodyDe);
        Assert.StartsWith("4 ", bodyEn);
    }

    [Fact]
    public void GetShoppingListActivityContent_UnknownLanguage_FallsBackToEnglish()
    {
        const Language unknownLanguage = (Language)999;
        const string listName = "Test List";

        var (title, body) = PushNotificationContentService.GetShoppingListActivityContent(unknownLanguage, listName, 2);

        Assert.Equal("Shopping List Updated", title);
        Assert.Contains(listName, body);
        Assert.Contains("2", body);
    }

    [Fact]
    public void GetShoppingListActivityContent_NeitherTitleNorBodyIsEmpty()
    {
        foreach (var language in Enum.GetValues<Language>())
        {
            var (title, body) = PushNotificationContentService.GetShoppingListActivityContent(language, "List", 1);

            Assert.False(string.IsNullOrWhiteSpace(title), $"Title should not be empty for language {language}");
            Assert.False(string.IsNullOrWhiteSpace(body), $"Body should not be empty for language {language}");
        }
    }

    // -------------------------------------------------------------------------
    // ShoppingListActivityMonitorService lifecycle
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            new ShoppingListActivityMonitorService(
                new NoOpServiceScopeFactory(),
                new NoOpWebPushService()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledBeforeFirstRun_StopsGracefully()
    {
        var service = new ShoppingListActivityMonitorService(
            new NoOpServiceScopeFactory(),
            new NoOpWebPushService());

        using var cts = new CancellationTokenSource();

        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(50); // let the service enter the delay loop

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
            "Scope creation not supported in unit tests – the 5-minute delay prevents ProcessAsync from being called.");
    }
}
