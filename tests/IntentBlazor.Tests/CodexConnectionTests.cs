using System.Text.Json;
using IntentBlazor.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

public sealed class CodexConnectionTests
{
    [Fact]
    public async Task Codex_RequiresACompletedFinalMessageAndReportsUsage()
    {
        string envelope = ModelTestSupport.Envelope();
        ScriptCodexRunner runner = new(
            Event("item.completed", new { type = "agent_message", phase = "commentary", text = "Working" }),
            Event("item.completed", new { type = "agent_message", phase = "final_answer", text = envelope }),
            "{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":37,\"output_tokens\":19}}");
        using CodexChatClient client = new(new() { Model = "test-codex" }, runner);
        ChatResponse response = await client.GetResponseAsync([new(ChatRole.System, "Build JSON"), new(ChatRole.User, "News")]);
        Assert.Equal(envelope, response.Text);
        Assert.Equal(37, response.Usage!.InputTokenCount);
        Assert.Equal(19, response.Usage.OutputTokenCount);
        Assert.Contains("system:", runner.Request!.Prompt);
        Assert.Contains("News", runner.Request.Prompt);
        using JsonDocument schema = JsonDocument.Parse(runner.Request.OutputSchema);
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("failed")]
    [InlineData("no-message")]
    [InlineData("oversized")]
    [InlineData("malformed")]
    public async Task Codex_RejectsIncompleteFailedOrUnboundedEvents(string mode)
    {
        string line = mode switch
        {
            "failed" => "{\"type\":\"turn.failed\",\"error\":{\"message\":\"private backend detail\"}}",
            "no-message" => "{\"type\":\"turn.completed\"}",
            "oversized" => new string('x', 1025),
            "malformed" => "not JSON",
            _ => Event("item.completed", new { type = "agent_message", text = "unfinished" })
        };
        using CodexChatClient client = new(new() { Model = "test", MaximumEventBytes = 1024 }, new ScriptCodexRunner(line));
        Exception error = mode == "malformed"
            ? await Assert.ThrowsAnyAsync<JsonException>(() => client.GetResponseAsync([new(ChatRole.User, "News")]))
            : await Assert.ThrowsAsync<InputRejectedException>(() => client.GetResponseAsync([new(ChatRole.User, "News")]));
        Assert.DoesNotContain("private backend", error.Message);
    }

    [Fact]
    public void Codex_ProcessArgumentsIsolateWorkAndDoNotEmbedThePrompt()
    {
        var start = CodexProcessRunner.CreateStartInfo(new() { ExecutablePath = "native-codex", Model = "test-model" },
            "C:/isolated path", "C:/isolated path/schema.json");
        Assert.Equal("native-codex", start.FileName);
        Assert.False(start.UseShellExecute);
        Assert.True(start.CreateNoWindow);
        Assert.True(start.RedirectStandardInput);
        string[] args = start.ArgumentList.ToArray();
        Assert.Contains("--ignore-user-config", args);
        Assert.Contains("--ignore-rules", args);
        Assert.Contains("--ephemeral", args);
        Assert.Contains("read-only", args);
        Assert.Contains("shell_tool", args);
        Assert.Contains("apps", args);
        Assert.Contains("web_search=\"disabled\"", args);
        Assert.Equal("C:/isolated path", args[Array.IndexOf(args, "-C") + 1]);
        Assert.Equal("C:/isolated path/schema.json", args[Array.IndexOf(args, "--output-schema") + 1]);
        Assert.Equal("-", args[^1]);
        Assert.DoesNotContain("--dangerously-bypass-approvals-and-sandbox", args);
    }

    [Theory]
    [InlineData("temperature")]
    [InlineData("tokens")]
    [InlineData("native-tools")]
    public async Task Codex_RejectsNativeToolsAndUnsupportedGenerationOptions(string setting)
    {
        ScriptCodexRunner runner = new();
        using CodexChatClient client = new(new() { Model = "test" }, runner);
        ChatOptions options = setting switch
        {
            "temperature" => new() { Temperature = .5f },
            "tokens" => new() { MaxOutputTokens = 1000 },
            _ => new() { Tools = [AIFunctionFactory.Create(() => "value")] }
        };
        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetResponseAsync([new(ChatRole.User, "Hi")], options));
        Assert.Null(runner.Request);
    }

    [Fact]
    public async Task Codex_RegistrationFeedsTheSameSessionAndInteractiveRendererContract()
    {
        ScriptCodexRunner runner = new(Event("item.completed", new { type = "agent_message", text = ModelTestSupport.Envelope() }),
            "{\"type\":\"turn.completed\"}");
        ServiceCollection services = new();
        services.AddSingleton(TestContent.Catalog);
        services.AddSingleton<ICodexProcessRunner>(runner);
        services.AddIntentBlazorAI().UseCodex(new() { Model = "test-codex" }).AddTools<NewsDataService>();
        using ServiceProvider root = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        Assert.True((await session.SubmitAsync("news")).Success);
        TestContent.Select(session, "headlines", "news-2");
        Assert.Equal("news-2", TestContent.Value(session, "selectedArticle"));
        Assert.Contains("Fictional fixture:", runner.Request!.Prompt);
        ModelProfile profile = scope.ServiceProvider.GetRequiredService<ModelProfile>();
        Assert.False(profile.NativeTools);
        Assert.False(profile.SupportsOutputTokenLimit);
        Assert.False(profile.IncrementalText);
    }

    private static string Event(string type, object item) => JsonSerializer.Serialize(new { type, item });
}
