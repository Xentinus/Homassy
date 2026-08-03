extern alias NotificationsProject;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using NotificationsProject::Homassy.Notifications.Services;
using NotificationsProject::Homassy.Notifications.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Homassy.Tests.Unit;

/// <summary>
/// Tests for ShoppingListActivityMonitorService and the related
/// PushNotificationContentService shopping list notification content.
/// </summary>
public class ShoppingListActivityMonitorServiceTests
{
    /// <summary>
    /// The item-level activities the monitor reports. The single
    /// GetShoppingListActivityContent method was split into one method per action; they share the
    /// same titles and differ only in the body wording, so every case below is asserted for all four.
    /// </summary>
    public enum ItemAction
    {
        Added,
        Edited,
        Deleted,
        Purchased
    }

    private static (string Title, string Body) ItemActivityContent(
        ItemAction action, Language language, string listName, int count) => action switch
        {
            ItemAction.Added => PushNotificationContentService.GetShoppingListItemsAddedContent(language, listName, count),
            ItemAction.Edited => PushNotificationContentService.GetShoppingListItemsEditedContent(language, listName, count),
            ItemAction.Deleted => PushNotificationContentService.GetShoppingListItemsDeletedContent(language, listName, count),
            ItemAction.Purchased => PushNotificationContentService.GetShoppingListItemsPurchasedContent(language, listName, count),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unhandled item action")
        };

    // -------------------------------------------------------------------------
    // PushNotificationContentService item activity content
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(Language.Hungarian, "Bevásárlólista frissítve", "Heti lista")]
    [InlineData(Language.German, "Einkaufsliste aktualisiert", "Wochenliste")]
    [InlineData(Language.English, "Shopping List Updated", "Weekly List")]
    public void ItemActivityContent_ReturnsCorrectTitle_ForLanguage(
        Language language, string expectedTitle, string listName)
    {
        foreach (var action in Enum.GetValues<ItemAction>())
        {
            var (title, _) = ItemActivityContent(action, language, listName, 1);

            Assert.Equal(expectedTitle, title);
        }
    }

    [Fact]
    public void ItemActivityContent_Hungarian_BodyContainsListName()
    {
        const string listName = "Heti bevásárlás";

        foreach (var action in Enum.GetValues<ItemAction>())
        {
            var (_, body) = ItemActivityContent(action, Language.Hungarian, listName, 3);

            Assert.Contains(listName, body);
            Assert.Contains("3", body);
        }
    }

    [Fact]
    public void ItemActivityContent_German_BodyContainsListName()
    {
        const string listName = "Wocheneinkauf";

        foreach (var action in Enum.GetValues<ItemAction>())
        {
            var (_, body) = ItemActivityContent(action, Language.German, listName, 2);

            Assert.Contains(listName, body);
            Assert.Contains("2", body);
        }
    }

    [Fact]
    public void ItemActivityContent_English_BodyContainsListName()
    {
        const string listName = "Weekly Shopping";

        foreach (var action in Enum.GetValues<ItemAction>())
        {
            var (_, body) = ItemActivityContent(action, Language.English, listName, 5);

            Assert.Contains(listName, body);
            Assert.Contains("5", body);
        }
    }

    [Fact]
    public void ItemActivityContent_SingleItem_UsesSingularForm()
    {
        foreach (var action in Enum.GetValues<ItemAction>())
        {
            var (_, bodyHu) = ItemActivityContent(action, Language.Hungarian, "Lista", 1);
            var (_, bodyDe) = ItemActivityContent(action, Language.German, "Liste", 1);
            var (_, bodyEn) = ItemActivityContent(action, Language.English, "List", 1);

            Assert.StartsWith("1 ", bodyHu);
            Assert.StartsWith("1 ", bodyDe);
            Assert.StartsWith("1 ", bodyEn);
        }
    }

    [Fact]
    public void ItemActivityContent_MultipleItems_UsesCount()
    {
        foreach (var action in Enum.GetValues<ItemAction>())
        {
            var (_, bodyHu) = ItemActivityContent(action, Language.Hungarian, "Lista", 4);
            var (_, bodyDe) = ItemActivityContent(action, Language.German, "Liste", 4);
            var (_, bodyEn) = ItemActivityContent(action, Language.English, "List", 4);

            Assert.StartsWith("4 ", bodyHu);
            Assert.StartsWith("4 ", bodyDe);
            Assert.StartsWith("4 ", bodyEn);
        }
    }

    [Fact]
    public void ItemActivityContent_UnknownLanguage_FallsBackToEnglish()
    {
        const Language unknownLanguage = (Language)999;
        const string listName = "Test List";

        foreach (var action in Enum.GetValues<ItemAction>())
        {
            var (title, body) = ItemActivityContent(action, unknownLanguage, listName, 2);

            Assert.Equal("Shopping List Updated", title);
            Assert.Contains(listName, body);
            Assert.Contains("2", body);
        }
    }

    [Fact]
    public void ItemActivityContent_NeitherTitleNorBodyIsEmpty()
    {
        foreach (var action in Enum.GetValues<ItemAction>())
        {
            foreach (var language in Enum.GetValues<Language>())
            {
                var (title, body) = ItemActivityContent(action, language, "List", 1);

                Assert.False(string.IsNullOrWhiteSpace(title), $"Title should not be empty for {action}/{language}");
                Assert.False(string.IsNullOrWhiteSpace(body), $"Body should not be empty for {action}/{language}");
            }
        }
    }

    // -------------------------------------------------------------------------
    // List created / deleted content (the monitor's other two notifications)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(Language.Hungarian, "Új bevásárlólista")]
    [InlineData(Language.German, "Neue Einkaufsliste")]
    [InlineData(Language.English, "New Shopping List")]
    public void GetShoppingListCreatedContent_ReturnsCorrectTitle_AndBodyContainsListName(
        Language language, string expectedTitle)
    {
        const string listName = "Heti lista";

        var (title, body) = PushNotificationContentService.GetShoppingListCreatedContent(language, listName);

        Assert.Equal(expectedTitle, title);
        Assert.Contains(listName, body);
    }

    [Theory]
    [InlineData(Language.Hungarian, "Bevásárlólista törölve")]
    [InlineData(Language.German, "Einkaufsliste gelöscht")]
    [InlineData(Language.English, "Shopping List Deleted")]
    public void GetShoppingListDeletedContent_ReturnsCorrectTitle_AndBodyContainsListName(
        Language language, string expectedTitle)
    {
        const string listName = "Heti lista";

        var (title, body) = PushNotificationContentService.GetShoppingListDeletedContent(language, listName);

        Assert.Equal(expectedTitle, title);
        Assert.Contains(listName, body);
    }

    [Fact]
    public void GetShoppingListCreatedAndDeletedContent_UnknownLanguage_FallsBackToEnglish()
    {
        const Language unknownLanguage = (Language)999;

        var (createdTitle, _) = PushNotificationContentService.GetShoppingListCreatedContent(unknownLanguage, "List");
        var (deletedTitle, _) = PushNotificationContentService.GetShoppingListDeletedContent(unknownLanguage, "List");

        Assert.Equal("New Shopping List", createdTitle);
        Assert.Equal("Shopping List Deleted", deletedTitle);
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
                new FamilyPushNotifier(new NoOpWebPushService())));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledBeforeFirstRun_StopsGracefully()
    {
        var service = new ShoppingListActivityMonitorService(
            new NoOpServiceScopeFactory(),
            new FamilyPushNotifier(new NoOpWebPushService()));

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
