using Homassy.API.Enums;

namespace Homassy.API.Exceptions;

public class AccountLockedException : AuthException
{
    public DateTime? LockedUntil { get; }
    public int RemainingSeconds { get; }

    public AccountLockedException(DateTime lockedUntil)
        : base($"Account is locked. Try again after {lockedUntil:HH:mm:ss} UTC", StatusCodes.Status429TooManyRequests, ErrorCodes.AuthAccountLocked)
    {
        LockedUntil = lockedUntil;
        RemainingSeconds = (int)Math.Max(0, (lockedUntil - DateTime.UtcNow).TotalSeconds);
    }

    public AccountLockedException(string message = "Account is temporarily locked due to too many failed login attempts")
        : base(message, StatusCodes.Status429TooManyRequests, ErrorCodes.AuthAccountLocked)
    {
    }
}
