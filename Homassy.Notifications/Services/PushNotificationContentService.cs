using Homassy.API.Enums;

namespace Homassy.Notifications.Services;

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

    public static (string Title, string Body) GetShoppingListItemsAddedContent(
        Language language, string listName, int count)
    {
        return language switch
        {
            Language.Hungarian => (
                "Bevásárlólista frissítve",
                count == 1
                    ? $"1 új elem került a(z) \"{listName}\" listához."
                    : $"{count} új elem került a(z) \"{listName}\" listához."
            ),
            Language.German => (
                "Einkaufsliste aktualisiert",
                count == 1
                    ? $"1 neues Element wurde zur Liste \"{listName}\" hinzugefügt."
                    : $"{count} neue Elemente wurden zur Liste \"{listName}\" hinzugefügt."
            ),
            _ => (
                "Shopping List Updated",
                count == 1
                    ? $"1 new item was added to the \"{listName}\" list."
                    : $"{count} new items were added to the \"{listName}\" list."
            )
        };
    }

    public static (string Title, string Body) GetShoppingListItemsEditedContent(
        Language language, string listName, int count)
    {
        return language switch
        {
            Language.Hungarian => (
                "Bevásárlólista frissítve",
                count == 1
                    ? $"1 elem módosult a(z) \"{listName}\" listában."
                    : $"{count} elem módosult a(z) \"{listName}\" listában."
            ),
            Language.German => (
                "Einkaufsliste aktualisiert",
                count == 1
                    ? $"1 Element in der Liste \"{listName}\" wurde geändert."
                    : $"{count} Elemente in der Liste \"{listName}\" wurden geändert."
            ),
            _ => (
                "Shopping List Updated",
                count == 1
                    ? $"1 item was edited in the \"{listName}\" list."
                    : $"{count} items were edited in the \"{listName}\" list."
            )
        };
    }

    public static (string Title, string Body) GetShoppingListItemsDeletedContent(
        Language language, string listName, int count)
    {
        return language switch
        {
            Language.Hungarian => (
                "Bevásárlólista frissítve",
                count == 1
                    ? $"1 elem törlődött a(z) \"{listName}\" listából."
                    : $"{count} elem törlődött a(z) \"{listName}\" listából."
            ),
            Language.German => (
                "Einkaufsliste aktualisiert",
                count == 1
                    ? $"1 Element wurde aus der Liste \"{listName}\" entfernt."
                    : $"{count} Elemente wurden aus der Liste \"{listName}\" entfernt."
            ),
            _ => (
                "Shopping List Updated",
                count == 1
                    ? $"1 item was removed from the \"{listName}\" list."
                    : $"{count} items were removed from the \"{listName}\" list."
            )
        };
    }

    public static (string Title, string Body) GetShoppingListItemsPurchasedContent(
        Language language, string listName, int count)
    {
        return language switch
        {
            Language.Hungarian => (
                "Bevásárlólista frissítve",
                count == 1
                    ? $"1 elem megvásárolva a(z) \"{listName}\" listáról."
                    : $"{count} elem megvásárolva a(z) \"{listName}\" listáról."
            ),
            Language.German => (
                "Einkaufsliste aktualisiert",
                count == 1
                    ? $"1 Element aus der Liste \"{listName}\" wurde gekauft."
                    : $"{count} Elemente aus der Liste \"{listName}\" wurden gekauft."
            ),
            _ => (
                "Shopping List Updated",
                count == 1
                    ? $"1 item from the \"{listName}\" list was marked as purchased."
                    : $"{count} items from the \"{listName}\" list were marked as purchased."
            )
        };
    }

    public static (string Title, string Body) GetShoppingListCreatedContent(Language language, string listName)
    {
        return language switch
        {
            Language.Hungarian => (
                "Új bevásárlólista",
                $"Új \"{listName}\" bevásárlólista jött létre a családodban."
            ),
            Language.German => (
                "Neue Einkaufsliste",
                $"Eine neue Einkaufsliste \"{listName}\" wurde in deiner Familie erstellt."
            ),
            _ => (
                "New Shopping List",
                $"A new \"{listName}\" shopping list was created in your family."
            )
        };
    }

    public static (string Title, string Body) GetShoppingListDeletedContent(Language language, string listName)
    {
        return language switch
        {
            Language.Hungarian => (
                "Bevásárlólista törölve",
                $"A(z) \"{listName}\" bevásárlólista törlődött a családodban."
            ),
            Language.German => (
                "Einkaufsliste gelöscht",
                $"Die Einkaufsliste \"{listName}\" wurde in deiner Familie gelöscht."
            ),
            _ => (
                "Shopping List Deleted",
                $"The \"{listName}\" shopping list was deleted in your family."
            )
        };
    }

    public static (string Title, string Body) GetInventoryItemsCreatedContent(Language language, int count)
    {
        return language switch
        {
            Language.Hungarian => (
                "Készlet frissítve",
                count == 1
                    ? "1 új tétel került a készletbe."
                    : $"{count} új tétel került a készletbe."
            ),
            Language.German => (
                "Bestand aktualisiert",
                count == 1
                    ? "1 neuer Eintrag wurde zum Bestand hinzugefügt."
                    : $"{count} neue Einträge wurden zum Bestand hinzugefügt."
            ),
            _ => (
                "Inventory Updated",
                count == 1
                    ? "1 new item was added to the inventory."
                    : $"{count} new items were added to the inventory."
            )
        };
    }

    public static (string Title, string Body) GetInventoryItemsUpdatedContent(Language language, int count)
    {
        return language switch
        {
            Language.Hungarian => (
                "Készlet frissítve",
                count == 1
                    ? "1 készlettétel módosult."
                    : $"{count} készlettétel módosult."
            ),
            Language.German => (
                "Bestand aktualisiert",
                count == 1
                    ? "1 Bestandseintrag wurde geändert."
                    : $"{count} Bestandseinträge wurden geändert."
            ),
            _ => (
                "Inventory Updated",
                count == 1
                    ? "1 inventory item was updated."
                    : $"{count} inventory items were updated."
            )
        };
    }

    public static (string Title, string Body) GetInventoryItemsDeletedContent(Language language, int count)
    {
        return language switch
        {
            Language.Hungarian => (
                "Készlet frissítve",
                count == 1
                    ? "1 tétel törlődött a készletből."
                    : $"{count} tétel törlődött a készletből."
            ),
            Language.German => (
                "Bestand aktualisiert",
                count == 1
                    ? "1 Eintrag wurde aus dem Bestand entfernt."
                    : $"{count} Einträge wurden aus dem Bestand entfernt."
            ),
            _ => (
                "Inventory Updated",
                count == 1
                    ? "1 item was removed from the inventory."
                    : $"{count} items were removed from the inventory."
            )
        };
    }

    public static (string Title, string Body) GetInventoryItemsConsumedContent(Language language, int count)
    {
        return language switch
        {
            Language.Hungarian => (
                "Készlet frissítve",
                count == 1
                    ? "1 tétel felhasználva a készletből."
                    : $"{count} tétel felhasználva a készletből."
            ),
            Language.German => (
                "Bestand aktualisiert",
                count == 1
                    ? "1 Eintrag wurde aus dem Bestand verbraucht."
                    : $"{count} Einträge wurden aus dem Bestand verbraucht."
            ),
            _ => (
                "Inventory Updated",
                count == 1
                    ? "1 item was consumed from the inventory."
                    : $"{count} items were consumed from the inventory."
            )
        };
    }

    public static (string Title, string Body, string ActionTitle) GetTestNotificationContent(Language language)
    {
        return language switch
        {
            Language.German => ("Homassy Testbenachrichtigung", "Dies ist eine Test-Push-Benachrichtigung von Homassy.", "Homassy öffnen"),
            Language.English => ("Homassy test notification", "This is a test push notification from Homassy.", "Open Homassy"),
            _ => ("Homassy teszt értesítés", "Ez egy teszt push értesítés a Homassytól.", "Homassy megnyitása")
        };
    }

    public static (string Title, string Body) GetAutomationNotificationContent(
        Language language, string productName, decimal quantity, string unit)
    {
        return language switch
        {
            Language.Hungarian => (
                "Automatikus felhasználás",
                $"{quantity} {unit} felhasználva a(z) \"{productName}\" termékből."
            ),
            Language.German => (
                "Automatischer Verbrauch",
                $"{quantity} {unit} von \"{productName}\" wurde verbraucht."
            ),
            _ => (
                "Automatic Consumption",
                $"{quantity} {unit} of \"{productName}\" has been consumed."
            )
        };
    }

    public static (string Title, string Body) GetAutomationReminderContent(
        Language language, string productName)
    {
        return language switch
        {
            Language.Hungarian => (
                "Felhasználási emlékeztető",
                $"Ideje felhasználni a(z) \"{productName}\" terméket."
            ),
            Language.German => (
                "Verbrauchserinnerung",
                $"Es ist Zeit, \"{productName}\" zu verwenden."
            ),
            _ => (
                "Usage Reminder",
                $"It's time to use \"{productName}\"."
            )
        };
    }

    public static (string Title, string Body) GetShoppingListAutomationContent(
        Language language, string productName, decimal quantity, string unit, string shoppingListName)
    {
        return language switch
        {
            Language.Hungarian => (
                "Bevásárlólistához adva",
                $"{quantity} {unit} \"{productName}\" hozzáadva a(z) \"{shoppingListName}\" bevásárlólistához."
            ),
            Language.German => (
                "Zur Einkaufsliste hinzugefügt",
                $"{quantity} {unit} \"{productName}\" wurde zur Einkaufsliste \"{shoppingListName}\" hinzugefügt."
            ),
            _ => (
                "Added to Shopping List",
                $"{quantity} {unit} of \"{productName}\" has been added to the \"{shoppingListName}\" shopping list."
            )
        };
    }

    public static (string Title, string Body) GetLowStockNotificationContent(
        Language language, string productName, decimal totalStock, decimal thresholdQuantity, string shoppingListName)
    {
        return language switch
        {
            Language.Hungarian => (
                "Alacsony készletszint",
                $"A(z) \"{productName}\" készlete ({totalStock}) a küszöbérték ({thresholdQuantity}) alá csökkent. Hozzáadva a(z) \"{shoppingListName}\" bevásárlólistához."
            ),
            Language.German => (
                "Niedriger Lagerbestand",
                $"Der Bestand von \"{productName}\" ({totalStock}) ist unter den Schwellenwert ({thresholdQuantity}) gefallen. Zur Einkaufsliste \"{shoppingListName}\" hinzugefügt."
            ),
            _ => (
                "Low Stock Alert",
                $"Stock of \"{productName}\" ({totalStock}) dropped below threshold ({thresholdQuantity}). Added to the \"{shoppingListName}\" shopping list."
            )
        };
    }

    /// <summary>Notification for existing family members when someone requests to join.</summary>
    public static (string Title, string Body) GetFamilyJoinRequestContent(Language language, string requesterName)
    {
        return language switch
        {
            Language.Hungarian => (
                "Új belépési kérelem",
                $"{requesterName} szeretne csatlakozni a családodhoz."
            ),
            Language.German => (
                "Neue Beitrittsanfrage",
                $"{requesterName} möchte deiner Familie beitreten."
            ),
            _ => (
                "New join request",
                $"{requesterName} wants to join your family."
            )
        };
    }

    /// <summary>Notification for the requester when their join request is approved.</summary>
    public static (string Title, string Body) GetFamilyJoinApprovedContent(Language language, string familyName)
    {
        return language switch
        {
            Language.Hungarian => (
                "Belépési kérelem elfogadva",
                $"Csatlakoztál a(z) \"{familyName}\" családhoz."
            ),
            Language.German => (
                "Beitrittsanfrage genehmigt",
                $"Du bist der Familie \"{familyName}\" beigetreten."
            ),
            _ => (
                "Join request approved",
                $"You have joined the \"{familyName}\" family."
            )
        };
    }

    /// <summary>Notification for the requester when their join request is declined.</summary>
    public static (string Title, string Body) GetFamilyJoinDeclinedContent(Language language, string familyName)
    {
        return language switch
        {
            Language.Hungarian => (
                "Belépési kérelem elutasítva",
                $"A(z) \"{familyName}\" családhoz küldött kérelmedet elutasították."
            ),
            Language.German => (
                "Beitrittsanfrage abgelehnt",
                $"Deine Anfrage, der Familie \"{familyName}\" beizutreten, wurde abgelehnt."
            ),
            _ => (
                "Join request declined",
                $"Your request to join the \"{familyName}\" family was declined."
            )
        };
    }

    /// <summary>
    /// Reminder for an event coming from a synced external (iCal) calendar. The lead time is phrased
    /// relatively ("in 15 minutes"), which stays correct for every recipient because the trigger instant
    /// is computed per recipient before this text is built.
    /// </summary>
    public static (string Title, string Body) GetCalendarEventReminderContent(
        Language language, string eventTitle, int leadTimeMinutes, bool isAllDay)
    {
        var title = language switch
        {
            Language.Hungarian => "Naptár emlékeztető",
            Language.German => "Kalendererinnerung",
            _ => "Calendar reminder"
        };

        if (isAllDay)
        {
            var body = (language, leadTimeMinutes) switch
            {
                (Language.Hungarian, 0) => $"\"{eventTitle}\" ma van.",
                (Language.Hungarian, 1440) => $"\"{eventTitle}\" holnap lesz.",
                (Language.Hungarian, _) => $"\"{eventTitle}\" {DescribeLeadTime(language, leadTimeMinutes)} múlva lesz.",
                (Language.German, 0) => $"\"{eventTitle}\" ist heute.",
                (Language.German, 1440) => $"\"{eventTitle}\" ist morgen.",
                (Language.German, _) => $"\"{eventTitle}\" ist in {DescribeLeadTime(language, leadTimeMinutes)}.",
                (_, 0) => $"\"{eventTitle}\" is today.",
                (_, 1440) => $"\"{eventTitle}\" is tomorrow.",
                _ => $"\"{eventTitle}\" is in {DescribeLeadTime(language, leadTimeMinutes)}."
            };

            return (title, body);
        }

        return (title, (language, leadTimeMinutes) switch
        {
            (Language.Hungarian, 0) => $"\"{eventTitle}\" most kezdődik.",
            (Language.Hungarian, _) => $"\"{eventTitle}\" {DescribeLeadTime(language, leadTimeMinutes)} múlva kezdődik.",
            (Language.German, 0) => $"\"{eventTitle}\" beginnt jetzt.",
            (Language.German, _) => $"\"{eventTitle}\" beginnt in {DescribeLeadTime(language, leadTimeMinutes)}.",
            (_, 0) => $"\"{eventTitle}\" is starting now.",
            _ => $"\"{eventTitle}\" starts in {DescribeLeadTime(language, leadTimeMinutes)}."
        });
    }

    /// <summary>Renders a lead time in the largest whole unit it divides into (minutes → hours → days).</summary>
    private static string DescribeLeadTime(Language language, int minutes)
    {
        if (minutes % 1440 == 0)
        {
            var days = minutes / 1440;
            return language switch
            {
                Language.Hungarian => $"{days} nap",
                Language.German => days == 1 ? "1 Tag" : $"{days} Tagen",
                _ => days == 1 ? "1 day" : $"{days} days"
            };
        }

        if (minutes % 60 == 0)
        {
            var hours = minutes / 60;
            return language switch
            {
                Language.Hungarian => $"{hours} óra",
                Language.German => hours == 1 ? "1 Stunde" : $"{hours} Stunden",
                _ => hours == 1 ? "1 hour" : $"{hours} hours"
            };
        }

        return language switch
        {
            Language.Hungarian => $"{minutes} perc",
            Language.German => minutes == 1 ? "1 Minute" : $"{minutes} Minuten",
            _ => minutes == 1 ? "1 minute" : $"{minutes} minutes"
        };
    }
}
