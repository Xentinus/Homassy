using Homassy.API.Middleware;
using Microsoft.AspNetCore.Http;

namespace Homassy.Tests.Unit;

public class CorrelationIdMiddlewareTests
{
    private static CorrelationIdMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new CorrelationIdMiddleware(next);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    #region Header Handling Tests

    [Fact]
    public async Task InvokeAsync_WhenNoCorrelationIdHeader_GeneratesNewGuid()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString();
        Assert.NotNull(correlationId);
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdHeaderExists_UsesExistingValue()
    {
        // Arrange
        var expectedCorrelationId = "test-correlation-123";
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = expectedCorrelationId;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString();
        Assert.Equal(expectedCorrelationId, correlationId);
    }

    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdHeaderIsEmpty_GeneratesNewGuid()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = "";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString();
        Assert.NotNull(correlationId);
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdHeaderIsWhitespace_GeneratesNewGuid()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = "   ";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString();
        Assert.NotNull(correlationId);
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    #endregion

    #region HttpContext.Items Tests

    [Fact]
    public async Task InvokeAsync_StoresCorrelationIdInHttpContextItems()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Items.ContainsKey(CorrelationIdMiddleware.CorrelationIdItemKey));
        Assert.NotNull(context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]);
    }

    [Fact]
    public async Task InvokeAsync_CorrelationIdAvailableInNextMiddleware()
    {
        // Arrange
        string? capturedCorrelationId = null;
        var middleware = CreateMiddleware(ctx =>
        {
            capturedCorrelationId = ctx.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString();
            return Task.CompletedTask;
        });
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.NotNull(capturedCorrelationId);
        Assert.True(Guid.TryParse(capturedCorrelationId, out _));
    }

    #endregion

    #region Response Header Tests

    [Fact]
    public async Task InvokeAsync_AddsCorrelationIdToResponseHeaders()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey(CorrelationIdMiddleware.CorrelationIdHeaderName));
    }

    [Fact]
    public async Task InvokeAsync_ResponseHeaderMatchesItemsValue()
    {
        // Arrange
        var expectedCorrelationId = "matching-correlation-id";
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = expectedCorrelationId;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var responseHeader = context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString();
        Assert.Equal(expectedCorrelationId, responseHeader);
    }

    #endregion

    #region Middleware Pipeline Tests

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        // Arrange
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_PreservesCorrelationIdThroughPipeline()
    {
        // Arrange
        var incomingCorrelationId = "preserved-correlation-id";
        string? middlewareCorrelationId = null;
        
        var middleware = CreateMiddleware(ctx =>
        {
            middlewareCorrelationId = ctx.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString();
            return Task.CompletedTask;
        });
        var context = CreateHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = incomingCorrelationId;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(incomingCorrelationId, middlewareCorrelationId);
        Assert.Equal(incomingCorrelationId, context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString());
    }

    #endregion

    #region Constants Tests

    [Fact]
    public void CorrelationIdHeaderName_HasExpectedValue()
    {
        Assert.Equal("X-Correlation-ID", CorrelationIdMiddleware.CorrelationIdHeaderName);
    }

    [Fact]
    public void CorrelationIdItemKey_HasExpectedValue()
    {
        Assert.Equal("CorrelationId", CorrelationIdMiddleware.CorrelationIdItemKey);
    }

    #endregion
}
