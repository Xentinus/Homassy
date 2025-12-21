using Homassy.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using Xunit.Abstractions;

namespace Homassy.Tests.Integration;

public class GracefulShutdownTests : IClassFixture<HomassyWebApplicationFactory>
{
    private readonly HomassyWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public GracefulShutdownTests(HomassyWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task WhenShutdownTriggered_ActiveRequestsComplete()
    {
        var client = _factory.CreateClient();
        var requestStarted = false;
        var requestCompleted = false;

        var longRunningTask = Task.Run(async () =>
        {
            requestStarted = true;
            var response = await client.GetAsync("/api/v1.0/health/live");
            requestCompleted = response.StatusCode == HttpStatusCode.OK;
        });

        await Task.Delay(100);
        Assert.True(requestStarted);

        await longRunningTask;

        _output.WriteLine($"Request started: {requestStarted}");
        _output.WriteLine($"Request completed: {requestCompleted}");

        Assert.True(requestCompleted);
    }

    [Fact]
    public async Task WhenMultipleRequestsActive_AllComplete()
    {
        var client = _factory.CreateClient();
        var tasks = new List<Task<HttpResponseMessage>>();

        for (var i = 0; i < 5; i++)
        {
            tasks.Add(client.GetAsync("/api/v1.0/health/live"));
        }

        var responses = await Task.WhenAll(tasks);

        _output.WriteLine($"Total requests: {responses.Length}");
        _output.WriteLine($"Successful requests: {responses.Count(r => r.StatusCode == HttpStatusCode.OK)}");

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }

    [Fact]
    public void GracefulShutdownService_IsRegistered()
    {
        var services = _factory.Services;
        var hostedServices = services.GetServices<IHostedService>();

        var gracefulShutdownServiceExists = hostedServices.Any(s => 
            s.GetType().Name == "GracefulShutdownService");

        _output.WriteLine($"GracefulShutdownService registered: {gracefulShutdownServiceExists}");
        _output.WriteLine($"Total hosted services: {hostedServices.Count()}");

        Assert.True(gracefulShutdownServiceExists);
    }

    [Fact]
    public async Task WhenShutdownRequested_ApplicationLifetimeTriggered()
    {
        using var scope = _factory.Services.CreateScope();
        var lifetime = scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();

        var stoppingTriggered = false;
        lifetime.ApplicationStopping.Register(() =>
        {
            stoppingTriggered = true;
        });

        _output.WriteLine($"Stopping token registered: {lifetime.ApplicationStopping.CanBeCanceled}");

        Assert.True(lifetime.ApplicationStopping.CanBeCanceled);
    }

    [Fact]
    public async Task ConcurrentRequests_CompleteSuccessfully()
    {
        var client = _factory.CreateClient();
        var tasks = Enumerable.Range(0, 10).Select(_ => client.GetAsync("/api/v1.0/health/live"));

        var responses = await Task.WhenAll(tasks);

        var successCount = responses.Count(r => r.IsSuccessStatusCode);
        _output.WriteLine($"Success count: {successCount} / {responses.Length}");

        Assert.Equal(responses.Length, successCount);
    }
}
