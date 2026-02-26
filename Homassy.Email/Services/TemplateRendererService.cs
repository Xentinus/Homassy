using System.Reflection;

namespace Homassy.Email.Services;

public sealed class TemplateRendererService : ITemplateRendererService
{
    private readonly string _template;

    public TemplateRendererService()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Homassy.Email.Templates.CodeEmail.html";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        _template = reader.ReadToEnd();
    }

    public string Render(Dictionary<string, string> tokens)
    {
        var result = _template;
        foreach (var (key, value) in tokens)
        {
            result = result.Replace($"{{{{{key}}}}}", value);
        }
        return result;
    }
}
