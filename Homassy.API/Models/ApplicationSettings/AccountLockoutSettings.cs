namespace Homassy.API.Models.ApplicationSettings;

public class AccountLockoutSettings
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;
    public bool EnableProgressiveLockout { get; set; } = true;
    public int ProgressiveMultiplier { get; set; } = 2;
    public int MaxLockoutDurationMinutes { get; set; } = 60;
}
