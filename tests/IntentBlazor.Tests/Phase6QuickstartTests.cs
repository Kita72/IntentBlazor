using IntentBlazor.AI;
using IntentBlazor.Blazor;
using IntentBlazor.Core;
using IntentBlazor.Quickstart;
using IntentBlazor.Runtime;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntentBlazor.Tests;

public sealed class Phase6QuickstartTests
{
    [Fact]
    public async Task DefaultPair_RendersAuthoredDataAndRegisteredInteractiveRendererWithoutAModel()
    {
        await using ServiceProvider provider = Services().BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        await using HtmlRenderer renderer = new(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<AiOutput>();
            Assert.True((await session.SubmitAsync("show catalog")).Success);
            string html = output.ToHtmlString();
            Assert.Contains("Brass lantern", html);
            Assert.Contains("Increase score", html);
            Assert.Contains("Clear score draft", html);
            Assert.True(session.DispatchExtension("scoreEditor", session.Revision, new ScoreEvent(1)).Success);
            Assert.Equal(4, session.Snapshot.Values["score"].GetDecimal());
            Assert.Contains("4 / 10", output.ToHtmlString());
            Assert.Contains("score", session.Snapshot.DirtyStates);
            Assert.True(session.Dispatch(new("clear", UiEventKind.Activate, session.Revision)).Success);
            Assert.Equal(3, session.Snapshot.Values["score"].GetDecimal());
            Assert.False(session.HasDraft);
            Assert.Contains(">Request</label>", (await renderer.RenderComponentAsync<AiInput>()).ToHtmlString());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Service_RegistersTheSameReadToolThroughAttributesOrExplicitMethods(bool explicitTools)
    {
        using ServiceProvider provider = Services(new() { ["Ai:Provider"] = "Ollama", ["Ai:Model"] = "sample-model", ["Ai:ExplicitTools"] = explicitTools.ToString() }).BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        DataToolRegistration registration = Assert.Single(scope.ServiceProvider.GetServices<DataToolRegistration>());
        Assert.Equal("catalog_items", registration.Name);
        Assert.Equal(nameof(CatalogService.Read), registration.Method.Name);
        CatalogService service = scope.ServiceProvider.GetRequiredService<CatalogService>();
        OutputDataSet filtered = Assert.IsType<OutputDataSet>(registration.Method.Invoke(service, ["lantern"]));
        Assert.Equal("lantern", Assert.Single(filtered.Rows)["id"].GetString());
        Assert.Empty(service.Read("no matching item").Rows);
    }

    [Theory]
    [InlineData("Ollama", "Ollama")]
    [InlineData("LMStudio", "LM Studio")]
    [InlineData("OpenAI", "OpenAI")]
    public void ProviderConfiguration_SelectsTheAdapterWithoutMakingALiveCall(string configured, string expected)
    {
        using ServiceProvider provider = Services(new() { ["Ai:Provider"] = configured, ["Ai:Model"] = "sample-model" }).BuildServiceProvider();
        ModelProfile profile = provider.GetRequiredService<ModelProfile>();
        Assert.Equal(expected, profile.Provider);
        Assert.Equal("sample-model", profile.Model);
        Assert.IsType<ModelInputProcessor>(provider.GetRequiredService<IInputProcessor>());
        Assert.Single(provider.GetServices<IOutputDataSource>());
    }

    [Fact]
    public async Task Renderer_ClampsBoundsRejectsMalformedAndStaleEventsAndRetainsDrafts()
    {
        using ServiceProvider provider = Services().BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        Assert.True((await session.SubmitAsync("catalog")).Success);
        for (int i = 0; i < 12; i++) Assert.True(session.DispatchExtension("scoreEditor", session.Revision, new ScoreEvent(1)).Success);
        Assert.Equal(10, session.Snapshot.Values["score"].GetDecimal());
        long revision = session.Revision;
        Assert.False(session.DispatchExtension("scoreEditor", revision - 1, new ScoreEvent(-1)).Success);
        Assert.False(session.DispatchExtension("scoreEditor", revision, new ScoreEvent(2)).Success);
        Assert.Equal(revision, session.Revision);
        Assert.Equal(10, session.Snapshot.Values["score"].GetDecimal());
        Assert.True(session.HasDraft);
        for (int i = 0; i < 12; i++) Assert.True(session.DispatchExtension("scoreEditor", session.Revision, new ScoreEvent(-1)).Success);
        Assert.Equal(0, session.Snapshot.Values["score"].GetDecimal());
    }

    [Fact]
    public async Task Renderer_RejectsConfigurationWithoutItsDeclaredNumericRange()
    {
        using ServiceProvider provider = Services().BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        Assert.True((await session.SubmitAsync("catalog")).Success);
        OutputContent invalid = new()
        {
            Data = session.Data,
            Document = session.Document! with { State = session.Document.State.SetItem("score", new() { Kind = StateKind.Number, InitialValue = JsonValues.From(3) }) }
        };
        Assert.False(session.Open(invalid).Success);
        Assert.Equal("extension-config", Assert.Single(session.Errors).Code);
        Assert.Equal(3, session.Snapshot.Values["score"].GetDecimal());
    }

    private static ServiceCollection Services(Dictionary<string, string?>? values = null)
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddQuickstart(configuration);
        return services;
    }
}
