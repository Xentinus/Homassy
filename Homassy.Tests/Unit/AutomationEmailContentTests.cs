extern alias EmailProject;
using EmailLanguage = EmailProject::Homassy.Email.Enums.Language;
using EmailProject::Homassy.Email.Services;

namespace Homassy.Tests.Unit;

/// <summary>
/// Tests for EmailContentService automation notification content methods.
/// Verifies localized content in HU/EN/DE for all automation action types.
/// </summary>
public class AutomationEmailContentTests
{
    private readonly IEmailContentService _contentService = new EmailContentService();

    // =========================================================================
    // GetAutomationSubject
    // =========================================================================

    [Theory]
    [InlineData(EmailLanguage.Hungarian, "auto_consume", "Automatikus felhasználás")]
    [InlineData(EmailLanguage.German, "auto_consume", "Automatischer Verbrauch")]
    [InlineData(EmailLanguage.English, "auto_consume", "Automatic Consumption")]
    [InlineData(EmailLanguage.Hungarian, "notify_only", "Felhasználási emlékeztető")]
    [InlineData(EmailLanguage.German, "notify_only", "Verbrauchserinnerung")]
    [InlineData(EmailLanguage.English, "notify_only", "Usage Reminder")]
    [InlineData(EmailLanguage.Hungarian, "insufficient_quantity", "Elégtelen készlet")]
    [InlineData(EmailLanguage.German, "insufficient_quantity", "Unzureichender Bestand")]
    [InlineData(EmailLanguage.English, "insufficient_quantity", "Insufficient Inventory")]
    [InlineData(EmailLanguage.Hungarian, "add_to_shopping_list", "Bevásárlólistához adva")]
    [InlineData(EmailLanguage.German, "add_to_shopping_list", "Zur Einkaufsliste hinzugefügt")]
    [InlineData(EmailLanguage.English, "add_to_shopping_list", "Added to Shopping List")]
    public void GetAutomationSubject_ReturnsCorrectSubject(EmailLanguage language, string actionType, string expectedSubstring)
    {
        var subject = _contentService.GetAutomationSubject(language, actionType);

        Assert.Contains(expectedSubstring, subject);
    }

    [Fact]
    public void GetAutomationSubject_AllCombinations_NonEmpty()
    {
        var actionTypes = new[] { "auto_consume", "notify_only", "insufficient_quantity", "add_to_shopping_list" };

        foreach (var lang in Enum.GetValues<EmailLanguage>())
        foreach (var action in actionTypes)
        {
            var subject = _contentService.GetAutomationSubject(lang, action);
            Assert.False(string.IsNullOrWhiteSpace(subject),
                $"Subject should not be empty for {lang}/{action}");
        }
    }

    // =========================================================================
    // GetAutomationGreeting
    // =========================================================================

    [Theory]
    [InlineData(EmailLanguage.Hungarian, "auto_consume", "Automatikus felhasználás")]
    [InlineData(EmailLanguage.German, "auto_consume", "Automatischer Verbrauch")]
    [InlineData(EmailLanguage.English, "auto_consume", "Automatic Consumption")]
    [InlineData(EmailLanguage.Hungarian, "notify_only", "Felhasználási emlékeztető")]
    [InlineData(EmailLanguage.German, "notify_only", "Verbrauchserinnerung")]
    [InlineData(EmailLanguage.English, "notify_only", "Usage Reminder")]
    [InlineData(EmailLanguage.Hungarian, "add_to_shopping_list", "Bevásárlólistához adva")]
    [InlineData(EmailLanguage.German, "add_to_shopping_list", "Zur Einkaufsliste hinzugefügt")]
    [InlineData(EmailLanguage.English, "add_to_shopping_list", "Added to Shopping List")]
    public void GetAutomationGreeting_ReturnsCorrectGreeting(EmailLanguage language, string actionType, string expected)
    {
        var greeting = _contentService.GetAutomationGreeting(language, actionType);

        Assert.Equal(expected, greeting);
    }

    // =========================================================================
    // GetAutomationMessage
    // =========================================================================

    [Theory]
    [InlineData(EmailLanguage.Hungarian)]
    [InlineData(EmailLanguage.German)]
    [InlineData(EmailLanguage.English)]
    public void GetAutomationMessage_AutoConsume_ContainsProductAndQuantity(EmailLanguage language)
    {
        var message = _contentService.GetAutomationMessage(
            language, "TestUser", "Mosószer", "auto_consume", 2.5m, "liter");

        Assert.Contains("Mosószer", message);
        Assert.Contains("2.5", message);
        Assert.Contains("liter", message);
    }

    [Theory]
    [InlineData(EmailLanguage.Hungarian)]
    [InlineData(EmailLanguage.German)]
    [InlineData(EmailLanguage.English)]
    public void GetAutomationMessage_NotifyOnly_ContainsProductName(EmailLanguage language)
    {
        var message = _contentService.GetAutomationMessage(
            language, "TestUser", "Vitamin", "notify_only", null, null);

        Assert.Contains("Vitamin", message);
    }

    [Theory]
    [InlineData(EmailLanguage.Hungarian)]
    [InlineData(EmailLanguage.German)]
    [InlineData(EmailLanguage.English)]
    public void GetAutomationMessage_InsufficientQuantity_ContainsProductName(EmailLanguage language)
    {
        var message = _contentService.GetAutomationMessage(
            language, null, "Cleaning Product", "insufficient_quantity", null, null);

        Assert.Contains("Cleaning Product", message);
    }

