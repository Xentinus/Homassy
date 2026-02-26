using Homassy.Email.Enums;
using Homassy.Email.Models;

namespace Homassy.Email.Services;

public sealed class EmailContentService : IEmailContentService
{
    private static int CurrentYear => DateTime.UtcNow.Year;

    // ─── Subject ────────────────────────────────────────────────────────────────

    public string GetSubject(EmailType type, Language language) => type switch
    {
        EmailType.LoginCode => language switch
        {
            Language.Hungarian => "Ellenőrző kód",
            Language.German => "Verifizierungscode",
            _ => "Verification Code"
        },
        EmailType.RegistrationCode => language switch
        {
            Language.Hungarian => "Üdvözöljük a Homassy-ban - Fejezze be a regisztrációt",
            Language.German => "Willkommen bei Homassy - Schließen Sie Ihre Registrierung ab",
            _ => "Welcome to Homassy - Complete Your Registration"
        },
        EmailType.VerificationCode => language switch
        {
            Language.Hungarian => "Email cím megerősítése",
            Language.German => "E-Mail-Adresse bestätigen",
            _ => "Verify Email Address"
        },
        EmailType.RecoveryCode => language switch
        {
            Language.Hungarian => "Fiók visszaállítása",
            Language.German => "Konto wiederherstellen",
            _ => "Account Recovery"
        },
        _ => "Homassy"
    };

    // ─── Greeting ───────────────────────────────────────────────────────────────

    public string GetGreeting(EmailType type, Language language) => type switch
    {
        EmailType.LoginCode => language switch
        {
            Language.Hungarian => "Üdvözöljük újra!",
            Language.German => "Willkommen zurück!",
            _ => "Welcome back!"
        },
        EmailType.RegistrationCode => language switch
        {
            Language.Hungarian => "Üdvözöljük!",
            Language.German => "Willkommen!",
            _ => "Welcome!"
        },
        EmailType.VerificationCode => language switch
        {
            Language.Hungarian => "Erősítse meg email címét!",
            Language.German => "Bestätigen Sie Ihre E-Mail-Adresse!",
            _ => "Verify your email address!"
        },
        EmailType.RecoveryCode => language switch
        {
            Language.Hungarian => "Fiókja visszaállítása",
            Language.German => "Ihr Konto wiederherstellen",
            _ => "Recover your account"
        },
        _ => "Homassy"
    };

    // ─── Message ─────────────────────────────────────────────────────────────────

    public string GetMessage(EmailType type, Language language, string? name) => type switch
    {
        EmailType.LoginCode => language switch
        {
            Language.Hungarian => "Bejelentkezési kérelmet kaptunk a Homassy fiókjához. A hitelesítés befejezéséhez használja az alábbi ellenőrző kódot.",
            Language.German => "Wir haben eine Anmeldeanfrage für Ihr Homassy-Konto erhalten. Verwenden Sie den folgenden Bestätigungscode, um Ihre Authentifizierung abzuschließen.",
            _ => "We received a request to sign in to your Homassy account. Use the verification code below to complete your authentication."
        },
        EmailType.RegistrationCode => language switch
        {
            Language.Hungarian => $"Gratulálunk! Sikeresen regisztrált a Homassy-ba.",
            Language.German => $"Herzlichen Glückwunsch! Sie haben sich erfolgreich bei Homassy registriert.",
            _ => $"Congratulations! You have successfully registered to Homassy."
        },
        EmailType.VerificationCode => language switch
        {
            Language.Hungarian => "A fiókja aktiválásához erősítse meg email címét a lenti kóddal.",
            Language.German => "Aktivieren Sie Ihr Konto mit dem folgenden Code.",
            _ => "Use the code below to verify your email address and activate your account."
        },
        EmailType.RecoveryCode => language switch
        {
            Language.Hungarian => "Kaptunk egy visszaállítási kérelmet a Homassy fiókjához. Használja az alábbi kódot a folyamat befejezéséhez.",
            Language.German => "Wir haben eine Wiederherstellungsanfrage für Ihr Homassy-Konto erhalten. Verwenden Sie den folgenden Code, um den Vorgang abzuschließen.",
            _ => "We received a recovery request for your Homassy account. Use the code below to complete the process."
        },
        _ => string.Empty
    };

    // ─── Code Label ──────────────────────────────────────────────────────────────

    public string GetCodeLabel(EmailType type, Language language) => type switch
    {
        EmailType.LoginCode => language switch
        {
            Language.Hungarian => "Ellenőrző kód",
            Language.German => "Verifizierungscode",
            _ => "Verification Code"
        },
        EmailType.RegistrationCode => language switch
        {
            Language.Hungarian => "Ellenőrző kód",
            Language.German => "Bestätigungscode",
            _ => "Verification Code"
        },
        EmailType.VerificationCode => language switch
        {
            Language.Hungarian => "Megerősítő kód",
            Language.German => "Bestätigungscode",
            _ => "Verification Code"
        },
        EmailType.RecoveryCode => language switch
        {
            Language.Hungarian => "Visszaállítási kód",
            Language.German => "Wiederherstellungscode",
            _ => "Recovery Code"
        },
        _ => "Code"
    };

