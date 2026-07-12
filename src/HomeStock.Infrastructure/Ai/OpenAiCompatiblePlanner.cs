using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeStock.Application.Abstractions;
using HomeStock.Application.Common;
using HomeStock.Application.Models;
using HomeStock.Infrastructure.Vision;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeStock.Infrastructure.Ai;

/// <summary>
/// Proposes a project's bill of materials via any OpenAI-compatible text chat endpoint. Only the
/// project description is sent — inventory matching is done locally by the application layer.
/// Credentials fall back to the Vision provider so one key can serve both features.
/// </summary>
public class OpenAiCompatiblePlanner(
    IHttpClientFactory httpClientFactory,
    IOptions<PlannerOptions> plannerOptions,
    IOptions<VisionOptions> visionOptions,
    ILogger<OpenAiCompatiblePlanner> logger) : IProjectPlanner
{
    public const string HttpClientName = "planner";
    private readonly PlannerOptions _planner = plannerOptions.Value;
    private readonly VisionOptions _vision = visionOptions.Value;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private string? BaseUrl => Pick(_planner.BaseUrl, _vision.BaseUrl);
    private string? ApiKey => Pick(_planner.ApiKey, _vision.ApiKey);
    private string? Model => _planner.Model; // model must be a text model set explicitly

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(Model) &&
        (!_planner.RequireApiKey || !string.IsNullOrWhiteSpace(ApiKey));

    public string ProviderLabel
    {
        get
        {
            var provider = _planner.ProviderName
                ?? (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var u) ? u.Host : "planner");
            return $"{provider} · {Model}";
        }
    }

    public async Task<Result<IReadOnlyList<NeededItem>>> ProposeItemsAsync(string description, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return Result<IReadOnlyList<NeededItem>>.Failure(
                "AI planning is not configured. Set a Planner model (and reuse your Vision provider, or set Planner base URL + key).");

        var body = new
        {
            model = Model,
            temperature = 0.2,
            max_tokens = 1200,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = $"Project description:\n{description}" }
            }
        };

        var client = httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_planner.TimeoutSeconds, 5, 300));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl!.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(body)
        };
        if (!string.IsNullOrWhiteSpace(ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        foreach (var (k, v) in _planner.ExtraHeaders) request.Headers.TryAddWithoutValidation(k, v);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result<IReadOnlyList<NeededItem>>.Failure("The planner timed out. Try again or use a faster model.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Planner request failed");
            return Result<IReadOnlyList<NeededItem>>.Failure("Could not reach the planner provider. Check the base URL and network.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await SafeReadAsync(response, ct);
            logger.LogWarning("Planner provider returned {Status}: {Detail}", (int)response.StatusCode, detail);
            return Result<IReadOnlyList<NeededItem>>.Failure(
                $"The planner provider returned an error ({(int)response.StatusCode}). Check the model id and your API key/quota.");
        }

        var raw = await response.Content.ReadAsStringAsync(ct);
        var content = OpenAiChatJson.ExtractMessageContent(raw);
        if (string.IsNullOrWhiteSpace(content))
            return Result<IReadOnlyList<NeededItem>>.Failure("The planner returned an empty response.");

        var parsed = ParseItems(content);
        if (parsed is null)
            return Result<IReadOnlyList<NeededItem>>.Failure("The planner's response could not be understood. Try again or use a different model.");

        var items = parsed
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => new NeededItem
            {
                Name = i.Name!.Trim(),
                Category = Clean(i.Category),
                Quantity = i.Quantity,
                Unit = Clean(i.Unit),
                Kind = ParseKind(i.Kind),
                Keywords = (i.Keywords ?? new()).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList()
            })
            .ToList();

        logger.LogInformation("Planner proposed {Count} needed item(s) via {Provider}", items.Count, ProviderLabel);
        return Result<IReadOnlyList<NeededItem>>.Success(items);
    }

    private const string SystemPrompt =
        "You help plan home/DIY projects. Given a project description, list the tools and materials " +
        "needed. Respond with ONLY a JSON object, no prose or markdown fences: " +
        "{\"items\":[{\"name\":string,\"category\":string,\"quantity\":number,\"unit\":string," +
        "\"kind\":\"material\"|\"tool\",\"keywords\":[string]}]}. " +
        "kind is \"material\" for things consumed (wire, screws, paint) and \"tool\" for reusable tools. " +
        "Include a realistic quantity and unit for materials (e.g. 25 ft). keywords are alternate names/synonyms " +
        "to help find the item in an inventory (e.g. [\"romex\",\"nm-b\"] for electrical cable). Keep the list practical.";

    private static string? Pick(string? primary, string? fallback) =>
        !string.IsNullOrWhiteSpace(primary) ? primary : fallback;

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static ItemKind ParseKind(string? kind) => kind?.Trim().ToLowerInvariant() switch
    {
        "material" => ItemKind.Material,
        "tool" => ItemKind.Tool,
        _ => ItemKind.Unknown
    };

    private static List<RawItem>? ParseItems(string content)
    {
        var json = OpenAiChatJson.StripToJson(content);
        if (json is null) return null;
        try
        {
            if (json.TrimStart().StartsWith('['))
                return JsonSerializer.Deserialize<List<RawItem>>(json, JsonOpts);
            return JsonSerializer.Deserialize<RawWrapper>(json, JsonOpts)?.Items;
        }
        catch (JsonException)
        {
            return null;
        }
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
        public decimal? Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Kind { get; set; }
        public List<string>? Keywords { get; set; }
    }
}
