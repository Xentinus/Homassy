using Homassy.Email.Models;
using Homassy.Email.Services;

namespace Homassy.Email.Endpoints;

public static class AutomationNotificationEndpoint
{
    public static async Task<IResult> HandleAsync(
        AutomationNotificationRequest request,
        IEmailQueueService queue,
        IEmailContentService content,
        ITemplateRendererService renderer,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("AutomationNotificationEndpoint");

        var language = content.ParseLanguage(request.Language);
        var actionType = request.ActionType ?? "notify_only";

        var subject = content.GetAutomationSubject(language, actionType);
        var greeting = content.GetAutomationGreeting(language, actionType);
        var message = content.GetAutomationMessage(language, request.Name, request.ProductName, actionType, request.ConsumedQuantity, request.Unit);
        var detailSection = BuildDetailSection(request, language, content);

        var htmlBody = renderer.RenderAutomationNotification(new Dictionary<string, string>
        {
            ["GREETING"] = greeting,
            ["MESSAGE"] = message,
            ["DETAIL_SECTION"] = detailSection,
            ["FOOTER_COPYRIGHT"] = content.GetFooterCopyright(language),
            ["FOOTER_AUTO_MESSAGE"] = content.GetFooterAutoMessage(language)
        });

        var plainText = content.GetAutomationPlainText(language, request.Name, request.ProductName, actionType, request.ConsumedQuantity, request.Unit);

        var emailMessage = new EmailMessage(
            To: request.To,
            Subject: subject,
            HtmlBody: htmlBody,
            PlainTextBody: plainText);

        var enqueued = await queue.TryEnqueueAsync(emailMessage);
        if (!enqueued)
            logger.LogWarning("Email queue full – dropped automation notification email for {To}", request.To);

        return Results.Ok();
    }

    private static string BuildDetailSection(AutomationNotificationRequest request, Enums.Language language, IEmailContentService content)
    {
        if (request.ActionType != "auto_consume" || request.ConsumedQuantity == null)
            return string.Empty;

        var quantityLabel = language switch
        {
            Enums.Language.Hungarian => "Felhasznált mennyiség",
            Enums.Language.German => "Verbrauchte Menge",
            _ => "Consumed quantity"
        };

        var unitStr = request.Unit ?? "";

        return $"""
            <table role="presentation" cellspacing="0" cellpadding="0" border="0" width="100%" style="margin-bottom: 20px;">
                <tr>
                    <td style="background-color: #f0f9ff; border-radius: 12px; border: 1px solid #bae6fd; padding: 20px 24px; text-align: center;">
                        <p style="margin: 0 0 8px 0; font-size: 13px; font-weight: 600; color: #0369a1;">{quantityLabel}</p>
                        <p style="margin: 0; font-size: 24px; font-weight: 700; color: #0c4a6e;">{request.ConsumedQuantity} {unitStr}</p>
                    </td>
                </tr>
            </table>
            """;
    }
}