    // ─── Expires At ──────────────────────────────────────────────────────────────

    public string GetExpiresAt(Language language, int expiresInMinutes) => language switch
    {
        Language.Hungarian => $"Lejár: {expiresInMinutes} perc múlva",
        Language.German => $"Läuft ab in {expiresInMinutes} Minuten",
        _ => $"Expires in {expiresInMinutes} minutes"
    };

    // ─── Security Note ───────────────────────────────────────────────────────────

    public string GetSecurityNote(EmailType type, Language language) => type switch
    {
        EmailType.LoginCode => language switch
        {
            Language.Hungarian => "Ez az ellenőrző kód egyedi a bejelentkezési munkamenetéhez, és nem szabad megosztani senkivel.",
            Language.German => "Dieser Bestätigungscode ist einzigartig für Ihre Anmeldesitzung und sollte mit niemandem geteilt werden.",
            _ => "This verification code is unique to your login session and should not be shared with anyone."
        },
        EmailType.RegistrationCode => language switch
        {
            Language.Hungarian => "Ez az ellenőrző kód egyedi a regisztrációs munkamenetéhez, és nem szabad megosztani senkivel.",
            Language.German => "Dieser Bestätigungscode ist einzigartig für Ihre Registrierungssitzung und sollte mit niemandem geteilt werden.",
            _ => "This verification code is unique to your registration session and should not be shared with anyone."
        },
        EmailType.VerificationCode => language switch
        {
            Language.Hungarian => "Ez a megerősítő kód az Ön fiókjához tartozik, ne ossza meg senkivel.",
            Language.German => "Dieser Bestätigungscode gehört zu Ihrem Konto und sollte nicht weitergegeben werden.",
            _ => "This verification code belongs to your account and should not be shared with anyone."
        },
        EmailType.RecoveryCode => language switch
        {
            Language.Hungarian => "Ha nem Ön kérte a visszaállítást, hagyja figyelmen kívül ezt az üzenetet, és fiókja biztonságban marad.",
            Language.German => "Wenn Sie diese Anfrage nicht gestellt haben, ignorieren Sie diese E-Mail. Ihr Konto bleibt sicher.",
            _ => "If you did not request this, ignore this email. Your account remains secure."
        },
        _ => string.Empty
    };

    // ─── Footer ──────────────────────────────────────────────────────────────────

    public string GetFooterCopyright(Language language) => language switch
    {
        Language.Hungarian => $"© {CurrentYear} Homassy. Minden jog fenntartva.",
        Language.German => $"© {CurrentYear} Homassy. Alle Rechte vorbehalten.",
        _ => $"© {CurrentYear} Homassy. All rights reserved."
    };

    public string GetFooterAutoMessage(Language language) => language switch
    {
        Language.Hungarian => "Ez egy automatikus üzenet, kérjük ne válaszoljon erre az e-mailre.",
        Language.German => "Dies ist eine automatische Nachricht, bitte antworten Sie nicht auf diese E-Mail.",
        _ => "This is an automated message, please do not reply to this email."
    };

    // ─── Plain Text ──────────────────────────────────────────────────────────────

    public string GetPlainText(EmailType type, Language language, string code, string expiresAt) => type switch
    {
        EmailType.LoginCode => language switch
        {
            Language.Hungarian => $"Üdvözöljük a Homassy-ban!\n\nAz Ön ellenőrző kódja: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Otthoni Raktárkezelő Rendszer",
            Language.German => $"Willkommen bei Homassy!\n\nIhr Bestätigungscode lautet: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Hauslager-Verwaltungssystem",
            _ => $"Welcome to Homassy!\n\nYour verification code is: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Home Storage Management System"
        },
        EmailType.RegistrationCode => language switch
        {
            Language.Hungarian => $"Üdvözöljük a Homassy-ban!\n\nGratulálunk! Sikeresen regisztrált.\n\nA regisztráció befejezéséhez jelentkezzen be ezzel a kóddal: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Otthoni Raktárkezelő Rendszer",
            Language.German => $"Willkommen bei Homassy!\n\nHerzlichen Glückwunsch! Sie haben sich erfolgreich registriert.\n\nUm Ihre Registrierung abzuschließen, melden Sie sich bitte mit diesem Code an: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Hauslager-Verwaltungssystem",
            _ => $"Welcome to Homassy!\n\nCongratulations! You have successfully registered.\n\nTo complete your registration, please use this code: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Home Storage Management System"
        },
        EmailType.VerificationCode => language switch
        {
            Language.Hungarian => $"Homassy - Email cím megerősítése\n\nErősítse meg email címét az alábbi kóddal: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Otthoni Raktárkezelő Rendszer",
            Language.German => $"Homassy - E-Mail-Adresse bestätigen\n\nBestätigen Sie Ihre E-Mail-Adresse mit folgendem Code: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Hauslager-Verwaltungssystem",
            _ => $"Homassy - Verify Email Address\n\nVerify your email address using the following code: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Home Storage Management System"
        },
        EmailType.RecoveryCode => language switch
        {
            Language.Hungarian => $"Homassy - Fiók visszaállítása\n\nA visszaállítási kódja: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Otthoni Raktárkezelő Rendszer",
            Language.German => $"Homassy - Konto wiederherstellen\n\nIhr Wiederherstellungscode lautet: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Hauslager-Verwaltungssystem",
            _ => $"Homassy - Account Recovery\n\nYour recovery code is: {code}\n\n{expiresAt}.\n\n© {CurrentYear} Homassy - Home Storage Management System"
        },
        _ => string.Empty
    };

