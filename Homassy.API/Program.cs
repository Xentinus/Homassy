using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.HealthChecks;
using Homassy.API.Hubs;
using Homassy.API.Infrastructure;
using Homassy.API.Infrastructure.Caching;
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
using Homassy.Data.Context;
using Homassy.Data.Validation;
using Homassy.Data.Functions;
using Homassy.Data.Extensions;

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
    //
    // Write-through cache refresh (see CacheWriteThrough): interceptors are the one place every
    // write passes through, which is what keeps this off the ~110 individual save sites. Singletons,
    // because the options they are attached to are singletons.
    builder.Services.AddSingleton<CacheWriteThrough>();
    builder.Services.AddSingleton<CacheWriteThroughSaveInterceptor>();
    builder.Services.AddSingleton<CacheWriteThroughTransactionInterceptor>();

    Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext = (services, options) =>
        options
            .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
            .AddInterceptors(
                services.GetRequiredService<CacheWriteThroughSaveInterceptor>(),
                services.GetRequiredService<CacheWriteThroughTransactionInterceptor>());

    builder.Services.AddDbContextFactory<HomassyDbContext>(configureDbContext);
    builder.Services.AddDbContext<HomassyDbContext>(configureDbContext, optionsLifetime: ServiceLifetime.Singleton);

    builder.Services.AddHttpContextAccessor();

    // SignalR broadcast helpers. Singletons over IHubContext<T>, which is itself a singleton;
    // the Functions layer reaches them through FunctionsRuntime, and InternalController injects
    // the inventory one directly to relay out-of-process changes.
    builder.Services.AddSingleton<InventoryRealtime>();
    builder.Services.AddSingleton<MasterDataRealtime>();
    builder.Services.AddSingleton<ShoppingListRealtime>();
    builder.Services.AddSingleton<ShoppingListPresence>();
    builder.Services.AddSingleton<FamilyChatRealtime>();
    builder.Services.AddSingleton<FamilyChatConnectionState>();

    // The cross-cutting services the Functions layer needs, as one typed parameter object.
    // See FunctionsRuntime for why it is a bundle rather than separate constructor parameters.
    builder.Services.AddSingleton<FunctionsRuntime>();

    // The Functions layer is the business logic, and it is scoped because that is the lifetime
    // of the work it does: a controller, hub, or worker scope resolves one and the contexts it
    // creates die with the operation. Consumers outside a request scope (the cache manager, the
    // automation worker) create their own scope and resolve from it.
    builder.Services.AddScoped<ActivityFunctions>();
    builder.Services.AddScoped<AutomationFunctions>();
    builder.Services.AddScoped<CalendarFunctions>();
    builder.Services.AddScoped<ExternalCalendarFunctions>();
    builder.Services.AddScoped<CalendarNoteFunctions>();
    builder.Services.AddScoped<FamilyCache>();
    builder.Services.AddScoped<FamilyChatFunctions>();
    builder.Services.AddScoped<FamilyFunctions>();
    builder.Services.AddScoped<FamilyJoinRequestFunctions>();
    builder.Services.AddScoped<ImageFunctions>();
    builder.Services.AddScoped<InsightFunctions>();
    builder.Services.AddScoped<LocationFunctions>();
    builder.Services.AddScoped<LowStockAutomationFunctions>();
    builder.Services.AddScoped<NotificationFunctions>();
    builder.Services.AddScoped<PriceInsightFunctions>();
    builder.Services.AddScoped<ProductFunctions>();
    builder.Services.AddScoped<PushNotificationFunctions>();
    builder.Services.AddScoped<SearchFunctions>();
    builder.Services.AddScoped<SelectValueFunctions>();
    builder.Services.AddScoped<ShoppingListFunctions>();
    builder.Services.AddScoped<UserFunctions>();

    builder.Services.AddHostedService<CacheManagementService>();
    builder.Services.AddHostedService<RateLimitCleanupService>();
    // Retires expired chat typing and watching flags and broadcasts the change (#148, #149).
    // Paired with
    // FamilyChatConnectionState.TypingTtl - see the service for why its interval is not a knob.
    builder.Services.AddHostedService<FamilyChatStateSweepService>();

    builder.Services.AddSingleton<IInputSanitizationService, InputSanitizationService>();
    builder.Services.AddSingleton<IBarcodeValidationService, BarcodeValidationService>();
    builder.Services.AddSingleton<IImageProcessingService, ImageProcessingService>();
    builder.Services.AddSingleton<IProgressTrackerService, ProgressTrackerService>();
    builder.Services.AddSingleton<StatisticsService>();
    builder.Services.AddHostedService<StatisticsRefreshWorker>();

    // Per-family TTL cache backing the R5 "Insight" aggregation endpoints. Singleton for the same
    // reason as StatisticsService above: it is the in-memory store itself, not a per-request view
    // over one. See FamilyInsightsCache's own doc comment for the single-flight/isolation
    // guarantees and the process-local scope this registration implies.
    builder.Services.AddSingleton<FamilyInsightsCache>();

    // #127: last-seen is stamped in memory on the request path and written in batches, so the
    // tracker is a singleton (every request writes to the same dictionary) and the flush service
    // is the only thing that touches the column. See LastSeenTracker's own remarks for why this is
    // not a row update per request.
    builder.Services.AddSingleton<LastSeenTracker>();
    builder.Services.AddHostedService<LastSeenFlushService>();
    // Sweeps expired entries out of the cache above on a timer, the same way RateLimitCleanupService
    // does for RateLimitService: nothing else ever removes an entry from FamilyInsightsCache just
    // because it expired, so without this a key nobody queries again would never be reclaimed.
    builder.Services.AddHostedService<FamilyInsightsCacheCleanupService>();

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

    // ValidateOnStart, so a non-numeric or out-of-range limit stops the host here rather than
    // turning every request - the health endpoints included - into a 500 (#82).
    builder.Services
        .AddOptions<RateLimitSettings>()
        .Bind(builder.Configuration.GetSection("RateLimiting"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    var httpsSettings = builder.Configuration.GetSection("Https").Get<HttpsSettings>() ?? new HttpsSettings();
    var gracefulShutdownSettings = builder.Configuration.GetSection("GracefulShutdown").Get<GracefulShutdownSettings>() ?? new GracefulShutdownSettings();

    if (gracefulShutdownSettings.Enabled)
    {
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.AddServerHeader = false;
        });

        // This is the whole drain. On the stop signal Kestrel stops accepting connections and
        // waits for the requests already in flight, for up to this long, then abandons them —
        // which is exactly the behaviour the old Thread.Sleep in the ApplicationStopping callback
        // was hand-rolling, except that the sleep waited out the full timeout whether or not
        // anything was in flight. The host's own default is 5 seconds, so without this line the
        // configured TimeoutSeconds would not have been the number that governs anything (#85).
        builder.Services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(gracefulShutdownSettings.TimeoutSeconds));
    }

    // Diagnostics for the drain above: how many requests the stop is waiting for, and whether
    // they finished inside the window. Paired with InFlightRequestMiddleware.
    builder.Services.AddSingleton<InFlightRequestTracker>();
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
                // The project is AGPL-3.0, not MIT, and the repository file is LICENSE.txt.
                // The SPDX expression goes in Name so that tooling matching on a license string
                // gets the right one: the document is OpenAPI 3.1, where the dedicated
                // `identifier` field is mutually exclusive with `url`, and a link a reader can
                // open is worth more here than the field it would have to displace.
                License = new()
                {
                    Name = "AGPL-3.0-only",
                    Url = new Uri("https://github.com/Xentinus/Homassy/blob/master/LICENSE.txt")
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

    // Straight after the forwarded-header unwind, so every request the server accepted is counted
    // for the shutdown drain — including the ones that never reach a controller.
    app.UseMiddleware<InFlightRequestMiddleware>();

    app.UseResponseCompression();

    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Application-Name", "Homassy");
        context.Response.Headers.Append("X-Application-Version", version);
        context.Response.Headers.Append("X-Application-Description", "Home storage management system");
        // These cover the API's own responses only — /api/v* and /hubs/*. The documents the
        // browser actually loads come from the Nuxt container, and get their headers from
        // Homassy.Proxy/Caddyfile, which deliberately skips the two API routes: two
        // Content-Security-Policy headers on one response are enforced as the intersection of
        // both. The API also runs without that proxy in development, so it keeps its own set.
        // X-XSS-Protection is gone: no current browser implements it, and the XSS auditor it
        // used to switch on was removed for being an information-disclosure vector of its own.
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
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

    // Realtime family chat channel (#144) - one group per family, joined when the chat panel
    // opens rather than on connect.
    app.MapHub<FamilyChatHub>("/hubs/family-chat").RequireCors("HomassyPolicy");

    Log.Information("Homassy API started successfully");

    // Nothing to register for shutdown here. The drain is HostOptions.ShutdownTimeout (set
    // above); what is waiting for what is reported by GracefulShutdownService.
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    // Reached on SIGTERM as well: RunAsync returns once the host has stopped, so the shutdown
    // log lines are still buffered when this flushes them. Nothing may block before here — the
    // Thread.Sleep this replaced was reliably SIGKILLed mid-sleep, taking the buffer with it.
    Log.Information("Shutting down Homassy API");
    await Log.CloseAndFlushAsync();
}

public partial class Program { }
