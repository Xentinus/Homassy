using Homassy.API.Context;
using Homassy.Notifications.Endpoints;
using Homassy.Notifications.HealthChecks;
using Homassy.Notifications.Middleware;
using Homassy.Notifications.Services;
using Homassy.Notifications.Workers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Homassy.Notifications service");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, cfg) => cfg
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    HomassyDbContext.SetConfiguration(builder.Configuration);

    builder.Services.AddDbContext<HomassyDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Services
    builder.Services.AddSingleton<IWebPushService, WebPushService>();
    builder.Services.AddScoped<InventoryExpirationService>();
    builder.Services.AddHttpClient<EmailServiceClient>();

    // Background workers
    builder.Services.AddHostedService<PushNotificationSchedulerService>();
    builder.Services.AddHostedService<ShoppingListActivityMonitorService>();
    builder.Services.AddHostedService<EmailWeeklySummaryService>();

    // Health checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
        .AddCheck<WebPushHealthCheck>("webpush", tags: ["ready"]);

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseMiddleware<ApiKeyMiddleware>();

    app.MapPost("/push/test", TestPushEndpoint.HandleAsync);
    app.MapPost("/email/test", TestEmailEndpoint.HandleAsync);
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = hc => hc.Tags.Contains("ready")
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Homassy.Notifications service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
