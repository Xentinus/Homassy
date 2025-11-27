using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Serilog;
using System.Reflection;

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

                // Load verification template
                var verificationResourceName = "Homassy.API.EmailTemplates.VerificationCode.html";
                using (var stream = assembly.GetManifestResourceStream(verificationResourceName))
                {
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        _verificationTemplate = reader.ReadToEnd();
                        Log.Information("Verification email template loaded successfully");
                    }
                    else
                    {
                        Log.Warning($"Verification email template resource not found: {verificationResourceName}");
                    }
                }

                // Load registration template (with verification code)
                var registrationResourceName = "Homassy.API.EmailTemplates.SuccessfulRegistration.html";
                using (var stream = assembly.GetManifestResourceStream(registrationResourceName))
                {
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
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

        public static async Task SendVerificationCodeAsync(string email, string code, UserTimeZone? userTimeZone = null)
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
                message.Subject = "Your Homassy Verification Code";

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
                    .Replace("{{CODE}}", formattedCode)
                    .Replace("{{EXPIRY_TIME}}", expirationTimeFormatted)
                    .Replace("{{YEAR}}", currentYear.ToString());

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody,
                    TextBody = $"Welcome to Homassy!\n\n" +
                              $"Your verification code is: {formattedCode}\n\n" +
                              $"This code will expire at {expirationTimeFormatted}.\n\n" +
                              $"© {currentYear} Homassy - Home Storage Management System"
                };

                message.Body = bodyBuilder.ToMessageBody();

                await SendEmailAsync(message);

                Log.Information($"Verification code sent to {email}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to send verification code to {email}: {ex.Message}");
                throw;
            }
        }

        public static async Task SendRegistrationEmailAsync(string email, string username, string code, UserTimeZone? userTimeZone = null)
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
                message.Subject = "Welcome to Homassy - Complete Your Registration";

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
                    .Replace("{{USERNAME}}", username)
                    .Replace("{{EMAIL}}", email)
                    .Replace("{{CODE}}", formattedCode)
                    .Replace("{{EXPIRY_TIME}}", expirationTimeFormatted)
                    .Replace("{{YEAR}}", currentYear.ToString());

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody,
                    TextBody = $"Welcome to Homassy, {username}!\n\n" +
                              $"Congratulations! You have successfully registered with the email: {email}\n\n" +
                              $"To complete your registration, please log in with this verification code: {formattedCode}\n\n" +
                              $"This code will expire at {expirationTimeFormatted}.\n\n" +
                              $"© {currentYear} Homassy - Home Storage Management System"
                };

                message.Body = bodyBuilder.ToMessageBody();

                await SendEmailAsync(message);

                Log.Information($"Registration email sent to {email}");
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