    [Fact]
    public void GetAutomationMessage_WithName_IncludesGreeting()
    {
        var message = _contentService.GetAutomationMessage(
            EmailLanguage.English, "Alice", "Detergent", "auto_consume", 1m, "piece");

        Assert.Contains("Hello, Alice!", message);
    }

    [Fact]
    public void GetAutomationMessage_WithName_Hungarian_IncludesGreeting()
    {
        var message = _contentService.GetAutomationMessage(
            EmailLanguage.Hungarian, "Béla", "Mosószer", "auto_consume", 1m, "db");

        Assert.Contains("Szia, Béla!", message);
    }

    [Fact]
    public void GetAutomationMessage_WithName_German_IncludesGreeting()
    {
        var message = _contentService.GetAutomationMessage(
            EmailLanguage.German, "Hans", "Waschmittel", "auto_consume", 1m, "Stück");

        Assert.Contains("Hallo, Hans!", message);
    }

    [Fact]
    public void GetAutomationMessage_NullName_NoGreetingPrefix()
    {
        var message = _contentService.GetAutomationMessage(
            EmailLanguage.English, null, "Detergent", "notify_only", null, null);

        Assert.DoesNotContain("Hello,", message);
    }

    [Fact]
    public void GetAutomationMessage_EmptyName_NoGreetingPrefix()
    {
        var message = _contentService.GetAutomationMessage(
            EmailLanguage.English, "", "Detergent", "notify_only", null, null);

        Assert.DoesNotContain("Hello,", message);
    }

    // =========================================================================
    // GetAutomationMessage - AddToShoppingList  
    // =========================================================================

    [Theory]
    [InlineData(EmailLanguage.Hungarian)]
    [InlineData(EmailLanguage.German)]
    [InlineData(EmailLanguage.English)]
    public void GetAutomationMessage_AddToShoppingList_ContainsProductAndQuantity(EmailLanguage language)
    {
        var message = _contentService.GetAutomationMessage(
            language, "TestUser", "Milk", "add_to_shopping_list", 2m, "liter");

        Assert.Contains("Milk", message);
        Assert.Contains("2", message);
        Assert.Contains("liter", message);
    }

    [Fact]
    public void GetAutomationMessage_AddToShoppingList_WithName_IncludesGreeting()
    {
        var message = _contentService.GetAutomationMessage(
            EmailLanguage.English, "Alice", "Bread", "add_to_shopping_list", 1m, "piece");

        Assert.Contains("Hello, Alice!", message);
        Assert.Contains("Bread", message);
    }

    // =========================================================================
    // GetAutomationPlainText
    // =========================================================================

    [Theory]
    [InlineData(EmailLanguage.Hungarian)]
    [InlineData(EmailLanguage.German)]
    [InlineData(EmailLanguage.English)]
    public void GetAutomationPlainText_ContainsProductName(EmailLanguage language)
    {
        var text = _contentService.GetAutomationPlainText(
            language, "User", "TestProduct", "auto_consume", 1m, "unit");

        Assert.Contains("TestProduct", text);
    }

    [Fact]
    public void GetAutomationPlainText_ContainsFooter()
    {
        var text = _contentService.GetAutomationPlainText(
            EmailLanguage.English, null, "Product", "notify_only", null, null);

        Assert.Contains("Homassy", text);
        Assert.Contains("automated message", text);
    }

    [Fact]
    public void GetAutomationPlainText_NonEmpty_ForAllCombinations()
    {
        var actionTypes = new[] { "auto_consume", "notify_only", "insufficient_quantity", "add_to_shopping_list" };

        foreach (var lang in Enum.GetValues<EmailLanguage>())
        foreach (var action in actionTypes)
        {
            var text = _contentService.GetAutomationPlainText(
                lang, "User", "Product", action, 1m, "unit");

            Assert.False(string.IsNullOrWhiteSpace(text),
                $"Plain text should not be empty for {lang}/{action}");
        }
    }

    // =========================================================================
    // ParseLanguage
    // =========================================================================

    [Theory]
    [InlineData("hu", EmailLanguage.Hungarian)]
    [InlineData("de", EmailLanguage.German)]
    [InlineData("en", EmailLanguage.English)]
    [InlineData(null, EmailLanguage.English)]
    [InlineData("fr", EmailLanguage.English)]
    [InlineData("", EmailLanguage.English)]
    public void ParseLanguage_ReturnsCorrectLanguage(string? code, EmailLanguage expected)
    {
        var result = _contentService.ParseLanguage(code);
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // Edge cases
    // =========================================================================

    [Fact]
    public void GetAutomationMessage_SpecialCharsInProductName_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            _contentService.GetAutomationMessage(
                EmailLanguage.English, "User", "Product \"special\" <100%>", "auto_consume", 1m, "unit"));

        Assert.Null(ex);
    }

    [Fact]
    public void GetAutomationMessage_UnknownActionType_FallsBackToNotifyOnly()
    {
        var message = _contentService.GetAutomationMessage(
            EmailLanguage.English, null, "Product", "unknown_type", null, null);

        // The default case is the notify-only message
        Assert.Contains("Product", message);
    }
}
