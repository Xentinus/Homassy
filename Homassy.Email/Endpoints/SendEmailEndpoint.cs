using Homassy.Email.Enums;
using Homassy.Email.Models;
using Homassy.Email.Services;

namespace Homassy.Email.Endpoints;

public static class SendEmailEndpoint
{
    private static readonly Dictionary<string, EmailType> TypeMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["login_code"] = EmailType.LoginCode,
            ["registration_code"] = EmailType.RegistrationCode,
            ["verification_code"] = EmailType.VerificationCode,
            ["recovery_code"] = EmailType.RecoveryCode,
        };

    public static async Task<IResult> HandleAsync(
        SendEmailRequest request,
        IEmailQueueService queue,
        IEmailContentService content,
        ITemplateRendererService renderer,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("SendEmailEndpoint");

        if (!TypeMap.TryGetValue(request.Type, out var emailType))
        {
            logger.LogWarning("Unknown email type: {Type}", request.Type);
            return Results.BadRequest($"Unknown email type: {request.Type}");
        }

        var language = content.ParseLanguage(request.Language);
        var code = request.Params.Code ?? string.Empty;
        var expiresInMinutes = request.Params.ExpiresInMinutes ?? 15;
        var expiresAt = content.GetExpiresAt(language, expiresInMinutes);
        var subject = content.GetSubject(emailType, language);
        var plainText = content.GetPlainText(emailType, language, code, expiresAt);
        var htmlBody = renderer.Render(new Dictionary<string, string>
        {
            ["GREETING"] = content.GetGreeting(emailType, language),
            ["MESSAGE"] = content.GetMessage(emailType, language, request.Params.Name),
            ["CODE"] = code,
            ["CODE_LABEL"] = content.GetCodeLabel(emailType, language),
            ["EXPIRES_AT"] = expiresAt,
            ["SECURITY_NOTE"] = content.GetSecurityNote(emailType, language),
            ["FOOTER_COPYRIGHT"] = content.GetFooterCopyright(language),
            ["FOOTER_AUTO_MESSAGE"] = content.GetFooterAutoMessage(language)
        });

        var message = new EmailMessage(
            To: request.To,
            Subject: subject,
            HtmlBody: htmlBody,
            PlainTextBody: plainText);

        var enqueued = await queue.TryEnqueueAsync(message);
        if (!enqueued)
            logger.LogWarning("Email queue full – dropped email for {To} subject {Subject}", message.To, message.Subject);

        return Results.Ok();
    }
}
