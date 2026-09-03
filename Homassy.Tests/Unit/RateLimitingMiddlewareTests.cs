using Homassy.API.Middleware;
using Homassy.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.Json;

namespace Homassy.Tests.Unit;

public class RateLimitingMiddlewareTests : IDisposable
{
    // Static: the rate-limit store is process-wide, and xUnit builds a new instance per
    // test — a per-instance counter would hand several tests the same "unique" IP.
    private static int _testCounter;

    public RateLimitingMiddlewareTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "100",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "30",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();

        ConfigService.Initialize(config);
    }

    public void Dispose()
    {
        ConfigService.ClearCache();
        GC.SuppressFinalize(this);
    }

    private static RateLimitingMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new RateLimitingMiddleware(next);
    }

    private DefaultHttpContext CreateHttpContext(string path = "/api/test", string? ip = null)
    {
        ip ??= GetUniqueIp();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    private static string GetUniqueIp()
    {
        var counter = Interlocked.Increment(ref _testCounter);
        var b1 = (counter / 65536) % 256;
        var b2 = (counter / 256) % 256;
        var b3 = counter % 256;
        return $"192.{b1}.{b2}.{b3}";
    }

    #region Rate Limited Response Tests

    [Fact]
    public async Task InvokeAsync_WhenRateLimited_Returns429()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "100",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "2",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();
        ConfigService.Initialize(config);

        var uniqueIp = GetUniqueIp();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        for (int i = 0; i < 2; i++)
        {
            var context = CreateHttpContext(path: "/api/rate-test-429", ip: uniqueIp);
            await middleware.InvokeAsync(context);
        }

        var rateLimitedContext = CreateHttpContext(path: "/api/rate-test-429", ip: uniqueIp);
        await middleware.InvokeAsync(rateLimitedContext);

        Assert.Equal(429, rateLimitedContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenRateLimited_AddsRateLimitLimitHeader()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "100",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "2",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();
        ConfigService.Initialize(config);

        var uniqueIp = GetUniqueIp();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        for (int i = 0; i < 2; i++)
        {
            var context = CreateHttpContext(path: "/api/rate-test-limit-header", ip: uniqueIp);
            await middleware.InvokeAsync(context);
        }

        var rateLimitedContext = CreateHttpContext(path: "/api/rate-test-limit-header", ip: uniqueIp);
        await middleware.InvokeAsync(rateLimitedContext);

        Assert.True(rateLimitedContext.Response.Headers.ContainsKey(RateLimitingMiddleware.RateLimitLimitHeader));
        var limitHeader = rateLimitedContext.Response.Headers[RateLimitingMiddleware.RateLimitLimitHeader].ToString();
        Assert.Equal("2", limitHeader);
    }

    [Fact]
    public async Task InvokeAsync_WhenRateLimited_AddsRetryAfterHeader()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "100",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "2",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();
        ConfigService.Initialize(config);

        var uniqueIp = GetUniqueIp();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        for (int i = 0; i < 2; i++)
        {
            var context = CreateHttpContext(path: "/api/rate-test-retry", ip: uniqueIp);
            await middleware.InvokeAsync(context);
        }

        var rateLimitedContext = CreateHttpContext(path: "/api/rate-test-retry", ip: uniqueIp);
        await middleware.InvokeAsync(rateLimitedContext);

        Assert.True(rateLimitedContext.Response.Headers.ContainsKey(RateLimitingMiddleware.RetryAfterHeader));
        var retryAfter = rateLimitedContext.Response.Headers[RateLimitingMiddleware.RetryAfterHeader].ToString();
        Assert.True(int.TryParse(retryAfter, out var seconds));
        Assert.True(seconds > 0);
    }

    [Fact]
    public async Task InvokeAsync_WhenRateLimited_RemainingIsZero()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "100",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "2",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();
        ConfigService.Initialize(config);

        var uniqueIp = GetUniqueIp();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        for (int i = 0; i < 2; i++)
        {
            var context = CreateHttpContext(path: "/api/rate-test-zero", ip: uniqueIp);
            await middleware.InvokeAsync(context);
        }

        var rateLimitedContext = CreateHttpContext(path: "/api/rate-test-zero", ip: uniqueIp);
        await middleware.InvokeAsync(rateLimitedContext);

        var remaining = rateLimitedContext.Response.Headers[RateLimitingMiddleware.RateLimitRemainingHeader].ToString();
        Assert.Equal("0", remaining);
    }

    [Fact]
    public async Task InvokeAsync_WhenRateLimited_ResetHeaderIsValidTimestamp()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "100",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "2",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();
        ConfigService.Initialize(config);

        var uniqueIp = GetUniqueIp();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        for (int i = 0; i < 2; i++)
        {
            var context = CreateHttpContext(path: "/api/rate-test-reset", ip: uniqueIp);
            await middleware.InvokeAsync(context);
        }

        var rateLimitedContext = CreateHttpContext(path: "/api/rate-test-reset", ip: uniqueIp);
        await middleware.InvokeAsync(rateLimitedContext);

        var resetHeader = rateLimitedContext.Response.Headers[RateLimitingMiddleware.RateLimitResetHeader].ToString();
        Assert.True(long.TryParse(resetHeader, out var resetTimestamp));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.True(resetTimestamp > now);
    }

    #endregion

    #region Middleware Pipeline Tests

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware_WhenNotRateLimited()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotCallNextMiddleware_WhenRateLimited()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "100",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "1",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();
        ConfigService.Initialize(config);

        var nextCallCount = 0;
        var uniqueIp = GetUniqueIp();
        var middleware = CreateMiddleware(_ =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        });

        var firstContext = CreateHttpContext(path: "/api/rate-test-next", ip: uniqueIp);
        await middleware.InvokeAsync(firstContext);

        var secondContext = CreateHttpContext(path: "/api/rate-test-next", ip: uniqueIp);
        await middleware.InvokeAsync(secondContext);

        Assert.Equal(1, nextCallCount);
    }

    #endregion

    #region Response Content Tests

    [Fact]
    public async Task InvokeAsync_WhenRateLimited_ReturnsJsonResponse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "100",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "1",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();
        ConfigService.Initialize(config);

        var uniqueIp = GetUniqueIp();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        var firstContext = CreateHttpContext(path: "/api/rate-test-json", ip: uniqueIp);
        await middleware.InvokeAsync(firstContext);

        var rateLimitedContext = CreateHttpContext(path: "/api/rate-test-json", ip: uniqueIp);
        await middleware.InvokeAsync(rateLimitedContext);

        Assert.Equal("application/json", rateLimitedContext.Response.ContentType);

        rateLimitedContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(rateLimitedContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        Assert.NotEmpty(body);
        Assert.Contains("success", body);
    }

    [Fact]
    public async Task InvokeAsync_WhenGlobalRateLimitExceeded_Returns429()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "2",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "100",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();
        ConfigService.Initialize(config);

        var uniqueIp = GetUniqueIp();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        for (int i = 0; i < 2; i++)
        {
            var context = CreateHttpContext(path: "/api/global-test", ip: uniqueIp);
            await middleware.InvokeAsync(context);
        }

        var rateLimitedContext = CreateHttpContext(path: "/api/global-test", ip: uniqueIp);
        await middleware.InvokeAsync(rateLimitedContext);

        Assert.Equal(429, rateLimitedContext.Response.StatusCode);
    }

    #endregion

    #region Forwarded Header Trust Tests

    [Fact]
    public async Task InvokeAsync_ForgedXForwardedFor_IsCountedAgainstTheConnectionAddress()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "100",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "2",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();
        ConfigService.Initialize(config);

        // One caller, a different fabricated X-Forwarded-For on every request. The
        // connection address is untrusted (no UseForwardedHeaders rewrote it), so the
        // header must not buy a fresh bucket.
        var connectionIp = GetUniqueIp();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        for (int i = 0; i < 2; i++)
        {
            var context = CreateHttpContext(path: "/api/forged-xff", ip: connectionIp);
            context.Request.Headers["X-Forwarded-For"] = $"203.0.113.{i}";
            await middleware.InvokeAsync(context);
        }

        var thirdContext = CreateHttpContext(path: "/api/forged-xff", ip: connectionIp);
        thirdContext.Request.Headers["X-Forwarded-For"] = "203.0.113.99";
        await middleware.InvokeAsync(thirdContext);

        Assert.Equal(429, thirdContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ForgedXRealIp_IsCountedAgainstTheConnectionAddress()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "2",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "100",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();
        ConfigService.Initialize(config);

        var connectionIp = GetUniqueIp();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        for (int i = 0; i < 2; i++)
        {
            var context = CreateHttpContext(path: "/api/forged-realip", ip: connectionIp);
            context.Request.Headers["X-Real-IP"] = $"198.51.100.{i}";
            await middleware.InvokeAsync(context);
        }

        var thirdContext = CreateHttpContext(path: "/api/forged-realip", ip: connectionIp);
        thirdContext.Request.Headers["X-Real-IP"] = "198.51.100.99";
        await middleware.InvokeAsync(thirdContext);

        Assert.Equal(429, thirdContext.Response.StatusCode);
    }

    #endregion

    #region Endpoint Key Bounding Tests

    [Fact]
    public void GetEndpointKey_WithoutAMatchedRoute_CollapsesEveryPathIntoOneBucket()
    {
        var keys = new HashSet<string>();

        for (int i = 0; i < 500; i++)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = $"/api/scan-{Guid.NewGuid()}";
            keys.Add(RateLimitingMiddleware.GetEndpointKey(context));
        }

        Assert.Single(keys);
        Assert.Equal(RateLimitingMiddleware.UnmatchedEndpointKey, keys.Single());
    }

    [Fact]
    public void GetEndpointKey_WithAMatchedRoute_UsesTheRouteTemplateNotTheConcretePath()
    {
        var keys = new HashSet<string>();

        for (int i = 0; i < 100; i++)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = $"/api/v1.0/product/{Guid.NewGuid()}";
            context.SetEndpoint(new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse("api/v{version:apiVersion}/Product/{publicId}"),
                0,
                EndpointMetadataCollection.Empty,
                "Product_Get"));

            keys.Add(RateLimitingMiddleware.GetEndpointKey(context));
        }

        Assert.Single(keys);
        Assert.Equal("api/v{version:apiversion}/product/{publicid}", keys.Single());
    }

    [Fact]
    public async Task InvokeAsync_ManyDistinctUnroutedPaths_ShareOneEndpointBucket()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalMaxAttempts"] = "100",
                ["RateLimiting:GlobalWindowMinutes"] = "1",
                ["RateLimiting:EndpointMaxAttempts"] = "2",
                ["RateLimiting:EndpointWindowMinutes"] = "1"
            })
            .Build();
        ConfigService.Initialize(config);

        var connectionIp = GetUniqueIp();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        for (int i = 0; i < 2; i++)
        {
            var context = CreateHttpContext(path: $"/api/unrouted-{Guid.NewGuid()}", ip: connectionIp);
            await middleware.InvokeAsync(context);
        }

        var thirdContext = CreateHttpContext(path: $"/api/unrouted-{Guid.NewGuid()}", ip: connectionIp);
        await middleware.InvokeAsync(thirdContext);

        Assert.Equal(429, thirdContext.Response.StatusCode);
    }

    #endregion

    #region Constants Tests

    [Fact]
    public void RateLimitLimitHeader_HasExpectedValue()
    {
        Assert.Equal("X-RateLimit-Limit", RateLimitingMiddleware.RateLimitLimitHeader);
    }

    [Fact]
    public void RateLimitRemainingHeader_HasExpectedValue()
    {
        Assert.Equal("X-RateLimit-Remaining", RateLimitingMiddleware.RateLimitRemainingHeader);
    }

    [Fact]
    public void RateLimitResetHeader_HasExpectedValue()
    {
        Assert.Equal("X-RateLimit-Reset", RateLimitingMiddleware.RateLimitResetHeader);
    }

    [Fact]
    public void RetryAfterHeader_HasExpectedValue()
    {
        Assert.Equal("Retry-After", RateLimitingMiddleware.RetryAfterHeader);
    }

    #endregion
}
