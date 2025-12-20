using Homassy.API.Enums;

namespace Homassy.API.Models.Background;

public record EmailTask(string Email, string Code, UserTimeZone? TimeZone, Language Language, EmailType Type);
