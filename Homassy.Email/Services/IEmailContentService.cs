using Homassy.Email.Enums;
using Homassy.Email.Models;

namespace Homassy.Email.Services;

public interface IEmailContentService
{
    string GetSubject(EmailType type, Language language);
    string GetGreeting(EmailType type, Language language);
    string GetMessage(EmailType type, Language language, string? name);
    string GetCodeLabel(EmailType type, Language language);
    string GetExpiresAt(Language language, int expiresInMinutes);
    string GetSecurityNote(EmailType type, Language language);
    string GetFooterCopyright(Language language);
    string GetFooterAutoMessage(Language language);
    string GetPlainText(EmailType type, Language language, string code, string expiresAt);
    Language ParseLanguage(string? code);

    // ─── Weekly Summary ─────────────────────────────────────────────────────────
    string GetWeeklySummarySubject(Language language);
    string GetWeeklySummaryGreeting(Language language);
    string GetWeeklySummaryMessage(Language language, string? name);
    string GetExpiredSectionHeader(Language language);
    string GetExpiringSoonSectionHeader(Language language);
    string GetNoExpiringItemsMessage(Language language);
    string GetWeeklySummaryPlainText(Language language, string? name, ExpiringProductDto[] expiredItems, ExpiringProductDto[] expiringSoonItems);
}
