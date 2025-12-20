using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Serilog;
using System.Reflection;
using System.Text;

namespace Homassy.API.Services
{
    public static class EmailService
    {
        private static IConfiguration? _configuration;
        private static string? _verificationTemplate;
        private static string? _registrationTemplate;

        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
            LoadEmailTemplates();
        }

        private static void LoadEmailTemplates()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                // Load verification template with UTF-8 encoding
                var verificationResourceName = "Homassy.API.EmailTemplates.VerificationCode.html";
                using (var stream = assembly.GetManifestResourceStream(verificationResourceName))
                {
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        _verificationTemplate = reader.ReadToEnd();
                        Log.Information("Verification email template loaded successfully");
                    }
                    else
                    {
                        Log.Warning($"Verification email template resource not found: {verificationResourceName}");
                    }
                }

                // Load registration template with UTF-8 encoding
                var registrationResourceName = "Homassy.API.EmailTemplates.SuccessfulRegistration.html";
                using (var stream = assembly.GetManifestResourceStream(registrationResourceName))
                {
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        _registrationTemplate = reader.ReadToEnd();
                        Log.Information("Registration email template loaded successfully");
                    }
                    else
                    {
                        Log.Warning($"Registration email template resource not found: {registrationResourceName}");
                    }
                }

                Log.Information($"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load email templates: {ex.Message}");
            }
        }

        public static string GenerateVerificationCode()
        {
            var codeLength = int.Parse(_configuration!["EmailVerification:CodeLength"]!);
            var random = new Random();
            return string.Join("", Enumerable.Range(0, codeLength)
                .Select(_ => random.Next(0, 10).ToString()));
        }

        public static async Task SendVerificationCodeAsync(string email, string code, UserTimeZone? userTimeZone = null, Language language = Language.English)
        {
            try
            {
                if (string.IsNullOrEmpty(_verificationTemplate))
                {
                    Log.Error("Email template is not loaded, cannot send email");
                    throw new InvalidOperationException("Email template is not loaded");
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    _configuration!["Email:SenderName"],
                    _configuration["Email:SenderEmail"]));
                message.To.Add(new MailboxAddress("", email));
                message.Subject = EmailTemplateService.GetVerificationSubject(language);

                var expirationMinutes = int.Parse(_configuration["EmailVerification:CodeExpirationMinutes"]!);
                var expirationTimeUtc = DateTime.UtcNow.AddMinutes(expirationMinutes);

                var timeZone = userTimeZone ?? UserTimeZone.UTC;
                var expirationTime = TimeZoneFunctions.ConvertUtcToUserTimeZone(expirationTimeUtc, timeZone);
                var timeZoneName = TimeZoneFunctions.GetDisplayName(timeZone);

                var expirationTimeFormatted = $"{expirationTime:yyyy-MM-dd HH:mm} ({timeZoneName})";
                var currentYear = DateTime.UtcNow.Year;

                var halfLength = code.Length / 2;
                var formattedCode = $"{code[..halfLength]} - {code[halfLength..]}";

                var htmlBody = _verificationTemplate
                    .Replace("{{APP_NAME}}", EmailTemplateService.GetAppName())
                    .Replace("{{CODE}}", formattedCode)
                    .Replace("{{EXPIRY_TIME}}", expirationTimeFormatted)
                    .Replace("{{YEAR}}", currentYear.ToString())
                    .Replace("{{APP_SUBTITLE}}", EmailTemplateService.GetAppSubtitle(language))
                    .Replace("{{GREETING}}", EmailTemplateService.GetVerificationGreeting(language))
                    .Replace("{{MESSAGE}}", EmailTemplateService.GetVerificationMessage(language))
                    .Replace("{{CODE_LABEL}}", EmailTemplateService.GetVerificationCodeLabel(language))
                    .Replace("{{EXPIRES_AT}}", EmailTemplateService.GetExpiresAt(language, expirationTimeFormatted))
                    .Replace("{{SECURITY_NOTE}}", EmailTemplateService.GetVerificationSecurityNote(language))
                    .Replace("{{FOOTER_COPYRIGHT}}", EmailTemplateService.GetFooterCopyright(language, currentYear))
                    .Replace("{{FOOTER_AUTO_MESSAGE}}", EmailTemplateService.GetFooterAutoMessage(language));

                var plainText = EmailTemplateService.GetVerificationPlainText(language, formattedCode, expirationTimeFormatted, currentYear);

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = htmlBody;
                bodyBuilder.TextBody = plainText;

                message.Body = bodyBuilder.ToMessageBody();

                // Ensure UTF-8 encoding for all text parts
                foreach (var part in message.BodyParts.OfType<TextPart>())
                {
                    part.ContentType.Charset = "utf-8";
                }

                await SendEmailAsync(message);

                Log.Information($"Verification code sent to {email} in {language} language");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to send verification code to {email}: {ex.Message}");
                throw;
            }
        }

        public static async Task SendRegistrationEmailAsync(string email, string username, string code, UserTimeZone? userTimeZone = null, Language language = Language.English)
        {
            try
            {
                if (string.IsNullOrEmpty(_registrationTemplate))
                {
                    Log.Error("Registration email template is not loaded, cannot send email");
                    throw new InvalidOperationException("Registration email template is not loaded");
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    _configuration!["Email:SenderName"],
                    _configuration["Email:SenderEmail"]));
                message.To.Add(new MailboxAddress(username, email));
                message.Subject = EmailTemplateService.GetRegistrationSubject(language);

                var expirationMinutes = int.Parse(_configuration["EmailVerification:CodeExpirationMinutes"]!);
                var expirationTimeUtc = DateTime.UtcNow.AddMinutes(expirationMinutes);

                var timeZone = userTimeZone ?? UserTimeZone.UTC;
                var expirationTime = TimeZoneFunctions.ConvertUtcToUserTimeZone(expirationTimeUtc, timeZone);
                var timeZoneName = TimeZoneFunctions.GetDisplayName(timeZone);

                var expirationTimeFormatted = $"{expirationTime:yyyy-MM-dd HH:mm} ({timeZoneName})";
                var currentYear = DateTime.UtcNow.Year;

                var halfLength = code.Length / 2;
                var formattedCode = $"{code[..halfLength]} - {code[halfLength..]}";

                var htmlBody = _registrationTemplate
                    .Replace("{{APP_NAME}}", EmailTemplateService.GetAppName())
                    .Replace("{{USERNAME}}", username)
                    .Replace("{{EMAIL}}", email)
                    .Replace("{{CODE}}", formattedCode)
                    .Replace("{{EXPIRY_TIME}}", expirationTimeFormatted)
                    .Replace("{{YEAR}}", currentYear.ToString())
                    .Replace("{{APP_SUBTITLE}}", EmailTemplateService.GetAppSubtitle(language))
                    .Replace("{{GREETING}}", EmailTemplateService.GetRegistrationGreeting(language, username))
                    .Replace("{{MESSAGE}}", EmailTemplateService.GetRegistrationMessage(language, email))
                    .Replace("{{COMPLETE_TITLE}}", EmailTemplateService.GetRegistrationCompleteTitle(language))
                    .Replace("{{COMPLETE_MESSAGE}}", EmailTemplateService.GetRegistrationCompleteMessage(language))
                    .Replace("{{CODE_LABEL}}", EmailTemplateService.GetVerificationCodeLabel(language))
                    .Replace("{{EXPIRES_AT}}", EmailTemplateService.GetExpiresAt(language, expirationTimeFormatted))
                    .Replace("{{FOOTER_COPYRIGHT}}", EmailTemplateService.GetFooterCopyright(language, currentYear))
                    .Replace("{{FOOTER_AUTO_MESSAGE}}", EmailTemplateService.GetFooterAutoMessage(language));

                var plainText = EmailTemplateService.GetRegistrationPlainText(language, username, email, formattedCode, expirationTimeFormatted, currentYear);

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = htmlBody;
                bodyBuilder.TextBody = plainText;

                message.Body = bodyBuilder.ToMessageBody();

                // Ensure UTF-8 encoding for all text parts
                foreach (var part in message.BodyParts.OfType<TextPart>())
                {
                    part.ContentType.Charset = "utf-8";
                }

                await SendEmailAsync(message);

                Log.Information($"Registration email sent to {email} in {language} language");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to send registration email to {email}: {ex.Message}");
                throw;
            }
        }

        private static async Task SendEmailAsync(MimeMessage message)
        {
            var username = _configuration!["Email:Username"];
            var password = _configuration["Email:Password"];

            using var client = new SmtpClient();

            var smtpServer = _configuration["Email:SmtpServer"]!;
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"]!);
            var enableSsl = bool.Parse(_configuration["Email:EnableSsl"]!);

            await client.ConnectAsync(smtpServer, smtpPort,
                enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}