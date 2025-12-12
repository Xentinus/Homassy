using System.Text.Json.Serialization;

namespace Homassy.API.Models.OpenFoodFacts;

/// <summary>
/// Response from Open Food Facts API.
/// </summary>
public record OpenFoodFactsResponse(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("status_verbose")] string? StatusVerbose,
    [property: JsonPropertyName("product")] OpenFoodFactsProduct? Product
);

/// <summary>
/// Product data from Open Food Facts.
/// </summary>
public record OpenFoodFactsProduct(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("product_name")] string? ProductName,
    [property: JsonPropertyName("product_name_hu")] string? ProductNameHu,
    [property: JsonPropertyName("brands")] string? Brands,
    [property: JsonPropertyName("categories")] string? Categories,
    [property: JsonPropertyName("categories_tags")] List<string>? CategoriesTags,
    [property: JsonPropertyName("quantity")] string? Quantity,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("image_small_url")] string? ImageSmallUrl,
    [property: JsonPropertyName("image_front_url")] string? ImageFrontUrl,
    [property: JsonPropertyName("nutrition_grades")] string? NutritionGrades,
    [property: JsonPropertyName("nutriscore_grade")] string? NutriscoreGrade,
    [property: JsonPropertyName("ecoscore_grade")] string? EcoscoreGrade,
    [property: JsonPropertyName("nova_group")] int? NovaGroup,
    [property: JsonPropertyName("ingredients_text")] string? IngredientsText,
    [property: JsonPropertyName("ingredients_text_hu")] string? IngredientsTextHu,
    [property: JsonPropertyName("allergens")] string? Allergens,
    [property: JsonPropertyName("allergens_tags")] List<string>? AllergensTags,
    [property: JsonPropertyName("countries")] string? Countries,
    [property: JsonPropertyName("countries_tags")] List<string>? CountriesTags,
    [property: JsonPropertyName("stores")] string? Stores,
    [property: JsonPropertyName("nutriments")] OpenFoodFactsNutriments? Nutriments
)
{
    /// <summary>
    /// Base64 encoded image data (populated after fetching the image).
    /// Format: "data:{contentType};base64,{base64Data}"
    /// </summary>
    [JsonPropertyName("image_base64")]
    public string? ImageBase64 { get; init; }
};

/// <summary>
/// Nutritional information from Open Food Facts.
/// </summary>
public record OpenFoodFactsNutriments(
    [property: JsonPropertyName("energy-kcal_100g")] double? EnergyKcal100g,
    [property: JsonPropertyName("energy-kj_100g")] double? EnergyKj100g,
    [property: JsonPropertyName("proteins_100g")] double? Proteins100g,
    [property: JsonPropertyName("carbohydrates_100g")] double? Carbohydrates100g,
    [property: JsonPropertyName("sugars_100g")] double? Sugars100g,
    [property: JsonPropertyName("fat_100g")] double? Fat100g,
    [property: JsonPropertyName("saturated-fat_100g")] double? SaturatedFat100g,
    [property: JsonPropertyName("fiber_100g")] double? Fiber100g,
    [property: JsonPropertyName("salt_100g")] double? Salt100g,
    [property: JsonPropertyName("sodium_100g")] double? Sodium100g
);
