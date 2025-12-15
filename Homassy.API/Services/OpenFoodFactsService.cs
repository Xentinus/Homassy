using System.Net.Http.Headers;
using Homassy.API.Middleware;
using Homassy.API.Models.OpenFoodFacts;

namespace Homassy.API.Services;

public class OpenFoodFactsService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string BaseUrl = "https://world.openfoodfacts.org/api/v2";

    public OpenFoodFactsService(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<OpenFoodFactsResponse?> GetProductByBarcodeAsync(
        string barcode, 
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(barcode);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/product/{barcode}");
        AddCorrelationIdHeader(request);

        var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await System.Text.Json.JsonSerializer.DeserializeAsync<OpenFoodFactsResponse>(
            stream,
            cancellationToken: cancellationToken);

        if (result?.Product != null && !string.IsNullOrEmpty(result.Product.ImageUrl))
        {
            var imageBase64 = await FetchImageAsBase64Async(result.Product.ImageUrl, cancellationToken);
            if (imageBase64 != null)
            {
                return result with
                {
                    Product = result.Product with { ImageBase64 = imageBase64 }
                };
            }
        }

        return result;
    }

    private async Task<string?> FetchImageAsBase64Async(string imageUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            AddCorrelationIdHeader(request);

            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var base64 = Convert.ToBase64String(imageBytes);

            return $"data:{contentType};base64,{base64}";
        }
        catch
        {
            return null;
        }
    }

    private void AddCorrelationIdHeader(HttpRequestMessage request)
    {
        var correlationId = _httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString();
        if (!string.IsNullOrEmpty(correlationId))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.CorrelationIdHeaderName, correlationId);
        }
    }
}
