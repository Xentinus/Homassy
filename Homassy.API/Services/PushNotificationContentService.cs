using Homassy.API.Enums;

namespace Homassy.API.Services
{
    public static class PushNotificationContentService
    {
        public static (string Title, string Body) GetWeeklyNotificationContent(Language language, int expiringCount)
        {
            return language switch
            {
                Language.Hungarian => (
                    "Heti összefoglaló",
                    expiringCount == 0
                        ? "Nincs lejáró termék a készletedben. Szép hetet!"
                        : expiringCount == 1
                            ? "1 termék fog lejárni a következő 14 napban."
                            : $"{expiringCount} termék fog lejárni a következő 14 napban."
                ),
                Language.German => (
                    "Wochenzusammenfassung",
                    expiringCount == 0
                        ? "Keine ablaufenden Produkte in Ihrem Bestand. Schöne Woche!"
                        : expiringCount == 1
                            ? "1 Produkt läuft in den nächsten 14 Tagen ab."
                            : $"{expiringCount} Produkte laufen in den nächsten 14 Tagen ab."
                ),
                _ => (
                    "Weekly Summary",
                    expiringCount == 0
                        ? "No expiring products in your inventory. Have a great week!"
                        : expiringCount == 1
                            ? "1 product will expire within the next 14 days."
                            : $"{expiringCount} products will expire within the next 14 days."
                )
            };
        }

        public static (string Title, string Body) GetShoppingListActivityContent(Language language, string listName, int itemCount)
        {
            return language switch
            {
                Language.Hungarian => (
                    "Bevásárlólista frissítve",
                    itemCount == 1
                        ? $"1 új elem került a(z) \"{listName}\" bevásárlólistához."
                        : $"{itemCount} új elem került a(z) \"{listName}\" bevásárlólistához."
                ),
                Language.German => (
                    "Einkaufsliste aktualisiert",
                    itemCount == 1
                        ? $"1 neues Element wurde zur Einkaufsliste \"{listName}\" hinzugefügt."
                        : $"{itemCount} neue Elemente wurden zur Einkaufsliste \"{listName}\" hinzugefügt."
                ),
                _ => (
                    "Shopping List Updated",
                    itemCount == 1
                        ? $"1 new item was added to the \"{listName}\" shopping list."
                        : $"{itemCount} new items were added to the \"{listName}\" shopping list."
                )
            };
        }
    }
}
