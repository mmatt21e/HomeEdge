namespace HomeStock.Infrastructure.Ai;

/// <summary>
/// Configuration for the AI project planner (bound from the "Planner" section). Targets any
/// OpenAI-compatible text chat endpoint. To reuse the same provider/key as photo import, leave
/// BaseUrl/ApiKey blank and they fall back to the "Vision" settings — just set a text Model.
/// </summary>
public class PlannerOptions
{
    public const string SectionName = "Planner";

    public string? BaseUrl { get; set; }
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
    public bool RequireApiKey { get; set; } = true;
    public string? ProviderName { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
    public Dictionary<string, string> ExtraHeaders { get; set; } = new();
}
