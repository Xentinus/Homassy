using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Homassy.API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string GenerateVerificationCode()
        {
            var codeLength = int.Parse(_configuration["EmailVerification:CodeLength"]!);
            var random = new Random();
            return string.Join("", Enumerable.Range(0, codeLength)
                .Select(_ => random.Next(0, 10).ToString()));
        }

        public async Task SendVerificationCodeAsync(string email, string code)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    _configuration["Email:SenderName"],
                    _configuration["Email:SenderEmail"]));
                message.To.Add(new MailboxAddress("", email));
                message.Subject = "Your Homassy Verification Code";

                var expirationMinutes = _configuration["EmailVerification:CodeExpirationMinutes"];

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $@"
                        <!DOCTYPE html>
                        <html>
                        <head>
                            <style>
                                body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; }}
                                .container {{ background-color: white; padding: 30px; border-radius: 10px; max-width: 600px; margin: 0 auto; }}
                                .code {{ color: #4CAF50; font-size: 48px; letter-spacing: 10px; font-weight: bold; text-align: center; margin: 20px 0; }}
                                .footer {{ color: #888; font-size: 12px; margin-top: 30px; text-align: center; }}
                            </style>
                        </head>
                        <body>
                            <div class='container'>
                                <h2>Welcome to Homassy! 🏠</h2>
                                <p>Your verification code is:</p>
                                <div class='code'>{code}</div>
                                <p>This code will expire in <strong>{expirationMinutes} minutes</strong>.</p>
                                <p>If you didn't request this code, please ignore this email.</p>
                                <div class='footer'>
                                    <p>© 2025 Homassy - Home Storage Management System</p>
                                </div>
                            </div>
                        </body>
                        </html>",
                    TextBody = $"Your Homassy verification code is: {code}\n\n" +
                              $"This code will expire in {expirationMinutes} minutes.\n\n" +
                              $"If you didn't request this code, please ignore this email."
                };

                message.Body = bodyBuilder.ToMessageBody();

                // Check if email is configured
                var username = _configuration["Email:Username"];
                var password = _configuration["Email:Password"];

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    // Development mode: log to console
                    _logger.LogWarning("Email not configured. Verification code for {Email}: {Code}", email, code);
                    Console.WriteLine($"\n{'=' * 50}");
                    Console.WriteLine($"VERIFICATION CODE FOR: {email}");
                    Console.WriteLine($"CODE: {code}");
                    Console.WriteLine($"EXPIRES IN: {expirationMinutes} minutes");
                    Console.WriteLine($"{'=' * 50}\n");
                    return;
                }

                using var client = new SmtpClient();

                var smtpServer = _configuration["Email:SmtpServer"]!;
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"]!);
                var enableSsl = bool.Parse(_configuration["Email:EnableSsl"]!);

                await client.ConnectAsync(smtpServer, smtpPort,
                    enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Verification code sent to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification code to {Email}", email);
                throw;
            }
        }
    }
}
