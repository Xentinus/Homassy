using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Middleware;
using Homassy.API.Models.Common;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Homassy.Tests.Unit;

public class GlobalExceptionMiddlewareTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private GlobalExceptionMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new GlobalExceptionMiddleware(next);
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = Guid.NewGuid().ToString();
        return context;
    }

    private static async Task<ApiResponse?> GetResponseAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ApiResponse>(responseBody, JsonOptions);
    }

    #region No Exception Tests
    [Fact]
    public async Task InvokeAsync_WhenNoException_CallsNextMiddleware()
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
    public async Task InvokeAsync_WhenNoException_DoesNotModifyResponse()
    {
        // Arrange
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
    }
    #endregion

    #region AuthException Tests
    [Fact]
    public async Task InvokeAsync_WhenUnauthorizedException_Returns401()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new UnauthorizedException("Test unauthorized"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        var response = await GetResponseAsync(context);
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Contains(ErrorCodes.AuthUnauthorized, response.ErrorCodes?.FirstOrDefault());
    }

    [Fact]
    public async Task InvokeAsync_WhenUserNotFoundException_Returns401()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new UserNotFoundException("User not found"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenForbiddenException_Returns403()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ForbiddenException("Access denied"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenBadRequestException_Returns400()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new BadRequestException("Invalid data"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenFamilyNotFoundException_Returns404()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new FamilyNotFoundException("Family not found"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }
    #endregion

    #region Product Exception Tests
    [Fact]
    public async Task InvokeAsync_WhenProductNotFoundException_Returns404()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ProductNotFoundException("Product not found"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenProductInventoryItemNotFoundException_Returns404()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ProductInventoryItemNotFoundException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenProductAccessDeniedException_Returns403()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ProductAccessDeniedException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }
    #endregion

    #region Location Exception Tests
    [Fact]
    public async Task InvokeAsync_WhenLocationNotFoundException_Returns404()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new LocationNotFoundException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenStorageLocationNotFoundException_Returns404()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new StorageLocationNotFoundException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenShoppingLocationNotFoundException_Returns404()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ShoppingLocationNotFoundException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenLocationAccessDeniedException_Returns403()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new LocationAccessDeniedException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }
    #endregion

    #region Shopping List Exception Tests
    [Fact]
    public async Task InvokeAsync_WhenShoppingListNotFoundException_Returns404()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ShoppingListNotFoundException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenShoppingListItemNotFoundException_Returns404()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ShoppingListItemNotFoundException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenShoppingListAccessDeniedException_Returns403()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ShoppingListAccessDeniedException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenInvalidShoppingListItemException_Returns400()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new InvalidShoppingListItemException("Invalid item"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }
    #endregion

    #region Standard Exception Tests
    [Fact]
    public async Task InvokeAsync_WhenArgumentNullException_Returns400()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ArgumentNullException("param"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentException_Returns400()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ArgumentException("Invalid argument"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenInvalidOperationException_Returns400()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Invalid operation"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnauthorizedAccessException_Returns401()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new UnauthorizedAccessException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenOperationCanceledException_Returns499()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new OperationCanceledException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(499, context.Response.StatusCode);
    }
    #endregion

    #region Timeout Exception Tests
    [Fact]
    public async Task InvokeAsync_WhenRequestTimeoutException_Returns504()
    {
        var middleware = CreateMiddleware(_ => throw new RequestTimeoutException("Request timed out"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status504GatewayTimeout, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestTimeoutException_ReturnsErrorCode()
    {
        var middleware = CreateMiddleware(_ => throw new RequestTimeoutException("Request timed out after 30 seconds"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);
        var response = await GetResponseAsync(context);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Contains(ErrorCodes.SystemRequestTimeout, response.ErrorCodes?.FirstOrDefault());
    }
    #endregion

    #region Unknown Exception Tests
    [Fact]
    public async Task InvokeAsync_WhenUnknownException_Returns500()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new Exception("Unknown error"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnknownException_ReturnsSystemErrorCode()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new Exception("Sensitive internal error"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);
        var response = await GetResponseAsync(context);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Contains(ErrorCodes.SystemUnexpectedError, response.ErrorCodes?.FirstOrDefault());
    }
    #endregion

    #region Response Format Tests
    [Fact]
    public async Task InvokeAsync_WhenException_SetsContentTypeToJson()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new Exception("Test"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_WhenException_ReturnsApiResponseFormat()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => throw new ProductNotFoundException("Test product not found"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);
        var response = await GetResponseAsync(context);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorCodes);
        Assert.Single(response.ErrorCodes);
        Assert.Contains(ErrorCodes.ProductNotFound, response.ErrorCodes[0]);
    }
    #endregion
}
