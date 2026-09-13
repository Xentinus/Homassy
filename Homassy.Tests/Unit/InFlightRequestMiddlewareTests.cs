using Homassy.API.Middleware;
using Homassy.API.Services;
using Microsoft.AspNetCore.Http;

namespace Homassy.Tests.Unit;

/// <summary>
/// What the shutdown drain counts, and what it must not (#85).
/// </summary>
public class InFlightRequestMiddlewareTests
{
    /// <summary>Records what the counter said while the request was inside the pipeline.</summary>
    private static async Task<int> CountDuringRequestAsync(InFlightRequestTracker tracker, string path)
    {
        var observed = 0;
        var middleware = new InFlightRequestMiddleware(_ =>
        {
            observed = tracker.Count;
            return Task.CompletedTask;
        }, tracker);

        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        return observed;
    }

    [Theory]
    [InlineData("/api/v1.0/product")]
    [InlineData("/api/v1.0/health/live")]
    [InlineData("/not-a-route-at-all")]
    public async Task AnOrdinaryRequest_IsCountedWhileItIsInThePipeline(string path)
    {
        var tracker = new InFlightRequestTracker();

        Assert.Equal(1, await CountDuringRequestAsync(tracker, path));
        Assert.Equal(0, tracker.Count);
    }

    /// <summary>
    /// A SignalR connection stays in the pipeline until the client goes away, so counting it
    /// would make the number permanent rather than in-flight: four hubs and three connected
    /// devices would mean the "stopping immediately" path never fires on the real box, and the
    /// drain timing would be measuring socket teardown instead of requests.
    /// </summary>
    [Theory]
    [InlineData("/hubs/shopping-list")]
    [InlineData("/hubs/inventory")]
    [InlineData("/hubs/master-data")]
    [InlineData("/hubs/family-chat")]
    [InlineData("/hubs/family-chat/negotiate")]
    [InlineData("/HUBS/Inventory")]
    public async Task AHubConnection_IsNotCounted(string path)
    {
        var tracker = new InFlightRequestTracker();

        Assert.Equal(0, await CountDuringRequestAsync(tracker, path));
        Assert.Equal(0, tracker.Count);
    }

    /// <summary>The prefix is a path segment, not a string prefix: /hubsomething is a normal route.</summary>
    [Fact]
    public async Task APathThatMerelyStartsWithTheSameLetters_IsStillCounted()
    {
        var tracker = new InFlightRequestTracker();

        Assert.Equal(1, await CountDuringRequestAsync(tracker, "/hubspot-webhook"));
    }

    [Fact]
    public async Task ARequestThatThrows_StillLeavesTheCounter()
    {
        var tracker = new InFlightRequestTracker();
        var middleware = new InFlightRequestMiddleware(_ => throw new InvalidOperationException("boom"), tracker);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(new DefaultHttpContext()));

        Assert.Equal(0, tracker.Count);
    }
}
