using Homassy.API.Context;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.HealthChecks;
using Homassy.API.Infrastructure;
using Homassy.API.Middleware;
using Homassy.API.Models.ApplicationSettings;
using Homassy.API.Models.HealthCheck;
using Homassy.API.Security;
using Homassy.API.Services;
using Homassy.API.Services.Background;
using Homassy.API.Services.Sanitization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "Logs/Homassy-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        encoding: Encoding.UTF8,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: LogEventLevel.Debug)
    .CreateLogger();

try
{
    Log.Information("Starting Homassy API");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    HomassyDbContext.SetConfiguration(builder.Configuration);

    ConfigService.Initialize(builder.Configuration);

    builder.Services.AddDbContext<HomassyDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddHostedService<CacheManagementService>();
    builder.Services.AddHostedService<RateLimitCleanupService>();

    builder.Services.AddSingleton<IInputSanitizationService, InputSanitizationService>();
    builder.Services.AddSingleton<IBarcodeValidationService, BarcodeValidationService>();
    builder.Services.AddSingleton<IImageProcessingService, ImageProcessingService>();
    builder.Services.AddSingleton<IProgressTrackerService, ProgressTrackerService>();

    // Kratos service registration
    builder.Services.AddHttpClient<IKratosService, KratosService>();

    builder.Services.AddSingleton<IWebPushService, WebPushService>();
    builder.Services.AddHostedService<PushNotificationSchedulerService>();
    builder.Services.AddHostedService<ShoppingListActivityMonitorService>();

    builder.Services.Configure<HttpsSettings>(builder.Configuration.GetSection("Https"));
    builder.Services.Configure<RequestTimeoutSettings>(builder.Configuration.GetSection("RequestTimeout"));
    builder.Services.Configure<HealthCheckOptions>(builder.Configuration.GetSection("HealthChecks"));
    builder.Services.Configure<GracefulShutdownSettings>(builder.Configuration.GetSection("GracefulShutdown"));

    var httpsSettings = builder.Configuration.GetSection("Https").Get<HttpsSettings>() ?? new HttpsSettings();
    var gracefulShutdownSettings = builder.Configuration.GetSection("GracefulShutdown").Get<GracefulShutdownSettings>() ?? new GracefulShutdownSettings();

    if (gracefulShutdownSettings.Enabled)
    {
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.AddServerHeader = false;
        });
    }

    builder.Services.AddHostedService<GracefulShutdownService>();

    if (httpsSettings.Enabled && httpsSettings.Hsts.Enabled)
    {
        builder.Services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(httpsSettings.Hsts.MaxAgeDays);
            options.IncludeSubDomains = httpsSettings.Hsts.IncludeSubDomains;
            options.Preload = httpsSettings.Hsts.Preload;
        });
    }

    if (httpsSettings.Enabled && httpsSettings.HttpsPort.HasValue)
    {
        builder.Services.AddHttpsRedirection(options =>
        {
            options.HttpsPort = httpsSettings.HttpsPort.Value;
        });
    }

    var version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "1.0.0";

    builder.Services.AddHttpClient<OpenFoodFactsService>(client =>
    {
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Homassy", version));
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("(https://github.com/Xentinus/Homassy)"));
    });

    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
            new Asp.Versioning.UrlSegmentApiVersionReader()
        );
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // Kratos-based authentication - session validation happens in KratosSessionMiddleware
    // This sets up a basic authentication scheme for the [Authorize] attribute
    builder.Services.AddAuthentication("Kratos")
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, KratosAuthenticationHandler>(
            "Kratos", options => { });

    builder.Services.AddAuthorization();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("HomassyPolicy", policy =>
        {
            policy.SetIsOriginAllowed(origin =>
                {
                    if (new Uri(origin).Host == "localhost")
                        return true;
                    
                    return allowedOrigins.Contains(origin);
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        });
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new()
            {
                Title = "Homassy API",
                Version = version,
                Description = "Home storage management system API - Manage products, inventory, shopping lists, and family sharing.",
                Contact = new()
                {
                    Name = "Homassy",
                    Url = new Uri("https://github.com/Xentinus/Homassy")
                },
                License = new()
                {
                    Name = "MIT License",
                    Url = new Uri("https://github.com/Xentinus/Homassy/blob/master/LICENSE")
                }
            };
            return Task.CompletedTask;
        });
    });

    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        [
            "application/json",
            "application/problem+json",
            "text/json"
        ]);
    });

    builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.Optimal;
    });

    builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.Optimal;
    });

    builder.Services.AddHttpClient("OpenFoodFactsHealthCheck");

    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "database",
            tags: ["db", "ready"])
        .AddCheck<OpenFoodFactsHealthCheck>(
            "openfoodfacts",
            tags: ["external"]);

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();
        var triggerInitializer = new DatabaseTriggerInitializer(dbContext);
        await triggerInitializer.InitializeTriggersAsync();
    }

    Log.Information($"Homassy API version {version}");

    app.UseResponseCompression();

    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Application-Name", "Homassy");
        context.Response.Headers.Append("X-Application-Version", version);
        context.Response.Headers.Append("X-Application-Description", "Home storage management system");
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'");
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        
        await next();
    });

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RequestTimeoutMiddleware>();
    app.UseRequestLogging(builder.Configuration);
    app.UseMiddleware<GlobalExceptionMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    if (httpsSettings.Enabled && httpsSettings.Hsts.Enabled && !app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    if (httpsSettings.Enabled && !app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseCors("HomassyPolicy");
    app.UseMiddleware<RateLimitingMiddleware>();
    app.UseMiddleware<KratosSessionMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<SessionInfoMiddleware>();
    app.MapControllers();

    Log.Information("Homassy API started successfully");

    if (gracefulShutdownSettings.Enabled)
    {
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() =>
        {
            Log.Information("Shutdown signal received, waiting for active requests to complete");
            Thread.Sleep(TimeSpan.FromSeconds(gracefulShutdownSettings.TimeoutSeconds));
        });
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("Shutting down Homassy API");
    await Log.CloseAndFlushAsync();
}

public partial class Program { }
