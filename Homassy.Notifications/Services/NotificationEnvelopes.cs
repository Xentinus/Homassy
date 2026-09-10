using Homassy.API.Enums;
using Homassy.API.Models.Notification;
using System.Globalization;

namespace Homassy.Notifications.Services;

/// <summary>
/// Builds the <see cref="NotificationEnvelope"/> for each kind of notification the workers emit
/// (#116).
/// </summary>
/// <remarks>
/// A factory rather than dictionary literals at the ten-odd call sites, because the parameter
/// names are a contract with two readers that cannot see the call site: the push renderer
/// (<see cref="NotificationContentRenderer"/>) and the client's <c>notifications.types.*</c>
/// templates. A typo'd key at a call site would compile, deliver a push, store a row, and only
/// show up as a notification with a blank in the middle of it.
/// <para>
/// Numbers are formatted with <see cref="CultureInfo.InvariantCulture"/>, matching the parser on
/// the reading side. Without that, a container running under a Hungarian culture would write
/// <c>1,5</c> where every reader expects <c>1.5</c>.
/// </para>
/// </remarks>
public static class NotificationEnvelopes
{
    public static NotificationEnvelope WeeklySummary(int expiringCount)
        => new(NotificationType.WeeklySummary, new Dictionary<string, string>
        {
            ["count"] = Num(expiringCount)
        });

    public static NotificationEnvelope ShoppingListItems(NotificationType type, string listName, int count)
        => new(type, new Dictionary<string, string>
        {
            ["listName"] = listName,
            ["count"] = Num(count)
        });

    public static NotificationEnvelope ShoppingList(NotificationType type, string listName)
        => new(type, new Dictionary<string, string>
        {
            ["listName"] = listName
        });

    public static NotificationEnvelope InventoryItems(NotificationType type, int count)
        => new(type, new Dictionary<string, string>
        {
            ["count"] = Num(count)
        });

    public static NotificationEnvelope AutomationExecuted(string productName, decimal quantity, string unit)
        => new(NotificationType.AutomationExecuted, new Dictionary<string, string>
        {
            ["productName"] = productName,
            ["quantity"] = Num(quantity),
            ["unit"] = unit
        });

    public static NotificationEnvelope AutomationReminder(string productName)
        => new(NotificationType.AutomationReminder, new Dictionary<string, string>
        {
            ["productName"] = productName
        });

    public static NotificationEnvelope AutomationAddedToShoppingList(
        string productName, decimal quantity, string unit, string listName)
        => new(NotificationType.AutomationAddedToShoppingList, new Dictionary<string, string>
        {
            ["productName"] = productName,
            ["quantity"] = Num(quantity),
            ["unit"] = unit,
            ["listName"] = listName
        });

    public static NotificationEnvelope LowStock(
        string productName, decimal totalStock, decimal threshold, string listName)
        => new(NotificationType.LowStock, new Dictionary<string, string>
        {
            ["productName"] = productName,
            ["quantity"] = Num(totalStock),
            ["threshold"] = Num(threshold),
            ["listName"] = listName
        });

    public static NotificationEnvelope FamilyJoinRequest(string requesterName)
        => new(NotificationType.FamilyJoinRequest, new Dictionary<string, string>
        {
            ["requesterName"] = requesterName
        });

    public static NotificationEnvelope FamilyJoinDecision(bool approved, string familyName)
        => new(approved ? NotificationType.FamilyJoinApproved : NotificationType.FamilyJoinDeclined,
            new Dictionary<string, string>
            {
                ["familyName"] = familyName
            });

    public static NotificationEnvelope CalendarEventReminder(string eventTitle, int leadMinutes, bool isAllDay)
        => new(NotificationType.CalendarEventReminder, new Dictionary<string, string>
        {
            ["eventTitle"] = eventTitle,
            ["leadMinutes"] = Num(leadMinutes),
            // Lower-cased so both `bool.TryParse` and JavaScript's `=== 'true'` read it.
            ["isAllDay"] = isAllDay ? "true" : "false"
        });

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Num(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
