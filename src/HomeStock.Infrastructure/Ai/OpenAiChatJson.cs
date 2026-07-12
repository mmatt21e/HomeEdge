using System.Text;
using System.Text.Json;

namespace HomeStock.Infrastructure.Ai;

/// <summary>Helpers for pulling JSON out of an OpenAI-compatible chat/completions response.</summary>
internal static class OpenAiChatJson
{
    /// <summary>Extracts choices[0].message.content (string, or concatenated text parts).</summary>
    public static string? ExtractMessageContent(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var msg = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content");
            if (msg.ValueKind == JsonValueKind.String) return msg.GetString();
            if (msg.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();
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

    /// <summary>Strips ```json fences and trims to the outermost JSON object/array.</summary>
    public static string? StripToJson(string content)
    {
        var s = content.Trim();
        if (s.StartsWith("```"))
        {
            var nl = s.IndexOf('\n');
            if (nl >= 0) s = s[(nl + 1)..];
            var fence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) s = s[..fence];
            s = s.Trim();
        }
        int obj = s.IndexOf('{'), arr = s.IndexOf('[');
        int start = (obj, arr) switch
        {
            (< 0, < 0) => -1,
            (< 0, _) => arr,
            (_, < 0) => obj,
            _ => Math.Min(obj, arr)
        };
        if (start < 0) return null;
        var close = s[start] == '{' ? '}' : ']';
        var end = s.LastIndexOf(close);
        return end > start ? s[start..(end + 1)] : null;
    }
}
