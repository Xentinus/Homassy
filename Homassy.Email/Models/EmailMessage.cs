namespace Homassy.Email.Models;

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string PlainTextBody,
    int AttemptCount = 0
);
