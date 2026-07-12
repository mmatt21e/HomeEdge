namespace HomeStock.Infrastructure.Vision;

/// <summary>
/// Configuration for photo→inventory extraction (bound from the "Vision" section). Targets any
/// OpenAI-compatible chat/completions endpoint, so it works with free options such as OpenRouter,
/// Groq, Google Gemini (OpenAI-compatible endpoint), or a local Ollama server.
/// </summary>
public class VisionOptions
{
    public const string SectionName = "Vision";

    /// <summary>Base URL of the OpenAI-compatible API, e.g. "https://openrouter.ai/api/v1".</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Model id, e.g. "meta-llama/llama-3.2-11b-vision-instruct:free" or "gemini-2.0-flash".</summary>
    public string? Model { get; set; }

    /// <summary>API key (Bearer). Not required for a local provider like Ollama.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Set false for providers that need no key (e.g. local Ollama).</summary>
    public bool RequireApiKey { get; set; } = true;

    /// <summary>Friendly provider name for the UI (defaults to the base URL host).</summary>
    public string? ProviderName { get; set; }

    /// <summary>Max accepted image size in bytes (default 6 MB).</summary>
    public long MaxImageBytes { get; set; } = 6 * 1024 * 1024;

    /// <summary>Request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Cap on how many items to accept from one photo.</summary>
    public int MaxItems { get; set; } = 40;

    /// <summary>Extra request headers (e.g. OpenRouter's optional HTTP-Referer / X-Title).</summary>
    public Dictionary<string, string> ExtraHeaders { get; set; } = new();

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(Model) &&
        (!RequireApiKey || !string.IsNullOrWhiteSpace(ApiKey));
}
