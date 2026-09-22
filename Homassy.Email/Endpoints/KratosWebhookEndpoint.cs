using Homassy.Email.Enums;
using Homassy.Email.Models;
using Homassy.Email.Services;

namespace Homassy.Email.Endpoints;

public static class KratosWebhookEndpoint
{
    private static readonly Dictionary<string, (EmailType Type, int ExpiresInMinutes)> TemplateTypeMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["login_code_valid"] = (EmailType.LoginCode, 15),
            ["registration_code_valid"] = (EmailType.RegistrationCode, 15),
            ["verification_code_valid"] = (EmailType.VerificationCode, 60),
            ["recovery_code_valid"] = (EmailType.RecoveryCode, 60),
        };

    public static async Task<IResult> HandleAsync(
        KratosWebhookRequest request,
        IEmailQueueService queue,
        IEmailContentService content,
        ITemplateRendererService renderer,
        ILoggerFactory loggerFactory,
        IHostEnvironment environment)
    {
        var logger = loggerFactory.CreateLogger("KratosWebhookEndpoint");
        if (!TemplateTypeMap.TryGetValue(request.TemplateType, out var mapping))
        {
            logger.LogWarning("Unknown Kratos template_type: {TemplateType}", request.TemplateType);
            return Results.Ok();
        }

        var (emailType, expiresInMinutes) = mapping;
        var traits = request.TemplateData.Identity?.Traits;
        var language = content.ParseLanguage(traits?.DefaultLanguage);
        var name = traits?.Name ?? traits?.DisplayName;

        var code = emailType switch
        {
            EmailType.LoginCode => request.TemplateData.LoginCode,
            EmailType.RegistrationCode => request.TemplateData.RegistrationCode,
            EmailType.VerificationCode => request.TemplateData.VerificationCode,
            EmailType.RecoveryCode => request.TemplateData.RecoveryCode,
            _ => null
        };

        if (string.IsNullOrEmpty(code))
        {
            logger.LogWarning("No code found for template_type {TemplateType}", request.TemplateType);
            return Results.Ok();
        }

        // DEVELOPMENT ONLY — this writes a live credential to the log.
        //
        // The local stack has no inbox: outgoing mail goes to whatever SMTP the compose file points at,
        // and a developer logging in to a freshly seeded database has no way to read the code Kratos just
        // generated. Kratos itself will not say it either (`log.leak_sensitive_values: false`), and it has
        // no setting that pins the code to a fixed value, so the choice is between printing it here and
        // inventing an authentication path that exists only in development — which is the kind of thing
        // that survives into production. This does not: `IsDevelopment()` is false unless
        // ASPNETCORE_ENVIRONMENT says otherwise, and the production compose file sets it to Production.
        //
        // Warning level on purpose. It is not a warning about the request; it is a warning that this log
        // now contains a credential, and it should be as loud as an accident with the environment
        // variable would be.
        if (environment.IsDevelopment())
        {
            logger.LogWarning(
                "[DEV] {TemplateType} for {To}: {Code} — development only, never enabled outside it",
                request.TemplateType,
                request.To,
                code);
        }

        var expiresAt = content.GetExpiresAt(language, expiresInMinutes);
        var subject = content.GetSubject(emailType, language);
        var plainText = content.GetPlainText(emailType, language, code, expiresAt);
        var htmlBody = renderer.Render(new Dictionary<string, string>
        {
            ["GREETING"] = content.GetGreeting(emailType, language),
            ["MESSAGE"] = content.GetMessage(emailType, language, name),
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
