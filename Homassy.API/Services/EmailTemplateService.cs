using Homassy.API.Enums;
using Homassy.API.Extensions;

namespace Homassy.API.Services
{
    public static class EmailTemplateService
    {
        #region Common Strings
        
        public static string GetAppName() => "Homassy";

        public static string GetAppSubtitle(Language language) => language switch
        {
            Language.Hungarian => "Otthoni Raktárkezelő Rendszer",
            Language.German => "Hauslager-Verwaltungssystem",
            _ => "Home Storage Management System"
        };

        public static string GetFooterCopyright(Language language, int year) => language switch
        {
            Language.Hungarian => $"© {year} Homassy. Minden jog fenntartva.",
            Language.German => $"© {year} Homassy. Alle Rechte vorbehalten.",
            _ => $"© {year} Homassy. All rights reserved."
        };

        public static string GetFooterAutoMessage(Language language) => language switch
        {
            Language.Hungarian => "Ez egy automatikus üzenet, kérjük ne válaszoljon erre az e-mailre.",
            Language.German => "Dies ist eine automatische Nachricht, bitte antworten Sie nicht auf diese E-Mail.",
            _ => "This is an automated message, please do not reply to this email."
        };

        public static string GetExpiresAt(Language language, string expirationTime) => language switch
        {
            Language.Hungarian => $"Lejár: {expirationTime}",
            Language.German => $"Läuft ab um {expirationTime}",
            _ => $"Expires at {expirationTime}"
        };

        #endregion

        #region Verification Email

        public static string GetVerificationSubject(Language language) => language switch
        {
            Language.Hungarian => "Ellenőrző kód",
            Language.German => "Verifizierungscode",
            _ => "Verification Code"
        };

        public static string GetVerificationGreeting(Language language) => language switch
        {
            Language.Hungarian => "Üdvözöljük újra!",
            Language.German => "Willkommen zurück!",
            _ => "Welcome back!"
        };

        public static string GetVerificationMessage(Language language) => language switch
        {
            Language.Hungarian => "Bejelentkezési kérelmet kaptunk a Homassy fiókjához. A hitelesítés befejezéséhez használja az alábbi ellenőrző kódot.",
            Language.German => "Wir haben eine Anmeldeanfrage für Ihr Homassy-Konto erhalten. Verwenden Sie den folgenden Bestätigungscode, um Ihre Authentifizierung abzuschließen.",
            _ => "We received a request to sign in to your Homassy account. Use the verification code below to complete your authentication."
        };

        public static string GetVerificationCodeLabel(Language language) => language switch
        {
            Language.Hungarian => "Ellenőrző kód",
            Language.German => "Verifizierungscode",
            _ => "Verification Code"
        };

        public static string GetVerificationSecurityNote(Language language) => language switch
        {
            Language.Hungarian => "Ez az ellenőrző kód egyedi a bejelentkezési munkamenetéhez, és nem szabad megosztani senkivel.",
            Language.German => "Dieser Bestätigungscode ist einzigartig für Ihre Anmeldesitzung und sollte mit niemandem geteilt werden.",
            _ => "This verification code is unique to your login session and should not be shared with anyone."
        };

        public static string GetVerificationPlainText(Language language, string code, string expirationTime, int year) => language switch
        {
            Language.Hungarian => $"Üdvözöljük a Homassy-ban!\n\nAz Ön ellenőrző kódja: {code}\n\nA kód lejár: {expirationTime}.\n\n© {year} Homassy - Otthoni Raktárkezelő Rendszer",
            Language.German => $"Willkommen bei Homassy!\n\nIhr Bestätigungscode lautet: {code}\n\nDieser Code läuft ab um {expirationTime}.\n\n© {year} Homassy - Hauslager-Verwaltungssystem",
            _ => $"Welcome to Homassy!\n\nYour verification code is: {code}\n\nThis code will expire at {expirationTime}.\n\n© {year} Homassy - Home Storage Management System"
        };

        #endregion

        #region Registration Email

        public static string GetRegistrationSubject(Language language) => language switch
        {
            Language.Hungarian => "Üdvözöljük a Homassy-ban - Fejezze be a regisztrációt",
            Language.German => "Willkommen bei Homassy - Schließen Sie Ihre Registrierung ab",
            _ => "Welcome to Homassy - Complete Your Registration"
        };

        public static string GetRegistrationGreeting(Language language) => language switch
        {
            Language.Hungarian => $"Üdvözöljük!",
            Language.German => $"Willkommen!",
            _ => $"Welcome!"
        };

        public static string GetRegistrationMessage(Language language, string email) => language switch
        {
            Language.Hungarian => $"Gratulálunk! Sikeresen regisztrált a Homassy-ba a következő e-mail címmel: <strong>{email}</strong>.",
            Language.German => $"Herzlichen Glückwunsch! Sie haben sich erfolgreich bei Homassy mit der E-Mail-Adresse <strong>{email}</strong> registriert.",
            _ => $"Congratulations! You have successfully registered to Homassy with the email address <strong>{email}</strong>."
        };

        public static string GetRegistrationCompleteTitle(Language language) => language switch
        {
            Language.Hungarian => "⚠️ Fejezze be a regisztrációt",
            Language.German => "⚠️ Schließen Sie Ihre Registrierung ab",
            _ => "⚠️ Complete Your Registration"
        };

        public static string GetRegistrationCompleteMessage(Language language) => language switch
        {
            Language.Hungarian => "A regisztrációs folyamat befejezéséhez és fiókja aktiválásához jelentkezzen be az alábbi ellenőrző kóddal.",
            Language.German => "Um den Registrierungsprozess abzuschließen und Ihr Konto zu aktivieren, melden Sie sich bitte mit dem folgenden Bestätigungscode an.",
            _ => "To finish the registration process and activate your account, please log in using the verification code below."
        };

        public static string GetRegistrationPlainText(Language language, string username, string email, string code, string expirationTime, int year) => language switch
        {
            Language.Hungarian => $"Üdvözöljük a Homassy-ban, {username}!\n\nGratulálunk! Sikeresen regisztrált a következő e-mail címmel: {email}\n\nA regisztráció befejezéséhez jelentkezzen be ezzel az ellenőrző kóddal: {code}\n\nA kód lejár: {expirationTime}.\n\n© {year} Homassy - Otthoni Raktárkezelő Rendszer",
            Language.German => $"Willkommen bei Homassy, {username}!\n\nHerzlichen Glückwunsch! Sie haben sich erfolgreich mit der E-Mail-Adresse registriert: {email}\n\nUm Ihre Registrierung abzuschließen, melden Sie sich bitte mit diesem Bestätigungscode an: {code}\n\nDieser Code läuft ab um {expirationTime}.\n\n© {year} Homassy - Hauslager-Verwaltungssystem",
            _ => $"Welcome to Homassy, {username}!\n\nCongratulations! You have successfully registered with the email: {email}\n\nTo complete your registration, please log in with this verification code: {code}\n\nThis code will expire at {expirationTime}.\n\n© {year} Homassy - Home Storage Management System"
        };

        #endregion

        #region Helper Methods

        public static string GetCultureCode(Language language) => language.ToLanguageCode();

        #endregion
    }
}
