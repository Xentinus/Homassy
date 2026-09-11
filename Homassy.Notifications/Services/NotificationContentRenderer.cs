using Homassy.API.Enums;
using Homassy.API.Models.Notification;
using System.Globalization;

namespace Homassy.Notifications.Services;

/// <summary>
/// Renders a <see cref="NotificationEnvelope"/> into the push notification's title and body (#116).
/// </summary>
/// <remarks>
/// This is the seam that lets one description of an event produce both the push text and the
/// stored inbox row. The workers used to build <c>(title, body)</c> pairs directly and hand them
/// to the push service, which left nothing to store but prose in one language; now they build
/// envelopes, this renders them for the push, and the client renders the same envelope again -
/// from the row's type and parameters - in whatever language the reader is using when they open
/// the inbox.
/// <para>
/// <see cref="PushNotificationContentService"/> keeps every localized string; this class only
/// unpacks the envelope's parameters and dispatches to it. Deliberately no strings of its own, so
/// there is still exactly one place a notification's wording lives.
/// </para>
/// <para>
/// The parameter names are the contract documented on <see cref="NotificationEnvelope"/> and
/// mirrored by the <c>notifications.types.*</c> blocks in the three locale files. A missing
/// parameter renders as an empty string or a zero rather than throwing: a push that reads slightly
/// wrong is a better outcome than a worker iteration that dies on one malformed envelope and
/// delivers nothing to anybody.
/// </para>
/// </remarks>
public static class NotificationContentRenderer
{
    public static (string Title, string Body) Render(NotificationEnvelope envelope, Language language)
    {
        var p = envelope.Parameters;

        return envelope.Type switch
        {
            NotificationType.WeeklySummary
                => PushNotificationContentService.GetWeeklyNotificationContent(language, Int(p, "count")),

            NotificationType.ShoppingListItemsAdded
                => PushNotificationContentService.GetShoppingListItemsAddedContent(language, Str(p, "listName"), Int(p, "count")),
            NotificationType.ShoppingListItemsEdited
                => PushNotificationContentService.GetShoppingListItemsEditedContent(language, Str(p, "listName"), Int(p, "count")),
            NotificationType.ShoppingListItemsDeleted
                => PushNotificationContentService.GetShoppingListItemsDeletedContent(language, Str(p, "listName"), Int(p, "count")),
            NotificationType.ShoppingListItemsPurchased
                => PushNotificationContentService.GetShoppingListItemsPurchasedContent(language, Str(p, "listName"), Int(p, "count")),
            NotificationType.ShoppingListCreated
                => PushNotificationContentService.GetShoppingListCreatedContent(language, Str(p, "listName")),
            NotificationType.ShoppingListDeleted
                => PushNotificationContentService.GetShoppingListDeletedContent(language, Str(p, "listName")),

            NotificationType.InventoryItemsCreated
                => PushNotificationContentService.GetInventoryItemsCreatedContent(language, Int(p, "count")),
            NotificationType.InventoryItemsUpdated
                => PushNotificationContentService.GetInventoryItemsUpdatedContent(language, Int(p, "count")),
            NotificationType.InventoryItemsDeleted
                => PushNotificationContentService.GetInventoryItemsDeletedContent(language, Int(p, "count")),
            NotificationType.InventoryItemsConsumed
                => PushNotificationContentService.GetInventoryItemsConsumedContent(language, Int(p, "count")),

            NotificationType.AutomationExecuted
                => PushNotificationContentService.GetAutomationNotificationContent(
                    language, Str(p, "productName"), Dec(p, "quantity"), Str(p, "unit")),
            NotificationType.AutomationReminder
                => PushNotificationContentService.GetAutomationReminderContent(language, Str(p, "productName")),
            NotificationType.AutomationAddedToShoppingList
                => PushNotificationContentService.GetShoppingListAutomationContent(
                    language, Str(p, "productName"), Dec(p, "quantity"), Str(p, "unit"), Str(p, "listName")),
            NotificationType.LowStock
                => PushNotificationContentService.GetLowStockNotificationContent(
                    language, Str(p, "productName"), Dec(p, "quantity"), Dec(p, "threshold"), Str(p, "listName")),

            NotificationType.FamilyJoinRequest
                => PushNotificationContentService.GetFamilyJoinRequestContent(language, Str(p, "requesterName")),
            NotificationType.FamilyJoinApproved
                => PushNotificationContentService.GetFamilyJoinApprovedContent(language, Str(p, "familyName")),
            NotificationType.FamilyJoinDeclined
                => PushNotificationContentService.GetFamilyJoinDeclinedContent(language, Str(p, "familyName")),

            NotificationType.CalendarEventReminder
                => PushNotificationContentService.GetCalendarEventReminderContent(
                    language, Str(p, "eventTitle"), Int(p, "leadMinutes"), Bool(p, "isAllDay")),

            NotificationType.FamilyChatMessages
                => PushNotificationContentService.GetFamilyChatMessagesContent(
                    language, Str(p, "senderName"), Int(p, "count"), Str(p, "preview")),

            // Unreachable for any envelope built in this process — every member of the enum is
            // handled above. It exists so that adding a member without adding a case here is a
            // notification that reads oddly rather than a worker that throws mid-iteration; the
            // compiler cannot enforce exhaustiveness over an enum.
            _ => (string.Empty, string.Empty)
        };
    }

    private static string Str(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value) ? value : string.Empty;

    private static int Int(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    /// <summary>
    /// Invariant culture on both sides of the wire. A quantity written with a Hungarian decimal
    /// comma and read back with an invariant parser is off by a factor of ten or silently zero,
    /// and the worker's culture is whatever the container happens to have.
    /// </summary>
    private static decimal Dec(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value)
            && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;

    private static bool Bool(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) && parsed;
}
