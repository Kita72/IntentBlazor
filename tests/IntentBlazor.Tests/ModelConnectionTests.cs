using System.Net;
using System.Text.Json;
using IntentBlazor.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace IntentBlazor.Tests;

public sealed class ModelConnectionTests
{
    [Theory]
    [InlineData("Ollama", "http://localhost:11434/v1/chat/completions", "Bearer ollama")]
    [InlineData("LM Studio", "http://localhost:1234/v1/chat/completions", "Bearer lm-studio")]
    [InlineData("OpenAI", "https://api.openai.com/v1/chat/completions", "Bearer test-only-key")]
    public async Task Adapters_SendStreamingRequestsThroughTheSameValidatedPipeline(string provider, string uri, string authorization)
    {
        ServiceCollection services = new();
        RecordingHandler handler = new();
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new TestHandlerFilter(handler));
        IntentModelBuilder builder = services.AddIntentBlazorAI();
        _ = provider switch
        {
            "Ollama" => builder.UseOllama("test-model"),
            "LM Studio" => builder.UseLMStudio("test-model"),
            _ => builder.UseOpenAI("test-model", _ => "test-only-key")
        };
        await using ServiceProvider root = services.BuildServiceProvider(validateScopes: true);
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        Assert.True((await session.SubmitAsync("Build a simple output")).Success);
        Assert.Equal("answer", session.Document!.Id);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(uri, request.Uri!.ToString());
        Assert.Equal(authorization, request.Authorization);
        using JsonDocument body = JsonDocument.Parse(request.Body);
        Assert.Equal("test-model", body.RootElement.GetProperty("model").GetString());
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(8000, body.RootElement.GetProperty(provider == "OpenAI" ? "max_completion_tokens" : "max_tokens").GetInt32());
        Assert.False(body.RootElement.TryGetProperty(provider == "OpenAI" ? "max_tokens" : "max_completion_tokens", out _));
        Assert.Equal("json_schema", body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        Assert.Contains("documentJson", request.Body);
        Assert.Equal(provider, scope.ServiceProvider.GetRequiredService<ModelInputProcessor>().LastRun!.Provider);
        Assert.NotNull(session.Progress);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Adapters_PreserveOutputAndSanitizeProviderFailures(int status)
    {
        ServiceCollection services = new();
        RecordingHandler handler = new() { Status = (HttpStatusCode)status };
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new TestHandlerFilter(handler));
        services.AddIntentBlazorAI().UseOpenAI("test-model", _ => "test-only-key");
        using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        session.Open(TestContent.Catalog.Get("news"));
        Assert.False((await session.SubmitAsync("hello")).Success);
        Assert.Equal("news-reader", session.Document!.Id);
        Assert.DoesNotContain("private upstream", session.Errors[0].Message);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("http://other-host.example/v1/")]
    [InlineData("https://user:password@example.invalid/v1/")]
    [InlineData("https://example.invalid/v1/?secret=123")]
    [InlineData("https://example.invalid/v1")]
    public void Connection_RejectsUnsafeOrAmbiguousBaseUris(string endpoint)
    {
        ServiceCollection services = new();
        Assert.Throws<ArgumentException>(() => services.AddIntentBlazorAI().UseLMStudio("test", new(endpoint)));
    }

    [Fact]
    public void Connection_RequiresAModelAndOneSelectedProvider()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddIntentBlazorAI().UseOllama(""));
        IntentModelBuilder builder = new ServiceCollection().AddIntentBlazorAI().UseOllama("test");
        Assert.Throws<InvalidOperationException>(() => builder.UseLMStudio("other"));
    }

    [Fact]
    public async Task MissingCredential_IsReportedOnSubmissionWithoutBlockingSessionResolution()
    {
        ServiceCollection services = new();
        services.AddIntentBlazorAI().UseOpenAI("test-model", _ => throw new InvalidOperationException("private credential store error"));
        using ServiceProvider root = services.BuildServiceProvider();
        using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        session.Open(TestContent.Catalog.Get("news"));
        Assert.False((await session.SubmitAsync("news")).Success);
        Assert.Equal("news-reader", session.Document!.Id);
        Assert.DoesNotContain("private credential", session.Errors[0].Message);
    }
}
