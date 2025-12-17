using Homassy.API.HealthChecks;
using Homassy.API.Models.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Homassy.Tests.Unit;

public class OpenFoodFactsHealthCheckTests
{
    private static OpenFoodFactsHealthCheck CreateHealthCheck(
        HttpClient httpClient,
        HealthCheckOptions? options = null)
    {
        options ??= new HealthCheckOptions
        {
            OpenFoodFactsTestBarcode = "3017620422003",
            TimeoutSeconds = 5
        };

        var httpClientFactory = new TestHttpClientFactory(httpClient);
        return new OpenFoodFactsHealthCheck(httpClientFactory, Options.Create(options));
    }

    private class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public TestHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name) => _httpClient;
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_WhenApiReturnsSuccess_ReturnsHealthy()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);
        var healthCheck = CreateHealthCheck(httpClient);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenApiReturnsSuccess_IncludesEndpointInData()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);
        var healthCheck = CreateHealthCheck(httpClient);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("endpoint"));
        Assert.Contains("openfoodfacts.org", result.Data["endpoint"].ToString());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenApiReturnsNotFound_ReturnsDegraded()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)));
        var httpClient = new HttpClient(handler);
        var healthCheck = CreateHealthCheck(httpClient);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenApiReturnsServerError_ReturnsDegraded()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)));
        var httpClient = new HttpClient(handler);
        var healthCheck = CreateHealthCheck(httpClient);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(500, result.Data["statusCode"]);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRequestTimesOut_ReturnsUnhealthy()
    {
        var handler = new FakeHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(10000, ct);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        var options = new HealthCheckOptions
        {
            OpenFoodFactsTestBarcode = "3017620422003",
            TimeoutSeconds = 1
        };
        var healthCheck = CreateHealthCheck(httpClient, options);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("timed out", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenHttpRequestExceptionThrown_ReturnsUnhealthy()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Connection refused"));
        var httpClient = new HttpClient(handler);
        var healthCheck = CreateHealthCheck(httpClient);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("unreachable", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var handler = new FakeHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(5000, ct);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        var healthCheck = CreateHealthCheck(httpClient);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            healthCheck.CheckHealthAsync(new HealthCheckContext(), cts.Token));
    }

    [Fact]
    public void Constructor_WhenHttpClientFactoryIsNull_ThrowsArgumentNullException()
    {
        var options = Options.Create(new HealthCheckOptions());

        Assert.Throws<ArgumentNullException>(() => new OpenFoodFactsHealthCheck(null!, options));
    }

    [Fact]
    public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        var httpClientFactory = new TestHttpClientFactory(new HttpClient());

        Assert.Throws<ArgumentNullException>(() => new OpenFoodFactsHealthCheck(httpClientFactory, null!));
    }
}
