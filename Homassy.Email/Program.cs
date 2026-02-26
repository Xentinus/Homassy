using Homassy.Email.Endpoints;
using Homassy.Email.HealthChecks;
using Homassy.Email.Middleware;
using Homassy.Email.Services;
using Homassy.Email.Workers;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
    Log.Information("Starting Homassy.Email service");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, cfg) => cfg
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddSingleton<IEmailQueueService, EmailQueueService>();
    builder.Services.AddSingleton<ITemplateRendererService, TemplateRendererService>();
    builder.Services.AddSingleton<IEmailContentService, EmailContentService>();
    builder.Services.AddScoped<IEmailSenderService, EmailSenderService>();
    builder.Services.AddHostedService<EmailWorkerService>();
    builder.Services.AddHealthChecks()
        .AddCheck<SmtpHealthCheck>("smtp", tags: ["ready"]);

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseMiddleware<ApiKeyMiddleware>();

    app.MapPost("/kratos/webhook", KratosWebhookEndpoint.HandleAsync);
    app.MapPost("/email/send", SendEmailEndpoint.HandleAsync);
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = hc => hc.Tags.Contains("ready")
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Homassy.Email service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
