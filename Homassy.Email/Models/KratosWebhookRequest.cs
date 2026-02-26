using System.Text.Json.Serialization;

namespace Homassy.Email.Models;

public sealed record KratosWebhookRequest(
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("template_type")] string TemplateType,
    [property: JsonPropertyName("template_data")] KratosTemplateData TemplateData
);
