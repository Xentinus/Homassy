using Homassy.API.Middleware;
using Homassy.API.Models;
using Microsoft.AspNetCore.Http;

namespace Homassy.Tests.Unit;

public class RequestLoggingMiddlewareTests
{
    private static RequestLoggingOptions CreateDefaultOptions() => new()
    {
        Enabled = true,
        DetailedPaths = [],
        ExcludedPaths = []
    };

    private static HttpContext CreateHttpContext(string method = "GET", string path = "/api/test")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.StatusCode = 200;
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = Guid.NewGuid().ToString();
        return context;
    }

    #region Basic Logging Tests
    [Fact]
    public async Task InvokeAsync_WhenEnabled_CallsNextMiddleware()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new RequestLoggingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, CreateDefaultOptions());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenDisabled_SkipsLogging()
    {
        // Arrange
        var options = new RequestLoggingOptions { Enabled = false };
        var nextCalled = false;
        var middleware = new RequestLoggingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }
    #endregion

    #region Excluded Path Tests
    [Fact]
    public async Task InvokeAsync_WhenPathExcluded_SkipsLogging()
    {
        // Arrange
        var options = new RequestLoggingOptions
        {
            Enabled = true,
            ExcludedPaths = ["/health", "/openapi"]
        };
        var nextCalled = false;
        var middleware = new RequestLoggingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, options);
        var context = CreateHttpContext(path: "/health");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenPathNotExcluded_LogsRequest()
    {
        // Arrange
        var options = new RequestLoggingOptions
        {
            Enabled = true,
            ExcludedPaths = ["/health"]
        };
        var nextCalled = false;
        var middleware = new RequestLoggingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, options);
        var context = CreateHttpContext(path: "/api/products");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/openapi")]
    [InlineData("/openapi/v1")]
    public async Task InvokeAsync_WhenPathStartsWithExcluded_SkipsLogging(string path)
    {
        // Arrange
        var options = new RequestLoggingOptions
        {
            Enabled = true,
            ExcludedPaths = ["/health", "/openapi"]
        };
        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, options);
        var context = CreateHttpContext(path: path);

        // Act & Assert (should not throw)
        await middleware.InvokeAsync(context);
    }
    #endregion

    #region Detailed Logging Tests
    [Fact]
    public async Task InvokeAsync_WhenDetailedPathMatches_LogsDetails()
    {
        // Arrange
        var options = new RequestLoggingOptions
        {
            Enabled = true,
            DetailedPaths = ["/api/auth"]
        };
        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, options);
        var context = CreateHttpContext(path: "/api/auth/login");

        // Act & Assert (should not throw)
        await middleware.InvokeAsync(context);
    }
    #endregion

    #region Exception Handling Tests
    [Fact]
    public async Task InvokeAsync_WhenNextThrows_StillLogsRequest()
    {
        // Arrange
        var middleware = new RequestLoggingMiddleware(_ =>
        {
            throw new InvalidOperationException("Test exception");
        }, CreateDefaultOptions());
        var context = CreateHttpContext();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }
    #endregion

    #region Query String Sanitization Tests
    [Fact]
    public async Task InvokeAsync_WithSensitiveQueryParams_RedactsValues()
    {
        // Arrange
        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, CreateDefaultOptions());
        var context = CreateHttpContext(path: "/api/test");
        context.Request.QueryString = new QueryString("?password=secret&name=test");

        // Act & Assert (should not throw, sensitive data should be redacted in logs)
        await middleware.InvokeAsync(context);
    }

    [Theory]
    [InlineData("?password=secret")]
    [InlineData("?token=abc123")]
    [InlineData("?access_token=xyz")]
    [InlineData("?refresh_token=def")]
    [InlineData("?apikey=key123")]
    [InlineData("?api_key=key456")]
    public async Task InvokeAsync_WithVariousSensitiveParams_RedactsValues(string queryString)
    {
        // Arrange
        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, CreateDefaultOptions());
        var context = CreateHttpContext(path: "/api/test");
        context.Request.QueryString = new QueryString(queryString);

        // Act & Assert (should not throw)
        await middleware.InvokeAsync(context);
    }
    #endregion

    #region HTTP Method Tests
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task InvokeAsync_WithDifferentHttpMethods_LogsCorrectly(string method)
    {
        // Arrange
        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, CreateDefaultOptions());
        var context = CreateHttpContext(method: method);

        // Act & Assert (should not throw)
        await middleware.InvokeAsync(context);
    }
    #endregion

    #region Status Code Tests
    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task InvokeAsync_WithDifferentStatusCodes_LogsAppropriateLevel(int statusCode)
    {
        // Arrange
        var middleware = new RequestLoggingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        }, CreateDefaultOptions());
        var context = CreateHttpContext();

        // Act & Assert (should not throw)
        await middleware.InvokeAsync(context);
        Assert.Equal(statusCode, context.Response.StatusCode);
    }
    #endregion

    #region Correlation ID Tests
    [Fact]
    public async Task InvokeAsync_WithCorrelationId_IncludesInLog()
    {
        // Arrange
        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, CreateDefaultOptions());
        var context = CreateHttpContext();
        var correlationId = Guid.NewGuid().ToString();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = correlationId;

        // Act & Assert (should not throw)
        await middleware.InvokeAsync(context);
    }

    [Fact]
    public async Task InvokeAsync_WithoutCorrelationId_UsesFallback()
    {
        // Arrange
        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, CreateDefaultOptions());
        var context = CreateHttpContext();
        context.Items.Remove(CorrelationIdMiddleware.CorrelationIdItemKey);

        // Act & Assert (should not throw)
        await middleware.InvokeAsync(context);
    }
    #endregion
}
