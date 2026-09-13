using System.Diagnostics;
using System.Net;
using Homassy.API.Middleware;
using Homassy.API.Models.ApplicationSettings;
using Homassy.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Homassy.Tests.Integration;

/// <summary>
/// Shutdown used to block for the full GracefulShutdown:TimeoutSeconds on every stop, whether or
/// not anything was in flight, and Docker's 10-second default grace period then killed the
/// container mid-sleep. The existing GracefulShutdownTests could not see any of that: they assert
/// on the settings object and on requests to a host that never stops.
///
/// These run a real Kestrel host on a loopback port and actually stop it, which is the only way
/// the two halves of the fix are observable — an idle host stopping at once, and a request that
/// was already in flight getting a normal response rather than a severed connection (#85).
/// </summary>
public class GracefulShutdownDrainTests
{
    private const int TimeoutSeconds = 30;

    private sealed record DrainHost(IHost Host, HttpClient Client) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            Host.Dispose();
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// The shutdown-relevant slice of Program.cs — the host timeout, the counting middleware and
    /// the reporting service — with one endpoint that stays in the pipeline until released.
    /// </summary>
    private static async Task<DrainHost> StartHostAsync(Task? releaseSlowRequest = null, TaskCompletionSource? slowRequestEntered = null)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
                    .ConfigureServices(services =>
                    {
                        services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(TimeoutSeconds));
                        services.Configure<GracefulShutdownSettings>(options =>
                        {
                            options.Enabled = true;
                            options.TimeoutSeconds = TimeoutSeconds;
                        });
                        services.AddSingleton<InFlightRequestTracker>();
                        services.AddHostedService<GracefulShutdownService>();
                        services.AddRouting();
                    })
                    .Configure(app =>
                    {
                        app.UseMiddleware<InFlightRequestMiddleware>();
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/fast", () => Results.Text("fast"));
                            endpoints.MapGet("/slow", async () =>
                            {
                                slowRequestEntered?.TrySetResult();

                                if (releaseSlowRequest != null)
                                {
                                    await releaseSlowRequest;
                                }

                                return Results.Text("drained");
                            });
                        });
                    });
            })
            .Build();

        await host.StartAsync();

        var address = host.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .First();

        return new DrainHost(host, new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(60) });
    }

    [Fact]
    public async Task AnIdleHost_StopsWellWithinTheConfiguredTimeout()
    {
        await using var drainHost = await StartHostAsync();

        // Prove the pipeline works, and that a completed request leaves the counter at zero.
        Assert.Equal(HttpStatusCode.OK, (await drainHost.Client.GetAsync("/fast")).StatusCode);
        Assert.Equal(0, drainHost.Host.Services.GetRequiredService<InFlightRequestTracker>().Count);

        var stopwatch = Stopwatch.StartNew();
        await drainHost.Host.StopAsync();
        stopwatch.Stop();

        // The old implementation slept for TimeoutSeconds unconditionally. Anything in that
        // neighbourhood means the stop is waiting out a clock instead of the requests. The budget
        // is a third of the timeout rather than something tight: this assembly also runs several
        // 1000-iteration Parallel.For tests that saturate every core, and xUnit runs classes in
        // parallel, so a tight budget would fail for scheduling reasons on a small runner.
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"An idle host took {stopwatch.Elapsed.TotalSeconds:0.0}s to stop, with a {TimeoutSeconds}s drain timeout configured.");
    }

    [Fact]
    public async Task ARequestInFlightWhenTheStopSignalArrives_GetsANormalResponse()
    {
        // RunContinuationsAsynchronously on both: without it SetResult runs the waiter's
        // continuation inline on whichever thread completed it — the endpoint's on the test
        // thread, the test's on a Kestrel thread — which is the standard way this shape turns
        // into an intermittent hang.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var drainHost = await StartHostAsync(release.Task, entered);
        var tracker = drainHost.Host.Services.GetRequiredService<InFlightRequestTracker>();

        var inFlightRequest = drainHost.Client.GetAsync("/slow");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(1, tracker.Count);

        // Stop while the request is still inside the pipeline, then let it finish.
        var stopping = drainHost.Host.StopAsync();
        release.SetResult();

        var response = await inFlightRequest;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("drained", await response.Content.ReadAsStringAsync());

        await stopping.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
        Assert.Equal(0, tracker.Count);
    }
}
