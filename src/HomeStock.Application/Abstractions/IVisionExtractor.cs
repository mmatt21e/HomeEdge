using HomeStock.Application.Common;
using HomeStock.Application.Models;

namespace HomeStock.Application.Abstractions;

/// <summary>
/// Extracts candidate inventory items from a photo using an external (or local) vision model.
/// Kept as an abstraction so the provider (Claude, OpenAI, Gemini, Groq, OpenRouter, Ollama, …)
/// is a configuration concern, not a code change. The default implementation targets any
/// OpenAI-compatible chat/completions endpoint.
/// </summary>
public interface IVisionExtractor
{
    /// <summary>True when a provider (base URL + model, and a key if the provider needs one) is configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Human-readable provider/model label for the UI (e.g. "openrouter · llama-3.2-vision").</summary>
    string ProviderLabel { get; }

    /// <summary>Sends the image to the model and returns the detected items, or a friendly error.</summary>
    Task<Result<VisionExtractionResult>> ExtractAsync(
        Stream image, string contentType, VisionMode mode, CancellationToken ct = default);
}
