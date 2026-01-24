using Homassy.API.Enums;

namespace Homassy.API.Services
{
    public static class PushNotificationContentService
    {
        public static (string Title, string Body) GetDailyNotificationContent(Language language, int expiringCount)
        {
            return language switch
            {
                Language.Hungarian => (
                    "Homassy - Lejárat figyelmeztetés",
                    expiringCount == 1
                        ? "1 termék hamarosan lejár vagy már lejárt a készletedben."
                        : $"{expiringCount} termék hamarosan lejár vagy már lejárt a készletedben."
                ),
                Language.German => (
                    "Homassy - Ablauferinnerung",
                    expiringCount == 1
                        ? "1 Produkt läuft bald ab oder ist bereits abgelaufen."
                        : $"{expiringCount} Produkte laufen bald ab oder sind bereits abgelaufen."
                ),
                _ => (
                    "Homassy - Expiration Reminder",
                    expiringCount == 1
                        ? "1 product is expiring soon or has already expired in your inventory."
                        : $"{expiringCount} products are expiring soon or have already expired in your inventory."
                )
            };
        }

        public static (string Title, string Body) GetWeeklyNotificationContent(Language language, int expiringCount)
        {
            return language switch
            {
                Language.Hungarian => (
                    "Homassy - Heti összefoglaló",
                    expiringCount == 0
                        ? "Nincs lejáró termék a készletedben. Szép hetet!"
                        : expiringCount == 1
                            ? "1 termék fog lejárni a következő 14 napban."
                            : $"{expiringCount} termék fog lejárni a következő 14 napban."
                ),
                Language.German => (
                    "Homassy - Wochenzusammenfassung",
                    expiringCount == 0
                        ? "Keine ablaufenden Produkte in Ihrem Bestand. Schöne Woche!"
                        : expiringCount == 1
                            ? "1 Produkt läuft in den nächsten 14 Tagen ab."
                            : $"{expiringCount} Produkte laufen in den nächsten 14 Tagen ab."
                ),
                _ => (
                    "Homassy - Weekly Summary",
                    expiringCount == 0
                        ? "No expiring products in your inventory. Have a great week!"
                        : expiringCount == 1
                            ? "1 product will expire within the next 14 days."
                            : $"{expiringCount} products will expire within the next 14 days."
                )
            };
        }
    }
}
