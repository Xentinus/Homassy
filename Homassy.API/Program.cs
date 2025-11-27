using Homassy.API.Context;
using Homassy.API.Infrastructure;
using Homassy.API.Middleware;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.Reflection;
using System.Text;

// Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
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

    // Serilog integrálása ASP.NET Core-ba
    builder.Host.UseSerilog();

    // Set configuration for HomassyDbContext
    HomassyDbContext.SetConfiguration(builder.Configuration);

    // Initialize static services
    ConfigService.Initialize(builder.Configuration);
    EmailService.Initialize(builder.Configuration);
    JwtService.Initialize(builder.Configuration);

    // PostgreSQL Database
    builder.Services.AddDbContext<HomassyDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Background services
    builder.Services.AddHostedService<CacheManagementService>();
    builder.Services.AddHostedService<RateLimitCleanupService>();

    // API Versioning
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

    // JWT Authentication
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    var app = builder.Build();

    // Trigger inicializálás induláskor
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();
        var triggerInitializer = new DatabaseTriggerInitializer(dbContext);
        await triggerInitializer.InitializeTriggersAsync();
    }

    // Get application version from assembly
    var version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "1.0.0";

    Log.Information($"Homassy API version {version}");

    // Response headers middleware
    app.Use(async (context, next) =>
    {
        var requestId = Guid.NewGuid().ToString();
        context.Items["RequestId"] = requestId;
        
        context.Response.Headers.Append("X-Application-Name", "Homassy");
        context.Response.Headers.Append("X-Application-Version", version);
        context.Response.Headers.Append("X-Application-Description", "Home storage management system");
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'");
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Append("X-Request-ID", requestId);
        
        await next();
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    
  
    app.UseMiddleware<RateLimitingMiddleware>();
    app.UseAuthorization();
    app.UseMiddleware<SessionInfoMiddleware>();
    app.MapControllers();

    Log.Information("Homassy API started successfully");

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
