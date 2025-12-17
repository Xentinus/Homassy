using Homassy.API.Exceptions;
using Homassy.API.Middleware;
using Homassy.API.Models.ApplicationSettings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Homassy.Tests.Unit;

public class RequestTimeoutMiddlewareTests
{
    private static RequestTimeoutMiddleware CreateMiddleware(RequestDelegate next, RequestTimeoutSettings? settings = null)
    {
        settings ??= new RequestTimeoutSettings { DefaultTimeoutSeconds = 30 };
        var options = Options.Create(settings);
        return new RequestTimeoutMiddleware(next, options);
    }

    private static DefaultHttpContext CreateHttpContext(string path = "/api/test")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    #region Normal Request Tests

    [Fact]
    public async Task InvokeAsync_WhenRequestCompletesWithinTimeout_CallsNextMiddleware()
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
    public async Task InvokeAsync_WhenRequestCompletesWithinTimeout_DoesNotThrow()
    {
        var middleware = CreateMiddleware(async _ =>
        {
            await Task.Delay(10);
        }, new RequestTimeoutSettings { DefaultTimeoutSeconds = 5 });
        var context = CreateHttpContext();

        var exception = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(exception);
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public async Task InvokeAsync_WhenRequestExceedsTimeout_ThrowsRequestTimeoutException()
    {
        var settings = new RequestTimeoutSettings { DefaultTimeoutSeconds = 1 };
        var middleware = CreateMiddleware(async ctx =>
        {
            await Task.Delay(5000, ctx.RequestAborted);
        }, settings);
        var context = CreateHttpContext();

        await Assert.ThrowsAsync<RequestTimeoutException>(() => middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestExceedsTimeout_ExceptionContainsTimeoutDuration()
    {
        var settings = new RequestTimeoutSettings { DefaultTimeoutSeconds = 1 };
        var middleware = CreateMiddleware(async ctx =>
        {
            await Task.Delay(5000, ctx.RequestAborted);
        }, settings);
        var context = CreateHttpContext();

        var exception = await Assert.ThrowsAsync<RequestTimeoutException>(() => middleware.InvokeAsync(context));

        Assert.Contains("1", exception.Message);
        Assert.Contains("seconds", exception.Message);
    }

    #endregion

    #region Endpoint-Specific Timeout Tests

    [Fact]
    public async Task InvokeAsync_WhenEndpointMatchesPattern_UsesEndpointTimeout()
    {
        var settings = new RequestTimeoutSettings
        {
            DefaultTimeoutSeconds = 1,
            Endpoints =
            [
                new EndpointTimeoutSettings
                {
                    PathPattern = "/api/slow/*",
                    TimeoutSeconds = 5
                }
            ]
        };
        var middleware = CreateMiddleware(async ctx =>
        {
            await Task.Delay(2000, ctx.RequestAborted);
        }, settings);
        var context = CreateHttpContext("/api/slow/endpoint");

        var exception = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(exception);
    }

    [Fact]
    public async Task InvokeAsync_WhenEndpointDoesNotMatchPattern_UsesDefaultTimeout()
    {
        var settings = new RequestTimeoutSettings
        {
            DefaultTimeoutSeconds = 1,
            Endpoints =
            [
                new EndpointTimeoutSettings
                {
                    PathPattern = "/api/slow/*",
                    TimeoutSeconds = 10
                }
            ]
        };
        var middleware = CreateMiddleware(async ctx =>
        {
            await Task.Delay(5000, ctx.RequestAborted);
        }, settings);
        var context = CreateHttpContext("/api/fast/endpoint");

        await Assert.ThrowsAsync<RequestTimeoutException>(() => middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_WhenMultiplePatternsMatch_UsesFirstMatch()
    {
        var settings = new RequestTimeoutSettings
        {
            DefaultTimeoutSeconds = 1,
            Endpoints =
            [
                new EndpointTimeoutSettings
                {
                    PathPattern = "/api/test/*",
                    TimeoutSeconds = 5
                },
                new EndpointTimeoutSettings
                {
                    PathPattern = "/api/*",
                    TimeoutSeconds = 10
                }
            ]
        };
        var middleware = CreateMiddleware(async ctx =>
        {
            await Task.Delay(3000, ctx.RequestAborted);
        }, settings);
        var context = CreateHttpContext("/api/test/endpoint");

        var exception = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(exception);
    }

    [Fact]
    public async Task InvokeAsync_WhenPatternHasWildcardInMiddle_MatchesCorrectly()
    {
        var settings = new RequestTimeoutSettings
        {
            DefaultTimeoutSeconds = 1,
            Endpoints =
            [
                new EndpointTimeoutSettings
                {
                    PathPattern = "/api/*/openfoodfacts/*",
                    TimeoutSeconds = 5
                }
            ]
        };
        var middleware = CreateMiddleware(async ctx =>
        {
            await Task.Delay(2000, ctx.RequestAborted);
        }, settings);
        var context = CreateHttpContext("/api/v1.0/openfoodfacts/search");

        var exception = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(exception);
    }

    #endregion

    #region Client Cancellation Tests

    [Fact]
    public async Task InvokeAsync_WhenClientCancels_ThrowsOperationCanceledExceptionOrDerived()
    {
        var clientCts = new CancellationTokenSource();
        var settings = new RequestTimeoutSettings { DefaultTimeoutSeconds = 30 };
        var middleware = CreateMiddleware(async ctx =>
        {
            await Task.Delay(5000, ctx.RequestAborted);
        }, settings);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        var requestAbortedField = typeof(DefaultHttpContext).GetProperty("RequestAborted");
        requestAbortedField?.SetValue(context, clientCts.Token);

        clientCts.CancelAfter(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task InvokeAsync_WhenPathIsNull_UsesDefaultTimeout()
    {
        var settings = new RequestTimeoutSettings { DefaultTimeoutSeconds = 5 };
        var middleware = CreateMiddleware(async ctx =>
        {
            await Task.Delay(100, ctx.RequestAborted);
        }, settings);
        
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(exception);
    }

    [Fact]
    public async Task InvokeAsync_WhenEndpointsListIsEmpty_UsesDefaultTimeout()
    {
        var settings = new RequestTimeoutSettings
        {
            DefaultTimeoutSeconds = 5,
            Endpoints = []
        };
        var middleware = CreateMiddleware(async ctx =>
        {
            await Task.Delay(100, ctx.RequestAborted);
        }, settings);
        var context = CreateHttpContext();

        var exception = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(exception);
    }

    [Fact]
    public async Task InvokeAsync_WhenPatternIsCaseInsensitive_MatchesCorrectly()
    {
        var settings = new RequestTimeoutSettings
        {
            DefaultTimeoutSeconds = 1,
            Endpoints =
            [
                new EndpointTimeoutSettings
                {
                    PathPattern = "/api/TEST/*",
                    TimeoutSeconds = 5
                }
            ]
        };
        var middleware = CreateMiddleware(async ctx =>
        {
            await Task.Delay(2000, ctx.RequestAborted);
        }, settings);
        var context = CreateHttpContext("/api/test/endpoint");

        var exception = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(exception);
    }

    #endregion
}
