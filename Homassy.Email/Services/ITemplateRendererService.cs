namespace Homassy.Email.Services;

public interface ITemplateRendererService
{
    string Render(Dictionary<string, string> tokens);
    string RenderWeeklySummary(Dictionary<string, string> tokens);
}
