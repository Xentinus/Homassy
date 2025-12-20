using Homassy.API.Enums;
using Homassy.API.Middleware;
using Homassy.API.Models.Common;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Homassy.Tests.Unit;

/// <summary>
/// Tests for CancellationToken support across the application.
/// Verifies that async operations properly respect cancellation requests.
/// </summary>
public class CancellationTokenTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #region OperationCanceledException Handling Tests

    [Fact]
    public async Task GlobalExceptionMiddleware_WhenOperationCanceled_Returns499()
    {
        // Arrange
        var middleware = new GlobalExceptionMiddleware(_ => throw new OperationCanceledException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(499, context.Response.StatusCode);
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WhenTaskCanceled_Returns499()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var middleware = new GlobalExceptionMiddleware(_ => 
            Task.FromCanceled(cts.Token));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(499, context.Response.StatusCode);
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WhenCanceled_ReturnsApiResponseFormat()
    {
        // Arrange
        var middleware = new GlobalExceptionMiddleware(_ => throw new OperationCanceledException("Request was cancelled"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var response = await GetResponseAsync(context);
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorCodes);
        Assert.Single(response.ErrorCodes);
        Assert.Equal(ErrorCodes.SystemRequestCancelled, response.ErrorCodes[0]);
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WhenCanceled_SetsJsonContentType()
    {
        // Arrange
        var middleware = new GlobalExceptionMiddleware(_ => throw new OperationCanceledException());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("application/json", context.Response.ContentType);
    }

    #endregion

    #region CancellationToken Propagation Tests

    [Fact]
    public void CancellationToken_WhenCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.Throws<OperationCanceledException>(() => cts.Token.ThrowIfCancellationRequested());
    }

    [Fact]
    public async Task CancellationToken_WhenCanceledDuringDelay_ThrowsTaskCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var task = Task.Delay(TimeSpan.FromSeconds(10), cts.Token);

        // Act
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
    }

    [Fact]
    public async Task CancellationToken_WhenNotCanceled_CompletesNormally()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var completed = false;

        // Act
        await Task.Run(() =>
        {
            cts.Token.ThrowIfCancellationRequested();
            completed = true;
        }, cts.Token);

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public async Task CancellationToken_LinkedTokenSource_CancelsWhenAnySourceCancels()
    {
        // Arrange
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);

        // Act
        await cts1.CancelAsync();

        // Assert
        Assert.True(linkedCts.Token.IsCancellationRequested);
        Assert.True(cts1.Token.IsCancellationRequested);
        Assert.False(cts2.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task CancellationToken_WithTimeout_CancelsAfterTimeout()
    {
        // Arrange
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act
        await Task.Delay(100);

        // Assert
        Assert.True(cts.Token.IsCancellationRequested);
    }

    #endregion

    #region Async Method Cancellation Pattern Tests

    [Fact]
    public async Task AsyncMethod_WhenCanceledBeforeStart_ThrowsImmediately()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await SimulateAsyncOperationAsync(cts.Token);
        });
    }

    [Fact]
    public async Task AsyncMethod_WhenCanceledDuringLoop_ExitsLoop()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var iterationsCompleted = 0;

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                cts.Token.ThrowIfCancellationRequested();
                iterationsCompleted++;

                if (i == 5)
                {
                    await cts.CancelAsync();
                }

                await Task.Delay(10, CancellationToken.None);
            }
        });

        // Assert - should have completed iterations 0-5, then cancelled at iteration 6
        Assert.Equal(6, iterationsCompleted);
    }

    [Fact]
    public async Task AsyncMethod_WithCancellationCallback_InvokesCallback()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var callbackInvoked = false;

        cts.Token.Register(() => callbackInvoked = true);

        // Act
        await cts.CancelAsync();

        // Assert
        Assert.True(callbackInvoked);
    }

    #endregion

    #region Helper Methods

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

    private static async Task SimulateAsyncOperationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(100, cancellationToken);
    }

    #endregion
}
