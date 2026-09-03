using Homassy.API.Context;
using Homassy.API.Extensions;
using Homassy.Notifications.Endpoints;
using Homassy.Notifications.HealthChecks;
using Homassy.Notifications.Middleware;
using Homassy.Notifications.Services;
using Homassy.Notifications.Workers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .UseHomassyMinimumLevels()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Homassy.Notifications service");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, cfg) => cfg
        .UseHomassyMinimumLevels(environmentName: ctx.HostingEnvironment.EnvironmentName)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    HomassyDbContext.SetConfiguration(builder.Configuration);

    // Mirrors Homassy.API: the Functions layer this service borrows takes its contexts from
    // the factory, one per operation. Registering the factory alongside the scoped context
    // requires the options to be a singleton, which AddDbContextFactory installs.
    Action<DbContextOptionsBuilder> configureDbContext = options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));

    builder.Services.AddDbContextFactory<HomassyDbContext>(configureDbContext);
    builder.Services.AddDbContext<HomassyDbContext>(configureDbContext, optionsLifetime: ServiceLifetime.Singleton);

    // Services
    builder.Services.AddSingleton<IWebPushService, WebPushService>();
    builder.Services.AddSingleton<FamilyPushNotifier>();
    builder.Services.AddScoped<InventoryExpirationService>();
    builder.Services.AddHttpClient<EmailServiceClient>();
    builder.Services.AddHttpClient<InventoryBroadcastServiceClient>();

    // Background workers
    builder.Services.AddHostedService<PushNotificationSchedulerService>();
    builder.Services.AddHostedService<ShoppingListActivityMonitorService>();
    builder.Services.AddHostedService<InventoryActivityMonitorService>();
    builder.Services.AddHostedService<FamilyJoinRequestMonitorService>();
    builder.Services.AddHostedService<EmailWeeklySummaryService>();
    builder.Services.AddHostedService<ItemAutomationWorkerService>();
    builder.Services.AddHostedService<ExternalCalendarReminderService>();

    // Health checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
        .AddCheck<WebPushHealthCheck>("webpush", tags: ["ready"]);

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseMiddleware<ApiKeyMiddleware>();

    app.MapPost("/push/test", TestPushEndpoint.HandleAsync);
    app.MapPost("/push/low-stock", LowStockPushEndpoint.HandleAsync);
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
