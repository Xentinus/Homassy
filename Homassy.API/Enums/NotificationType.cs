namespace Homassy.API.Enums
{
    /// <summary>
    /// What a stored <see cref="Entities.User.UserNotification"/> is about (#116).
    /// </summary>
    /// <remarks>
    /// One member per notification the workers in <c>Homassy.Notifications</c> can emit, so the
    /// type plus the row's parameters is enough to render the text at read time in whatever
    /// language the reader is using then - which is the whole reason the rows do not store
    /// pre-rendered strings. Every member has a matching
    /// <c>notifications.types.&lt;name&gt;.{title,body}</c> pair in each of the three locale files,
    /// and <c>NotificationEnvelope</c>'s XML docs name the parameters each one expects.
    /// <para>
    /// <b>Numbering is permanent.</b> The value is what the <c>Type</c> column persists, so a
    /// renumbered member silently reinterprets every existing row. Organised into thematic blocks
    /// with gaps (expiration 1-9, shopping list 10-19, inventory 20-29, automation 30-39, family
    /// 40-49, calendar 50-59); add a new member inside its block using a free number, never by
    /// appending to the end.
    /// </para>
    /// <para>
    /// The test notification is deliberately absent. It is a diagnostic - "did push reach this
    /// device" - not information about the household, and an inbox that fills up with test rows
    /// tells the user nothing.
    /// </para>
    /// </remarks>
    public enum NotificationType
    {
        // Expiration / summaries
        WeeklySummary = 1,

        // Shopping list
        ShoppingListItemsAdded = 10,
        ShoppingListItemsEdited = 11,
        ShoppingListItemsDeleted = 12,
        ShoppingListItemsPurchased = 13,
        ShoppingListCreated = 14,
        ShoppingListDeleted = 15,

        // Inventory (Készletek)
        InventoryItemsCreated = 20,
        InventoryItemsUpdated = 21,
        InventoryItemsDeleted = 22,
        InventoryItemsConsumed = 23,

        // Item automation
        AutomationExecuted = 30,
        AutomationReminder = 31,
        AutomationAddedToShoppingList = 32,
        LowStock = 33,

        // Family
        FamilyJoinRequest = 40,
        FamilyJoinApproved = 41,
        FamilyJoinDeclined = 42,

        /// <summary>
        /// New messages in the family chat (#149) - one notification per sender per burst, never
        /// one per message, which is why the parameters carry a count.
        /// </summary>
        FamilyChatMessages = 43,

        // External calendar
        CalendarEventReminder = 50
    }
}
