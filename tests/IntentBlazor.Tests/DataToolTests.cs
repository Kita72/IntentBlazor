using System.ComponentModel;
using IntentBlazor.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace IntentBlazor.Tests;

public sealed class DataToolTests
{
    [Theory]
    [InlineData("valid", 1)]
    [InlineData("extra", 0)]
    [InlineData("missing", 0)]
    [InlineData("wrong-type", 0)]
    public async Task DataTools_ValidateParametersBeforeCallingTheScopedService(string mode, int expectedCalls)
    {
        Dictionary<string, object?> arguments = new() { ["game"] = "Example RPG", ["count"] = 2, ["includeRatings"] = true };
        if (mode == "extra") arguments["privateAdminFlag"] = true;
        if (mode == "missing") arguments.Remove("game");
        if (mode == "wrong-type") arguments["count"] = "not an integer";
        ScriptChatClient client = new(ModelTestSupport.Tool("parameterized_news", arguments), ModelTestSupport.Text(ModelTestSupport.Envelope()));
        ServiceCollection services = new();
        services.AddIntentBlazorAI(options => options.MaximumCorrections = 0)
            .UseChatClient(_ => client, new("test", "test", null)).AddTools<ParameterTools>();
        using ServiceProvider root = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = root.CreateScope();
        bool accepted = (await scope.ServiceProvider.GetRequiredService<AiSession>().SubmitAsync("news")).Success;
        Assert.Equal(mode == "valid", accepted);
        ParameterTools instance = scope.ServiceProvider.GetRequiredService<ParameterTools>();
        Assert.Equal(expectedCalls, instance.Calls);
        AIFunction function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(client.Calls[0].Options!.Tools!));
        Assert.Contains("Game filter", function.JsonSchema.GetRawText());
        Assert.DoesNotContain("cancellationToken", function.JsonSchema.GetRawText());
        if (expectedCalls == 1)
        {
            Assert.Equal("Example RPG", instance.Game);
            Assert.Equal(2, instance.Count);
            Assert.True(instance.IncludeRatings);
        }
    }

    public sealed class ParameterTools
    {
        public int Calls { get; private set; }
        public string? Game { get; private set; }
        public int Count { get; private set; }
        public bool IncludeRatings { get; private set; }
        [AiDataTool("parameterized_news", "Read parameterized news")]
        public OutputDataSet Read([Description("Game filter")] string game, int count, bool includeRatings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            Game = game; Count = count; IncludeRatings = includeRatings;
            return TestContent.Catalog.Get("news").Data[0];
        }
    }
}
