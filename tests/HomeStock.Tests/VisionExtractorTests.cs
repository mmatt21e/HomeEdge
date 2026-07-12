using System.Net;
using System.Text;
using HomeStock.Application.Models;
using HomeStock.Infrastructure.Vision;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HomeStock.Tests;

public class VisionExtractorTests
{
    /// <summary>Test double: returns a fixed HTTP response for any request.</summary>
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private static OpenAiCompatibleVisionExtractor Build(HttpMessageHandler handler, VisionOptions options) =>
        new(new StubFactory(handler), Options.Create(options), NullLogger<OpenAiCompatibleVisionExtractor>.Instance);

    private static VisionOptions Configured() =>
        new() { BaseUrl = "https://example.test/v1", Model = "vision-model", ApiKey = "k" };

    private static Stream Png() => new MemoryStream(new byte[] { 1, 2, 3, 4 });

    private static string Envelope(string content)
    {
        // OpenAI-compatible chat/completions response with the model's content as a string.
        var escaped = System.Text.Json.JsonSerializer.Serialize(content);
        return $"{{\"choices\":[{{\"message\":{{\"role\":\"assistant\",\"content\":{escaped}}}}}]}}";
    }

    [Fact]
    public void Not_Configured_When_Fields_Missing()
    {
        var svc = Build(new StubHandler(HttpStatusCode.OK, "{}"), new VisionOptions());
        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public async Task Unconfigured_Extract_Returns_Friendly_Failure()
    {
        var svc = Build(new StubHandler(HttpStatusCode.OK, "{}"), new VisionOptions());
        var result = await svc.ExtractAsync(Png(), "image/png", VisionMode.Scene);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("not configured"));
    }

    [Fact]
    public async Task Parses_Items_From_Wrapper_Json()
    {
        var content = """{"items":[{"name":"Cordless Drill","category":"Tools","quantity":1,"brand":"DeWalt","model_number":"DCD777","confidence":0.9},{"name":"Safety Glasses","quantity":2}]}""";
        var svc = Build(new StubHandler(HttpStatusCode.OK, Envelope(content)), Configured());

        var result = await svc.ExtractAsync(Png(), "image/png", VisionMode.Scene);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Items.Count);
        var drill = result.Value.Items[0];
        Assert.Equal("Cordless Drill", drill.Name);
        Assert.Equal("DCD777", drill.ModelNumber);
        Assert.Equal(2, result.Value.Items[1].Quantity);
    }

    [Fact]
    public async Task Tolerates_Markdown_Fenced_Json()
    {
        var content = "Here you go:\n```json\n{\"items\":[{\"name\":\"Hammer\"}]}\n```";
        var svc = Build(new StubHandler(HttpStatusCode.OK, Envelope(content)), Configured());

        var result = await svc.ExtractAsync(Png(), "image/png", VisionMode.SingleProduct);

        Assert.True(result.Succeeded);
        Assert.Equal("Hammer", result.Value!.Items.Single().Name);
    }

    [Fact]
    public async Task Http_Error_Returns_Friendly_Failure_Without_Leaking()
    {
        var svc = Build(new StubHandler(HttpStatusCode.Unauthorized, "{\"error\":\"bad key sk-secret\"}"), Configured());

        var result = await svc.ExtractAsync(Png(), "image/png", VisionMode.Scene);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("401"));
        Assert.DoesNotContain(result.Errors, e => e.Contains("sk-secret"));
    }

    [Fact]
    public async Task Oversized_Image_Is_Rejected()
    {
        var options = Configured();
        options.MaxImageBytes = 2;
        var svc = Build(new StubHandler(HttpStatusCode.OK, Envelope("{\"items\":[]}")), options);

        var result = await svc.ExtractAsync(new MemoryStream(new byte[10]), "image/png", VisionMode.Scene);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("larger than"));
    }
}
