using Serilog;
using Serilog.Events;

namespace Homassy.API.Extensions;

/// <summary>
/// Shared Serilog minimum level policy for every Homassy service.
/// EF Core writes the full SQL text of every command to the
/// <c>Microsoft.EntityFrameworkCore.Database.Command</c> category at Information level,
/// so that category is kept at Warning unless SQL logging is explicitly switched on.
/// This file is compiled into Homassy.Email as a link, so all services share one definition.
/// </summary>
public static class SerilogExtensions
{
    /// <summary>
    /// Environment variable that raises EF Core back to Information (SQL statements visible).
    /// Local debugging only – ignored when the service runs in the Production environment.
    /// </summary>
    public const string SqlLoggingEnvironmentVariable = "EFCORE_SQL_LOGGING";

    /// <summary>
    /// Applies the shared minimum levels. Every service must call this instead of
    /// configuring <c>MinimumLevel</c> by hand, so no service can accidentally start
    /// with SQL logging enabled.
    /// </summary>
    /// <param name="loggerConfiguration">The Serilog configuration being built.</param>
    /// <param name="defaultLevel">Default level for application code.</param>
    /// <param name="environmentName">
    /// Host environment name. When null it is read from the environment variables
    /// (falling back to Production, the safe default).
    /// </param>
    public static LoggerConfiguration UseHomassyMinimumLevels(
        this LoggerConfiguration loggerConfiguration,
        LogEventLevel defaultLevel = LogEventLevel.Information,
        string? environmentName = null)
    {
        var efCoreLevel = IsSqlLoggingEnabled(environmentName)
            ? LogEventLevel.Information
            : LogEventLevel.Warning;

        return loggerConfiguration
            .MinimumLevel.Is(defaultLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", efCoreLevel)
            .MinimumLevel.Override("System", LogEventLevel.Warning);
    }

    /// <summary>
    /// True only when <see cref="SqlLoggingEnvironmentVariable"/> is set to a truthy value
    /// and the service is not running in Production.
    /// </summary>
    private static bool IsSqlLoggingEnabled(string? environmentName)
    {
        var flag = Environment.GetEnvironmentVariable(SqlLoggingEnvironmentVariable);

        if (!bool.TryParse(flag, out var enabled) || !enabled)
        {
            return false;
        }

        environmentName ??= Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environments.Production;

        return !string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase);
    }
}
