using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Homassy.API.Enums;
using Homassy.API.Functions;

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

        public async Task SendVerificationCodeAsync(string email, string code, UserTimeZone? userTimeZone = null)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    _configuration["Email:SenderName"],
                    _configuration["Email:SenderEmail"]));
                message.To.Add(new MailboxAddress("", email));
                message.Subject = "Your Homassy Verification Code";

                var expirationMinutes = int.Parse(_configuration["EmailVerification:CodeExpirationMinutes"]!);
                var expirationTimeUtc = DateTime.UtcNow.AddMinutes(expirationMinutes);

                // Convert to user's timezone or fallback to UTC
                DateTime expirationTime;
                string timeZoneName;

                var timeZone = userTimeZone ?? UserTimeZone.UTC;
                var timeZoneInfo = TimeZoneFunctions.GetTimeZoneInfo(timeZone);
                expirationTime = TimeZoneInfo.ConvertTimeFromUtc(expirationTimeUtc, timeZoneInfo);
                timeZoneName = TimeZoneFunctions.GetDisplayName(timeZone);

                var expirationTimeFormatted = $"{expirationTime:yyyy-MM-dd HH:mm} ({timeZoneName})";
                var currentYear = DateTime.UtcNow.Year;

                // Format code with dash separator (e.g., "123 - 456")
                var halfLength = code.Length / 2;
                var formattedCode = $"{code[..halfLength]} - {code[halfLength..]}";

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $@"
                        <!DOCTYPE html>
                        <html>
                        <head>
                            <meta charset='UTF-8'>
                            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                            <meta http-equiv='X-UA-Compatible' content='IE=edge'>
                            <style>
                                * {{
                                    margin: 0;
                                    padding: 0;
                                    box-sizing: border-box;
                                }}
                                body {{ 
                                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
                                    background-color: #f9fafb;
                                    padding: 20px 16px;
                                    line-height: 1.6;
                                    -webkit-font-smoothing: antialiased;
                                    -moz-osx-font-smoothing: grayscale;
                                }}
                                .email-wrapper {{
                                    max-width: 600px;
                                    margin: 0 auto;
                                    width: 100%;
                                }}
                                .container {{ 
                                    background-color: #ffffff;
                                    border: 1px solid #e5e7eb;
                                    border-radius: 12px;
                                    overflow: hidden;
                                    box-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06);
                                }}
                                .header {{
                                    background: linear-gradient(135deg, #18181b 0%, #27272a 100%);
                                    padding: 32px 20px;
                                    text-align: center;
                                }}
                                .header-title {{
                                    color: #ffffff;
                                    font-size: 24px;
                                    font-weight: 700;
                                    margin: 0;
                                    letter-spacing: -0.5px;
                                }}
                                .header-subtitle {{
                                    color: #a1a1aa;
                                    font-size: 13px;
                                    margin-top: 8px;
                                    font-weight: 400;
                                }}
                                .content {{
                                    padding: 32px 24px;
                                }}
                                .greeting {{
                                    color: #18181b;
                                    font-size: 18px;
                                    font-weight: 600;
                                    margin-bottom: 12px;
                                    letter-spacing: -0.3px;
                                }}
                                .message {{
                                    color: #52525b;
                                    font-size: 15px;
                                    margin-bottom: 28px;
                                    line-height: 1.6;
                                }}
                                .code-container {{
                                    background: linear-gradient(135deg, #18181b 0%, #27272a 100%);
                                    border-radius: 12px;
                                    padding: 28px 20px;
                                    text-align: center;
                                    margin: 28px 0;
                                    box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
                                }}
                                .code-label {{
                                    color: #a1a1aa;
                                    font-size: 11px;
                                    text-transform: uppercase;
                                    letter-spacing: 1.2px;
                                    margin-bottom: 14px;
                                    font-weight: 600;
                                }}
                                .code {{
                                    color: #ffffff;
                                    font-size: clamp(28px, 8vw, 36px);
                                    font-weight: 700;
                                    letter-spacing: 0.3em;
                                    font-family: 'Courier New', Courier, monospace;
                                    margin: 0;
                                    user-select: all;
                                    word-break: keep-all;
                                    white-space: nowrap;
                                    overflow: hidden;
                                    text-overflow: clip;
                                    padding: 0 10px;
                                }}
                                .expiry-info {{
                                    display: inline-flex;
                                    align-items: center;
                                    gap: 6px;
                                    background-color: #3f3f46;
                                    color: #fafafa;
                                    padding: 8px 14px;
                                    border-radius: 8px;
                                    font-size: 12px;
                                    margin-top: 18px;
                                    font-weight: 500;
                                }}
                                .divider {{
                                    height: 1px;
                                    background-color: #e5e7eb;
                                    margin: 28px 0;
                                }}
                                .footer {{
                                    background-color: #f9fafb;
                                    padding: 20px 24px;
                                    text-align: center;
                                    border-top: 1px solid #e5e7eb;
                                }}
                                .footer-text {{
                                    color: #71717a;
                                    font-size: 12px;
                                    margin: 0;
                                    line-height: 1.5;
                                }}
                                .footer-text + .footer-text {{
                                    margin-top: 6px;
                                }}
                                .badge {{
                                    display: inline-block;
                                    background-color: #18181b;
                                    color: #ffffff;
                                    padding: 4px 12px;
                                    border-radius: 6px;
                                    font-size: 11px;
                                    font-weight: 600;
                                    margin-top: 12px;
                                    letter-spacing: 0.3px;
                                }}
                                
                                /* Mobile Responsive Styles */
                                @media only screen and (max-width: 600px) {{
                                    body {{
                                        padding: 12px 8px;
                                    }}
                                    .header {{
                                        padding: 28px 16px;
                                    }}
                                    .header-title {{
                                        font-size: 22px;
                                    }}
                                    .header-subtitle {{
                                        font-size: 12px;
                                    }}
                                    .content {{
                                        padding: 24px 16px;
                                    }}
                                    .greeting {{
                                        font-size: 16px;
                                        margin-bottom: 10px;
                                    }}
                                    .message {{
                                        font-size: 14px;
                                        margin-bottom: 24px;
                                    }}
                                    .code-container {{
                                        padding: 24px 12px;
                                        margin: 24px 0;
                                        border-radius: 10px;
                                    }}
                                    .code-label {{
                                        font-size: 10px;
                                        margin-bottom: 12px;
                                    }}
                                    .code {{
                                        font-size: clamp(24px, 7vw, 32px);
                                        letter-spacing: 0.25em;
                                        padding: 0 8px;
                                    }}
                                    .expiry-info {{
                                        font-size: 11px;
                                        padding: 6px 12px;
                                        gap: 5px;
                                        margin-top: 16px;
                                    }}
                                    .divider {{
                                        margin: 24px 0;
                                    }}
                                    .footer {{
                                        padding: 16px 16px;
                                    }}
                                    .footer-text {{
                                        font-size: 11px;
                                    }}
                                    .badge {{
                                        font-size: 10px;
                                        padding: 3px 10px;
                                        margin-top: 10px;
                                    }}
                                }}
                                
                                /* Extra Small Mobile Devices */
                                @media only screen and (max-width: 360px) {{
                                    .code {{
                                        font-size: 22px;
                                        letter-spacing: 0.2em;
                                    }}
                                    .code-container {{
                                        padding: 20px 8px;
                                    }}
                                }}
                                
                                /* Dark Mode Support */
                                @media (prefers-color-scheme: dark) {{
                                    body {{
                                        background-color: #18181b;
                                    }}
                                    .container {{
                                        border-color: #3f3f46;
                                    }}
                                    .footer {{
                                        background-color: #18181b;
                                        border-top-color: #3f3f46;
                                    }}
                                }}
                            </style>
                        </head>
                        <body>
                            <div class='email-wrapper'>
                                <div class='container'>
                                    <div class='header'>
                                        <h1 class='header-title'>Homassy</h1>
                                        <p class='header-subtitle'>Home Storage Management System</p>
                                    </div>
                                    
                                    <div class='content'>
                                        <h2 class='greeting'>Welcome back!</h2>
                                        <p class='message'>
                                            We received a request to sign in to your Homassy account. 
                                            Use the verification code below to complete your authentication.
                                        </p>
                                        
                                        <div class='code-container'>
                                            <div class='code-label'>Your Verification Code</div>
                                            <div class='code'>{formattedCode}</div>
                                            <div class='expiry-info'>
                                                <span>⏱️</span>
                                                <span>Expires at {expirationTimeFormatted}</span>
                                            </div>
                                        </div>
                                        
                                        <div class='divider'></div>
                                        
                                        <p class='message' style='margin-bottom: 0; font-size: 14px;'>
                                            This verification code is unique to your login session and should not be shared with anyone.
                                        </p>
                                    </div>
                                    
                                    <div class='footer'>
                                        <p class='footer-text'>© {currentYear} Homassy. All rights reserved.</p>
                                        <p class='footer-text'>This is an automated message, please do not reply to this email.</p>
                                    </div>
                                </div>
                            </div>
                        </body>
                        </html>",
                    TextBody = $"Welcome to Homassy!\n\n" +
                              $"Your verification code is: {formattedCode}\n\n" +
                              $"This code will expire at {expirationTimeFormatted}.\n\n" +
                              $"This verification code is unique to your login session and should not be shared with anyone.\n\n" +
                              $"© {currentYear} Homassy - Home Storage Management System"
                };

                message.Body = bodyBuilder.ToMessageBody();

                // Check if email is configured
                var username = _configuration["Email:Username"];
                var password = _configuration["Email:Password"];

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    // Development mode: log to console
                    _logger.LogWarning("Email not configured. Verification code for {Email}: {Code}", email, formattedCode);
                    Console.WriteLine($"\n{new string('=', 50)}");
                    Console.WriteLine($"VERIFICATION CODE FOR: {email}");
                    Console.WriteLine($"CODE: {formattedCode}");
                    Console.WriteLine($"EXPIRES AT: {expirationTimeFormatted}");
                    Console.WriteLine($"{new string('=', 50)}\n");
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