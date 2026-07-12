using System.Net;
using System.Text;
using HomeStock.Application.Models;
using HomeStock.Infrastructure.Ai;
using HomeStock.Infrastructure.Vision;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HomeStock.Tests;

public class PlannerParsingTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private static OpenAiCompatiblePlanner Build(HttpMessageHandler handler, PlannerOptions planner, VisionOptions? vision = null) =>
        new(new StubFactory(handler), Options.Create(planner), Options.Create(vision ?? new VisionOptions()),
            NullLogger<OpenAiCompatiblePlanner>.Instance);

    private static string Envelope(string content)
    {
        var escaped = System.Text.Json.JsonSerializer.Serialize(content);
        return $"{{\"choices\":[{{\"message\":{{\"content\":{escaped}}}}}]}}";
    }

    [Fact]
    public void Falls_Back_To_Vision_Credentials_But_Needs_Own_Model()
    {
        var planner = Build(new StubHandler(HttpStatusCode.OK, "{}"),
            new PlannerOptions { Model = "text-model" },
            new VisionOptions { BaseUrl = "https://example.test/v1", ApiKey = "k" });

        Assert.True(planner.IsConfigured); // base url + key from Vision, model from Planner
    }

    [Fact]
    public async Task Parses_Needed_Items()
    {
        var content = """{"items":[{"name":"14/2 wire","category":"Electrical","quantity":25,"unit":"ft","kind":"material","keywords":["romex"]},{"name":"drill","kind":"tool"}]}""";
        var planner = Build(new StubHandler(HttpStatusCode.OK, Envelope(content)),
            new PlannerOptions { BaseUrl = "https://example.test/v1", Model = "m", ApiKey = "k" });

        var result = await planner.ProposeItemsAsync("wire an outlet");

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal(ItemKind.Material, result.Value[0].Kind);
        Assert.Equal(25, result.Value[0].Quantity);
        Assert.Contains("romex", result.Value[0].Keywords);
        Assert.Equal(ItemKind.Tool, result.Value[1].Kind);
    }

    [Fact]
    public async Task Unconfigured_Returns_Friendly_Failure()
    {
        var planner = Build(new StubHandler(HttpStatusCode.OK, "{}"), new PlannerOptions());
        var result = await planner.ProposeItemsAsync("x");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("not configured"));
    }
}
