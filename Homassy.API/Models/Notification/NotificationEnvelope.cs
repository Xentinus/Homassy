using Homassy.API.Enums;

namespace Homassy.API.Models.Notification
{
    /// <summary>
    /// One notification, described by its type and its parameters rather than by its text (#116).
    /// </summary>
    /// <remarks>
    /// This is the shape the notification workers pass around, and it is what makes the push and
    /// the stored inbox row come from a single description. Before it, each worker built a
    /// <c>(title, body)</c> pair per recipient language and handed that straight to the push
    /// service; recording an inbox row from that would either have stored one language's prose
    /// forever or needed a second, parallel description of the same event.
    /// <para>
    /// Rendering happens twice, from the same envelope: once server-side into the push payload
    /// (in the recipient's stored language), and once client-side when the inbox row is read (in
    /// whatever language the reader is using then).
    /// </para>
    /// <para>
    /// <b>Parameter names are a contract</b> shared by
    /// <c>Homassy.Notifications.Services.NotificationContentRenderer</c> and the
    /// <c>notifications.types.*</c> blocks in the three locale files. Per type:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="NotificationType.WeeklySummary"/> - <c>count</c></item>
    /// <item>the four <c>ShoppingListItems*</c> types - <c>listName</c>, <c>count</c></item>
    /// <item><see cref="NotificationType.ShoppingListCreated"/> / <see cref="NotificationType.ShoppingListDeleted"/> - <c>listName</c></item>
    /// <item>the four <c>InventoryItems*</c> types - <c>count</c></item>
    /// <item><see cref="NotificationType.AutomationExecuted"/> - <c>productName</c>, <c>quantity</c>, <c>unit</c></item>
    /// <item><see cref="NotificationType.AutomationReminder"/> - <c>productName</c></item>
    /// <item><see cref="NotificationType.AutomationAddedToShoppingList"/> - <c>productName</c>, <c>listName</c></item>
    /// <item><see cref="NotificationType.LowStock"/> - <c>productName</c>, <c>quantity</c>, <c>unit</c>, <c>threshold</c></item>
    /// <item><see cref="NotificationType.FamilyJoinRequest"/> - <c>requesterName</c></item>
    /// <item><see cref="NotificationType.FamilyJoinApproved"/> / <see cref="NotificationType.FamilyJoinDeclined"/> - <c>familyName</c></item>
    /// <item><see cref="NotificationType.CalendarEventReminder"/> - <c>eventTitle</c>, <c>when</c></item>
    /// </list>
    /// </remarks>
    /// <param name="Type">Which notification this is.</param>
    /// <param name="Parameters">
    /// The values that vary, keyed by the names listed above. Always non-null; an empty dictionary
    /// for a type that takes none.
    /// </param>
    public readonly record struct NotificationEnvelope(
        NotificationType Type,
        IReadOnlyDictionary<string, string> Parameters)
    {
        /// <summary>Shared empty parameter set, so a no-parameter envelope allocates nothing.</summary>
        public static readonly IReadOnlyDictionary<string, string> NoParameters
            = new Dictionary<string, string>();

        /// <summary>An envelope for a type that takes no parameters.</summary>
        public NotificationEnvelope(NotificationType type)
            : this(type, NoParameters)
        {
        }
    }
}
