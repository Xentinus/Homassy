using System.Reflection;

namespace Homassy.Email.Services;

public sealed class TemplateRendererService : ITemplateRendererService
{
    private readonly string _codeTemplate;
    private readonly string _weeklySummaryTemplate;

    public TemplateRendererService()
    {
        var assembly = Assembly.GetExecutingAssembly();
        _codeTemplate = LoadResource(assembly, "Homassy.Email.Templates.CodeEmail.html");
        _weeklySummaryTemplate = LoadResource(assembly, "Homassy.Email.Templates.WeeklySummaryEmail.html");
    }

    private static string LoadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public string Render(Dictionary<string, string> tokens)
        => ApplyTokens(_codeTemplate, tokens);

    public string RenderWeeklySummary(Dictionary<string, string> tokens)
        => ApplyTokens(_weeklySummaryTemplate, tokens);

    private static string ApplyTokens(string template, Dictionary<string, string> tokens)
    {
        var result = template;
        foreach (var (key, value) in tokens)
        {
            result = result.Replace($"{{{{{key}}}}}", value);
        }
        return result;
    }
}
