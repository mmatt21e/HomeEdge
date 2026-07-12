using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeStock.Infrastructure.Vision;

/// <summary>
/// Extracts inventory items from a photo via any OpenAI-compatible chat/completions endpoint
/// that supports image ("vision") input. The image is sent as a base64 data URL in the request,
/// server-side, so the API key never reaches the browser.
/// </summary>
public class OpenAiCompatibleVisionExtractor(
    IHttpClientFactory httpClientFactory,
    IOptions<VisionOptions> options,
    ILogger<OpenAiCompatibleVisionExtractor> logger) : IVisionExtractor
{
    public const string HttpClientName = "vision";
    private readonly VisionOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConfigured => _options.IsConfigured;

    public string ProviderLabel
    {
        get
        {
            var provider = _options.ProviderName
                ?? (Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var u) ? u.Host : "vision");
            return $"{provider} · {_options.Model}";
        }
    }

    public async Task<Result<VisionExtractionResult>> ExtractAsync(
        Stream image, string contentType, VisionMode mode, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return Result<VisionExtractionResult>.Failure(
                "Photo import is not configured. Set a Vision provider (base URL, model, and API key) in configuration.");

        // Read + size-check the image.
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms, ct);
        if (ms.Length == 0) return Result<VisionExtractionResult>.Failure("The image is empty.");
        if (ms.Length > _options.MaxImageBytes)
            return Result<VisionExtractionResult>.Failure(
                $"Image is larger than the {_options.MaxImageBytes / (1024 * 1024)} MB limit.");

        var dataUrl = $"data:{NormalizeContentType(contentType)};base64,{Convert.ToBase64String(ms.ToArray())}";

        var body = new
        {
            model = _options.Model,
            temperature = 0.1,
            max_tokens = 1500,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = UserPrompt(mode) },
                        new { type = "image_url", image_url = new { url = dataUrl } }
                    }
                }
            }
        };

        var client = httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 300));

        using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(_options.BaseUrl!, "chat/completions"))
        {
            Content = JsonContent.Create(body)
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        foreach (var (k, v) in _options.ExtraHeaders)
            request.Headers.TryAddWithoutValidation(k, v);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result<VisionExtractionResult>.Failure("The vision model timed out. Try a smaller photo or a faster model.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Vision provider request failed");
            return Result<VisionExtractionResult>.Failure("Could not reach the vision provider. Check the base URL and network.");
        }

        if (!response.IsSuccessStatusCode)
        {
            // Log the detail server-side but keep the key/detail out of the user-facing message.
            var detail = await SafeReadAsync(response, ct);
            logger.LogWarning("Vision provider returned {Status}: {Detail}", (int)response.StatusCode, detail);
            return Result<VisionExtractionResult>.Failure(
                $"The vision provider returned an error ({(int)response.StatusCode}). Check the model id and your API key/quota.");
        }

        var raw = await response.Content.ReadAsStringAsync(ct);
        var content = ExtractMessageContent(raw);
        if (string.IsNullOrWhiteSpace(content))
            return Result<VisionExtractionResult>.Failure("The vision model returned an empty response.");

        var items = ParseItems(content);
        if (items is null)
            return Result<VisionExtractionResult>.Failure("The vision model's response could not be understood. Try again or use a different model.");

        var mapped = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Take(_options.MaxItems)
            .Select(i => new DetectedItem
            {
                Name = i.Name!.Trim(),
                Category = Clean(i.Category),
                Quantity = i.Quantity is > 0 ? i.Quantity.Value : 1,
                Description = Clean(i.Description),
                Manufacturer = Clean(i.Manufacturer),
                Brand = Clean(i.Brand),
                ModelNumber = Clean(i.ModelNumber),
                Confidence = i.Confidence
            })
            .ToList();

        logger.LogInformation("Vision extraction ({Mode}) via {Provider} detected {Count} item(s)", mode, ProviderLabel, mapped.Count);
        return Result<VisionExtractionResult>.Success(new VisionExtractionResult(mapped, _options.Model, null));
    }

    // ---- prompts ----

    private const string SystemPrompt =
        "You are an assistant that identifies household inventory items from a photo. " +
        "Respond with ONLY a JSON object, no prose and no markdown fences, of the form: " +
        "{\"items\":[{\"name\":string,\"category\":string,\"quantity\":number,\"description\":string," +
        "\"manufacturer\":string,\"brand\":string,\"model_number\":string,\"confidence\":number}]}. " +
        "Use concise names. Omit fields you cannot determine (do not invent serial numbers or prices). " +
        "confidence is 0..1. Suggested categories: Tools, Electronics, Appliances, Furniture, Automotive, " +
        "Outdoor Equipment, Building Materials, Cleaning Supplies, Food Storage, Documents, Replacement Parts, " +
        "Seasonal Items, Safety Equipment, Personal Property.";

    private static string UserPrompt(VisionMode mode) => mode switch
    {
        VisionMode.SingleProduct =>
            "This photo shows a single product. Return exactly one item with as much detail as you can read " +
            "from the packaging or label (name, brand, manufacturer, model number).",
        _ =>
            "This photo shows a scene that may contain multiple distinct household items. Return one entry per " +
            "distinct item you can identify, with a best-guess quantity."
    };

    // ---- parsing helpers ----

    private static string NormalizeContentType(string contentType) =>
        string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? "image/jpeg" : contentType;

    private static string CombineUrl(string baseUrl, string path) =>
        $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Pulls choices[0].message.content out of an OpenAI-compatible response envelope.</summary>
    private static string? ExtractMessageContent(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var msg = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content");
            // content is usually a string; some providers return an array of parts.
            if (msg.ValueKind == JsonValueKind.String) return msg.GetString();
            if (msg.ValueKind == JsonValueKind.Array)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var part in msg.EnumerateArray())
                    if (part.TryGetProperty("text", out var t)) sb.Append(t.GetString());
                return sb.ToString();
            }
            return msg.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Parses the model's item list, tolerating markdown fences and either a wrapper object or bare array.</summary>
    private static List<RawItem>? ParseItems(string content)
    {
        var json = StripToJson(content);
        if (json is null) return null;
        try
        {
            if (json.TrimStart().StartsWith('['))
                return JsonSerializer.Deserialize<List<RawItem>>(json, JsonOpts);
            var wrapper = JsonSerializer.Deserialize<RawWrapper>(json, JsonOpts);
            return wrapper?.Items;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Strips ```json fences and trims to the outermost JSON object/array.</summary>
    private static string? StripToJson(string content)
    {
        var s = content.Trim();
        if (s.StartsWith("```"))
        {
            var firstNl = s.IndexOf('\n');
            if (firstNl >= 0) s = s[(firstNl + 1)..];
            var fence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) s = s[..fence];
            s = s.Trim();
        }
        int objStart = s.IndexOf('{'), arrStart = s.IndexOf('[');
        int start = (objStart, arrStart) switch
        {
            (< 0, < 0) => -1,
            (< 0, _) => arrStart,
            (_, < 0) => objStart,
            _ => Math.Min(objStart, arrStart)
        };
        if (start < 0) return null;
        var open = s[start];
        var close = open == '{' ? '}' : ']';
        var end = s.LastIndexOf(close);
        return end > start ? s[start..(end + 1)] : null;
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try { var t = await response.Content.ReadAsStringAsync(ct); return t.Length > 500 ? t[..500] : t; }
        catch { return "(unreadable)"; }
    }

    private sealed class RawWrapper { public List<RawItem>? Items { get; set; } }

    private sealed class RawItem
    {
        public string? Name { get; set; }
        public string? Category { get; set; }
        public int? Quantity { get; set; }
        public string? Description { get; set; }
        public string? Manufacturer { get; set; }
        public string? Brand { get; set; }
        [JsonPropertyName("model_number")] public string? ModelNumber { get; set; }
        public double? Confidence { get; set; }
    }
}
