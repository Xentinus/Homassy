using Homassy.API.Models.ApplicationSettings;

namespace Homassy.API.Services;

public class AccountLockoutService
{
    private readonly AccountLockoutSettings _settings;

    public AccountLockoutService()
    {
        _settings = GetSettingsFromConfiguration();
    }

    public AccountLockoutService(AccountLockoutSettings settings)
    {
        _settings = settings;
    }

    private static AccountLockoutSettings GetSettingsFromConfiguration()
    {
        var config = ConfigService.Configuration;
        var section = config.GetSection("Security:AccountLockout");

        return new AccountLockoutSettings
        {
            MaxFailedAttempts = section.GetValue("MaxFailedAttempts", 5),
            LockoutDurationMinutes = section.GetValue("LockoutDurationMinutes", 15),
            EnableProgressiveLockout = section.GetValue("EnableProgressiveLockout", true),
            ProgressiveMultiplier = section.GetValue("ProgressiveMultiplier", 2),
            MaxLockoutDurationMinutes = section.GetValue("MaxLockoutDurationMinutes", 60)
        };
    }

    public bool IsLockedOut(DateTime? lockedOutUntil)
    {
        return lockedOutUntil.HasValue && lockedOutUntil.Value > DateTime.UtcNow;
    }

    public DateTime? CalculateLockoutExpiry(int failedAttempts)
    {
        if (failedAttempts < _settings.MaxFailedAttempts)
        {
            return null;
        }

        var baseDuration = _settings.LockoutDurationMinutes;

        if (_settings.EnableProgressiveLockout)
        {
            var lockoutCount = (failedAttempts - _settings.MaxFailedAttempts) / _settings.MaxFailedAttempts;
            var multiplier = Math.Pow(_settings.ProgressiveMultiplier, lockoutCount);
            baseDuration = (int)(baseDuration * multiplier);
            baseDuration = Math.Min(baseDuration, _settings.MaxLockoutDurationMinutes);
        }

        return DateTime.UtcNow.AddMinutes(baseDuration);
    }

    public bool ShouldLockout(int failedAttempts)
    {
        return failedAttempts >= _settings.MaxFailedAttempts;
    }

    public int GetMaxFailedAttempts() => _settings.MaxFailedAttempts;

    public int GetRemainingAttempts(int failedAttempts)
    {
        return Math.Max(0, _settings.MaxFailedAttempts - failedAttempts);
    }
}
