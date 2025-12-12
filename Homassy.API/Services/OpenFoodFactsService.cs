using System.Net.Http.Headers;
using Homassy.API.Models.OpenFoodFacts;

namespace Homassy.API.Services;

/// <summary>
/// Service for interacting with Open Food Facts API.
/// </summary>
public class OpenFoodFactsService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://world.openfoodfacts.org/api/v2";

    public OpenFoodFactsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Homassy", "1.0"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("(https://github.com/Xentinus/Homassy)"));
    }

    /// <summary>
    /// Gets product information by barcode from Open Food Facts.
    /// The world database includes products from all countries, including Hungary.
    /// </summary>
    /// <param name="barcode">Product barcode (EAN-13, UPC-A, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Product information or null if not found</returns>
    public async Task<OpenFoodFactsResponse?> GetProductByBarcodeAsync(
        string barcode, 
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(barcode);

        var response = await _httpClient.GetAsync(
            $"{BaseUrl}/product/{barcode}",
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

    /// <summary>
    /// Fetches an image from URL and converts it to Base64 data URI.
    /// </summary>
    private async Task<string?> FetchImageAsBase64Async(string imageUrl, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                imageUrl,
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
}
