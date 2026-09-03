using Homassy.API.Context;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.HealthChecks;
using Homassy.API.Hubs;
using Homassy.API.Infrastructure;
using Homassy.API.Middleware;
using Homassy.API.Models.ApplicationSettings;
using Homassy.API.Models.HealthCheck;
using Homassy.API.Security;
using Homassy.API.Services;
using Homassy.API.Services.Background;
using Homassy.API.Services.Sanitization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;

Log.Logger = new LoggerConfiguration()
    .UseHomassyMinimumLevels(LogEventLevel.Debug)
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

    // A context is scoped to the operation, not to the request: the Functions layer takes
    // IDbContextFactory<HomassyDbContext> and disposes each context as the unit of work ends,
    // which is what returns the pooled connection and stops a change tracker outliving the
    // call that filled it. It also works unchanged in the background workers and cache
    // refreshes, which have no request scope to borrow a context from.
    //
    // The scoped registration stays for the consumers that legitimately want the ambient
    // context — startup trigger initialisation and the integration tests. Registering both
    // requires the options to be a singleton, which is what AddDbContextFactory installs.
    Action<DbContextOptionsBuilder> configureDbContext = options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));

    builder.Services.AddDbContextFactory<HomassyDbContext>(configureDbContext);
    builder.Services.AddDbContext<HomassyDbContext>(configureDbContext, optionsLifetime: ServiceLifetime.Singleton);

    builder.Services.AddHttpContextAccessor();

    // The Functions layer is the business logic, and it is scoped because that is the lifetime
    // of the work it does: a controller, hub, or worker scope resolves one and the contexts it
    // creates die with the operation. Consumers outside a request scope (the cache manager, the
    // automation worker) create their own scope and resolve from it.
    builder.Services.AddScoped<ActivityFunctions>();
    builder.Services.AddScoped<AutomationFunctions>();
    builder.Services.AddScoped<CalendarFunctions>();
    builder.Services.AddScoped<ExternalCalendarFunctions>();
    builder.Services.AddScoped<FamilyFunctions>();
    builder.Services.AddScoped<FamilyJoinRequestFunctions>();
    builder.Services.AddScoped<ImageFunctions>();
    builder.Services.AddScoped<LocationFunctions>();
    builder.Services.AddScoped<ProductFunctions>();
    builder.Services.AddScoped<PushNotificationFunctions>();
    builder.Services.AddScoped<SelectValueFunctions>();
    builder.Services.AddScoped<ShoppingListFunctions>();
    builder.Services.AddScoped<UserFunctions>();

    builder.Services.AddHostedService<CacheManagementService>();
    builder.Services.AddHostedService<RateLimitCleanupService>();

    builder.Services.AddSingleton<IInputSanitizationService, InputSanitizationService>();
    builder.Services.AddSingleton<IBarcodeValidationService, BarcodeValidationService>();
    builder.Services.AddSingleton<IImageProcessingService, ImageProcessingService>();
    builder.Services.AddSingleton<IProgressTrackerService, ProgressTrackerService>();
    builder.Services.AddSingleton<StatisticsService>();
    builder.Services.AddHostedService<StatisticsRefreshWorker>();

    // External calendar sync. The URL is user-supplied, so this client is deliberately the
    // most restricted one in the application.
    ExternalUrlGuard.AllowInsecureScheme = builder.Environment.IsDevelopment();

    builder.Services.AddHttpClient("ExternalCalendarSync", client =>
        {
            // A slow feed must not tie up the sync worker: the other calendars still have to run.
            client.Timeout = TimeSpan.FromSeconds(20);
            // An ICS feed is text; a few MB is generous. Without this the whole body is
            // buffered into memory, so one large response is an OOM lever.
            client.MaxResponseContentBufferSize = 5 * 1024 * 1024;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            // A 302 to an internal address would otherwise walk straight past every check made
            // on the URL the user actually supplied.
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            AutomaticDecompression = DecompressionMethods.All,
            // The real SSRF gate: the host is re-resolved and screened here, immediately before
            // the socket opens, so re-pointing DNS after the calendar was saved changes nothing.
            ConnectCallback = ExternalUrlGuard.CreateConnectCallback()
        });
    builder.Services.AddHostedService<ExternalCalendarSyncService>();

    // Kratos service registration
    builder.Services.AddHttpClient<IKratosService, KratosService>();

    // Notifications service proxy client
    builder.Services.AddHttpClient<NotificationsServiceClient>();

    var forwardedHeadersSettings = builder.Configuration.GetSection("ForwardedHeaders").Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

    if (forwardedHeadersSettings.Enabled)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = forwardedHeadersSettings.ForwardLimit;

            // The defaults trust loopback only, which is wrong for a container that is
            // reached over the Docker bridge. The trusted set is configuration-driven,
            // so an unconfigured deployment trusts nothing instead of trusting everything.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var network in forwardedHeadersSettings.KnownNetworks)
            {
                if (System.Net.IPNetwork.TryParse(network, out var parsedNetwork))
                {
                    options.KnownIPNetworks.Add(parsedNetwork);
                }
                else
                {
                    Log.Warning($"Ignoring unparseable ForwardedHeaders:KnownNetworks entry '{network}'");
                }
            }

            foreach (var proxy in forwardedHeadersSettings.KnownProxies)
            {
                if (IPAddress.TryParse(proxy, out var parsedProxy))
                {
                    options.KnownProxies.Add(parsedProxy);
                }
                else
                {
                    Log.Warning($"Ignoring unparseable ForwardedHeaders:KnownProxies entry '{proxy}'");
                }
            }

            if (options.KnownIPNetworks.Count == 0 && options.KnownProxies.Count == 0)
            {
                Log.Warning("ForwardedHeaders is enabled but no known proxy or network is configured; forwarded headers will be ignored");
            }
        });
    }

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

    var allowedOrigins = CorsOriginPolicy.ParseAllowedOrigins(
        builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []);

    if (allowedOrigins.Count == 0 && !builder.Environment.IsDevelopment())
    {
        // Production gets Cors__AllowedOrigins__0 from docker-compose.production.yml. An empty
        // list is a broken deployment, not a stricter one: every cross-origin call fails.
        Log.Warning($"No Cors:AllowedOrigins configured in the {builder.Environment.EnvironmentName} environment; every cross-origin request will be rejected");
    }

    var allowLoopbackOrigins = builder.Environment.IsDevelopment();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("HomassyPolicy", policy =>
        {
            // The loopback shortcut exists so local dev does not have to enumerate every
            // dev-server port. It is decided once, here, rather than inside the predicate:
            // outside Development the allowlist is the only thing that grants an origin.
            policy.SetIsOriginAllowed(origin =>
                    (allowLoopbackOrigins && CorsOriginPolicy.IsLoopback(origin))
                    || CorsOriginPolicy.IsAllowed(origin, allowedOrigins))
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

    // SignalR realtime hub for shopping lists. camelCase payloads match the MVC JSON
    // output and the frontend TypeScript types (publicId, shoppingListPublicId, ...).
    builder.Services.AddSignalR()
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
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

    var healthChecksBuilder = builder.Services.AddHealthChecks();

    var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(dbConnectionString))
    {
        healthChecksBuilder.AddNpgSql(
            dbConnectionString,
            name: "database",
            tags: ["db", "ready"]);
    }

    healthChecksBuilder.AddCheck<OpenFoodFactsHealthCheck>(
        "openfoodfacts",
        tags: ["external"]);

    var app = builder.Build();

    Homassy.API.Infrastructure.ServiceLocator.Provider = app.Services;

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();
        var triggerInitializer = new DatabaseTriggerInitializer(dbContext);
        await triggerInitializer.InitializeTriggersAsync();
    }

    Log.Information($"Homassy API version {version}");

    // Must be the first middleware: everything downstream (rate limiting, request
    // logging, HTTPS redirection) reads Connection.RemoteIpAddress and Request.Scheme,
    // and neither is trustworthy until the forwarded chain has been unwound.
    if (forwardedHeadersSettings.Enabled)
    {
        app.UseForwardedHeaders();
    }

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
        // AllowAnonymous so KratosSessionMiddleware skips it the same way it skips the other
        // public endpoints — the check reads endpoint metadata, not paths.
        app.MapOpenApi().AllowAnonymous();
    }

    if (httpsSettings.Enabled && httpsSettings.Hsts.Enabled && !app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    if (httpsSettings.Enabled && !app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // Explicit, so that the middleware below runs with the matched endpoint available:
    // rate limiting keys on the route template, and the Kratos middleware reads
    // [AllowAnonymous] from the endpoint metadata.
    app.UseRouting();

    app.UseCors("HomassyPolicy");
    app.UseMiddleware<RateLimitingMiddleware>();
    app.UseMiddleware<KratosSessionMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<SessionInfoMiddleware>();
    app.MapControllers();

    // Realtime shopping list channel. Auth flows through KratosSessionMiddleware (above) just
    // like the controllers; RequireCors is explicit because credentialed WS negotiation is strict.
    app.MapHub<ShoppingListHub>("/hubs/shopping-list").RequireCors("HomassyPolicy");

    // Realtime Készletek (inventory) channel — per-family / per-user groups joined on connect.
    app.MapHub<InventoryHub>("/hubs/inventory").RequireCors("HomassyPolicy");

    // Realtime Törzsadatok (master-data) channel — per-family / per-user groups joined on connect.
    app.MapHub<MasterDataHub>("/hubs/master-data").RequireCors("HomassyPolicy");

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