    // ─── Language Parser ─────────────────────────────────────────────────────────

    public Language ParseLanguage(string? code) => code?.ToLowerInvariant() switch
    {
        "hu" => Language.Hungarian,
        "de" => Language.German,
        _ => Language.English
    };

    // ─── Weekly Summary ──────────────────────────────────────────────────────────

    public string GetWeeklySummarySubject(Language language) => language switch
    {
        Language.Hungarian => "Homassy - Heti összefoglaló",
        Language.German => "Homassy - Wochenzusammenfassung",
        _ => "Homassy - Weekly Summary"
    };

    public string GetWeeklySummaryGreeting(Language language) => language switch
    {
        Language.Hungarian => "Heti összefoglaló",
        Language.German => "Wochenzusammenfassung",
        _ => "Weekly Summary"
    };

    public string GetWeeklySummaryMessage(Language language, string? name)
    {
        var greeting = string.IsNullOrWhiteSpace(name) ? string.Empty : language switch
        {
            Language.Hungarian => $"Szia, {name}! ",
            Language.German => $"Hallo, {name}! ",
            _ => $"Hello, {name}! "
        };

        return greeting + language switch
        {
            Language.Hungarian => "Íme a készletedben lévő lejárt és hamarosan lejáró termékek heti összefoglalója.",
            Language.German => "Hier ist die wöchentliche Zusammenfassung der abgelaufenen und bald ablaufenden Produkte in Ihrem Inventar.",
            _ => "Here is your weekly summary of expired and soon-to-expire products in your inventory."
        };
    }

    public string GetExpiredSectionHeader(Language language) => language switch
    {
        Language.Hungarian => "⚠️ Lejárt termékek",
        Language.German => "⚠️ Abgelaufene Produkte",
        _ => "⚠️ Expired Products"
    };

    public string GetExpiringSoonSectionHeader(Language language) => language switch
    {
        Language.Hungarian => "🕐 Hamarosan lejáró termékek",
        Language.German => "🕐 Bald ablaufende Produkte",
        _ => "🕐 Expiring Soon"
    };

    public string GetNoExpiringItemsMessage(Language language) => language switch
    {
        Language.Hungarian => "✅ Nincs lejárt vagy hamarosan lejáró terméked — szép hetet!",
        Language.German => "✅ Keine abgelaufenen oder bald ablaufenden Produkte — schöne Woche!",
        _ => "✅ No expired or expiring products — have a great week!"
    };

    public string GetWeeklySummaryPlainText(Language language, string? name, ExpiringProductDto[] expiredItems, ExpiringProductDto[] expiringSoonItems)
    {
        var sb = new System.Text.StringBuilder();

        var greeting = string.IsNullOrWhiteSpace(name)
            ? GetWeeklySummaryGreeting(language)
            : language switch
            {
                Language.Hungarian => $"Szia, {name}!",
                Language.German => $"Hallo, {name}!",
                _ => $"Hello, {name}!"
            };

        sb.AppendLine(greeting);
        sb.AppendLine();

        if (expiredItems.Length == 0 && expiringSoonItems.Length == 0)
        {
            sb.AppendLine(GetNoExpiringItemsMessage(language));
        }
        else
        {
            if (expiredItems.Length > 0)
            {
                sb.AppendLine(GetExpiredSectionHeader(language));
                foreach (var item in expiredItems)
                {
                    var brand = string.IsNullOrWhiteSpace(item.Brand) ? string.Empty : $" ({item.Brand})";
                    sb.AppendLine($"- {item.Name}{brand}: {item.ExpirationDate:yyyy-MM-dd}");
                }
                sb.AppendLine();
            }

            if (expiringSoonItems.Length > 0)
            {
                sb.AppendLine(GetExpiringSoonSectionHeader(language));
                foreach (var item in expiringSoonItems)
                {
                    var brand = string.IsNullOrWhiteSpace(item.Brand) ? string.Empty : $" ({item.Brand})";
                    sb.AppendLine($"- {item.Name}{brand}: {item.ExpirationDate:yyyy-MM-dd}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine(GetFooterCopyright(language));
        sb.AppendLine(GetFooterAutoMessage(language));

        return sb.ToString();
    }
}
