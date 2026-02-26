using System.Text;
using Homassy.Email.Models;
using Homassy.Email.Services;

namespace Homassy.Email.Endpoints;

public static class WeeklySummaryEndpoint
{
    public static async Task<IResult> HandleAsync(
        WeeklySummaryRequest request,
        IEmailQueueService queue,
        IEmailContentService content,
        ITemplateRendererService renderer,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("WeeklySummaryEndpoint");

        var language = content.ParseLanguage(request.Language);

        var subject  = content.GetWeeklySummarySubject(language);
        var greeting = content.GetWeeklySummaryGreeting(language);
        var message  = content.GetWeeklySummaryMessage(language, request.Name);

        var expiredSection   = BuildSection(request.ExpiredItems,      content.GetExpiredSectionHeader(language),      "#fff1f0", "#ffa39e", "#cf1322");
        var expiringSection  = BuildSection(request.ExpiringSoonItems, content.GetExpiringSoonSectionHeader(language), "#fffbe6", "#ffe58f", "#ad8b00");
        var noItemsMessage   = (request.ExpiredItems.Length == 0 && request.ExpiringSoonItems.Length == 0)
            ? BuildNoItemsBlock(content.GetNoExpiringItemsMessage(language))
            : string.Empty;

        var htmlBody = renderer.RenderWeeklySummary(new Dictionary<string, string>
        {
            ["GREETING"]          = greeting,
            ["MESSAGE"]           = message,
            ["EXPIRED_SECTION"]   = expiredSection,
            ["EXPIRING_SECTION"]  = expiringSection,
            ["NO_ITEMS_MESSAGE"]  = noItemsMessage,
            ["FOOTER_COPYRIGHT"]  = content.GetFooterCopyright(language),
            ["FOOTER_AUTO_MESSAGE"] = content.GetFooterAutoMessage(language)
        });

        var plainText = content.GetWeeklySummaryPlainText(language, request.Name, request.ExpiredItems, request.ExpiringSoonItems);

        var emailMessage = new EmailMessage(
            To: request.To,
            Subject: subject,
            HtmlBody: htmlBody,
            PlainTextBody: plainText);

        var enqueued = await queue.TryEnqueueAsync(emailMessage);
        if (!enqueued)
            logger.LogWarning("Email queue full – dropped weekly summary email for {To}", request.To);

        return Results.Ok();
    }

    // ─── HTML helpers ────────────────────────────────────────────────────────────

    private static string BuildSection(ExpiringProductDto[] items, string header, string bgColor, string borderColor, string titleColor)
    {
        if (items.Length == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"""
            <table role="presentation" cellspacing="0" cellpadding="0" border="0" width="100%" style="margin-bottom: 20px;">
                <tr>
                    <td style="background-color: {bgColor}; border-radius: 12px; border: 1px solid {borderColor}; padding: 20px 24px;">
                        <p style="margin: 0 0 12px 0; font-size: 14px; font-weight: 700; color: {titleColor};">{header}</p>
                        <table role="presentation" cellspacing="0" cellpadding="0" border="0" width="100%">
            """);

        foreach (var item in items)
        {
            var brand = string.IsNullOrWhiteSpace(item.Brand) ? string.Empty : $" <span style=\"color: #888888; font-size: 13px;\">({item.Brand})</span>";
            var date  = item.ExpirationDate.ToString("yyyy-MM-dd");
            sb.AppendLine($"""
                            <tr>
                                <td style="padding: 4px 0; font-size: 14px; color: #444444; border-bottom: 1px solid {borderColor};">
                                    <span style="font-weight: 600;">{item.Name}</span>{brand}
                                </td>
                                <td style="padding: 4px 0; font-size: 14px; color: #666666; text-align: right; white-space: nowrap; border-bottom: 1px solid {borderColor};">
                                    {date}
                                </td>
                            </tr>
                """);
        }

        sb.AppendLine("""
                        </table>
                    </td>
                </tr>
            </table>
            """);

        return sb.ToString();
    }

    private static string BuildNoItemsBlock(string message) => $"""
        <table role="presentation" cellspacing="0" cellpadding="0" border="0" width="100%">
            <tr>
                <td style="background-color: #f6ffed; border-radius: 12px; border: 1px solid #b7eb8f; padding: 20px 24px; text-align: center;">
                    <p style="margin: 0; font-size: 15px; color: #389e0d; font-weight: 600;">{message}</p>
                </td>
            </tr>
        </table>
        """;
}
