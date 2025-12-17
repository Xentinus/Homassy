using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Homassy.API.Models.HealthCheck;

namespace Homassy.API.HealthChecks;

public class OpenFoodFactsHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly HealthCheckOptions _options;
    private const string BaseUrl = "https://world.openfoodfacts.org/api/v2";

    public OpenFoodFactsHealthCheck(IHttpClientFactory httpClientFactory, IOptions<HealthCheckOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        
        _httpClient = httpClientFactory.CreateClient("OpenFoodFactsHealthCheck");
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var response = await _httpClient.GetAsync(
                $"{BaseUrl}/product/{_options.OpenFoodFactsTestBarcode}",
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy(
                    $"OpenFoodFacts API is reachable",
                    new Dictionary<string, object>
                    {
                        ["endpoint"] = BaseUrl,
                        ["statusCode"] = (int)response.StatusCode
                    });
            }

            return HealthCheckResult.Degraded(
                $"OpenFoodFacts API returned {(int)response.StatusCode}",
                data: new Dictionary<string, object>
                {
                    ["endpoint"] = BaseUrl,
                    ["statusCode"] = (int)response.StatusCode
                });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                $"OpenFoodFacts API request timed out after {_options.TimeoutSeconds} seconds",
                data: new Dictionary<string, object>
                {
                    ["endpoint"] = BaseUrl,
                    ["timeoutSeconds"] = _options.TimeoutSeconds
                });
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy(
                $"OpenFoodFacts API is unreachable: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    ["endpoint"] = BaseUrl
                });
        }
    }
}
