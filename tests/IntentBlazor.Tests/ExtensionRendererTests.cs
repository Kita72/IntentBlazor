using System.Collections.Immutable;
using IntentBlazor.Blazor;
using IntentBlazor.Content;
using IntentBlazor.Packs.ImageEditor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntentBlazor.Tests;

public sealed class ExtensionRendererTests
{
    [Fact]
    public async Task RegisteredRenderer_UsesTypedConfigurationAndRevisionCheckedProtectedEvents()
    {
        ServiceCollection services = new(); services.AddLogging(); services.AddIntentRenderer<CounterContract, CounterView>();
        using ServiceProvider root = services.BuildServiceProvider(); using IServiceScope scope = root.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>(); OutputContent document = CounterDocument();
        Assert.True(session.Open(document).Success);
        await using HtmlRenderer renderer = new(scope.ServiceProvider, root.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<AiOutput>(); Assert.Contains("Counter value: 0", output.ToHtmlString());
            long revision = session.Revision;
            Assert.True(session.DispatchExtension("counterNode", revision, new CounterEvent(1)).Success);
            Assert.Contains("Counter value: 1", output.ToHtmlString()); Assert.True(session.HasDraft);
            Assert.Equal("stale-revision", Assert.Single(session.DispatchExtension("counterNode", revision, new CounterEvent(1)).Errors).Code);
            Assert.False(session.DispatchExtension("counterNode", session.Revision, new { delta = "wrong type" }).Success);
            Assert.False(session.DispatchExtension("counterNode", session.Revision, new CounterEvent(10)).Success);
            Assert.False(session.DispatchExtension("counterNode", session.Revision, new CounterEvent(99)).Success);
            Assert.Equal("1", TestContent.Value(session, "count"));
            Assert.True(session.ApplyUpdate(document, session.Revision).Success); Assert.Equal("1", TestContent.Value(session, "count"));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingExtension_UsesEncodedOptionalFallbackOrRejectsRequiredVersion(bool required)
    {
        OutputContent content = CounterDocument();
        content = content with
        {
            Document = content.Document with
            {
                Requires = required ? content.Document.Requires : ImmutableDictionary<string, string>.Empty.Add("builtin", "0.1")
            }
        };
        ServiceCollection services = new(); services.AddLogging(); services.AddIntentBlazor();
        using ServiceProvider provider = services.BuildServiceProvider(); using IServiceScope scope = provider.CreateScope();
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>(); var result = session.Open(content);
        if (required) { Assert.Equal("toolkit", Assert.Single(result.Errors).Code); Assert.Null(session.Document); return; }
        Assert.True(result.Success);
        await using HtmlRenderer renderer = new(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            string html = (await renderer.RenderComponentAsync<AiOutput>()).ToHtmlString();
            Assert.Contains("&lt;script&gt;fallback&lt;/script&gt;", html); Assert.DoesNotContain("<script>", html);
        });
        Assert.False(session.DispatchExtension("counterNode", session.Revision, new CounterEvent(1)).Success);
    }

    [Fact]
    public async Task ImagePack_CreatesAnActualDerivedPngAndRetainsOriginalAcrossRecovery()
    {
        using ContentHarness h = new(); using ContentScope scope = h.Scope();
        ContentArtifact original = await scope.Import(ContentSamples.Image(), "original.png", "image/png");
        Assert.True((await scope.Session.ConnectAsync("images")).Success);
        scope.Session.SetArtifacts([original.Reference]); Assert.True((await scope.Session.SubmitAsync("Inspect images")).Success);
        Assert.True(scope.Session.Dispatch(new("files", UiEventKind.Select, scope.Session.Revision) { Value = JsonValues.From(original.Reference.Id) }).Success);
        Assert.True(scope.Session.Change("width", "16", scope.Session.Revision).Success);
        Assert.True(scope.Session.Change("height", "24", scope.Session.Revision).Success);
        ActionRequest request = Assert.IsType<ActionRequest>(scope.Session.PrepareAction("resize", scope.Session.Revision));
        Assert.Equal(ActionStatus.Succeeded, (await scope.Session.ExecuteActionAsync(request)).Status); Assert.False(scope.Session.HasDraft);
        string preview = TestContent.Value(scope.Session, "previewArtifact"); Assert.NotEqual(original.Reference.Id, preview);
        ArtifactReference reference = Assert.Single(scope.Session.Artifacts, item => item.Id == preview);
        ContentArtifact derived = (await scope.Content.DescribeAsync(reference))!;
        Assert.Equal(original.Reference, derived.Parent); Assert.Equal(16, derived.Width); Assert.Equal(24, derived.Height);
        byte[] bytes = (await h.Provider.GetRequiredService<IArtifactStore>().ReadAsync("owner-a", reference))!;
        using var bitmap = SkiaSharp.SKBitmap.Decode(bytes); Assert.Equal(16, bitmap.Width); Assert.Equal(24, bitmap.Height);
        Assert.Equal(SkiaSharp.SKColors.Gold, bitmap.GetPixel(8, 12));
        Assert.Equal(ContentSamples.Image(), await h.Provider.GetRequiredService<IArtifactStore>().ReadAsync("owner-a", original.Reference));
        await using (HtmlRenderer renderer = new(scope.Services, scope.Services.GetRequiredService<ILoggerFactory>()))
            await renderer.Dispatcher.InvokeAsync(async () =>
            {
                string html = (await renderer.RenderComponentAsync<AiOutput>()).ToHtmlString();
                Assert.Contains("Show original", html); Assert.DoesNotContain("@onclick", html); Assert.DoesNotContain("@bind", html);
            });
        Assert.True(scope.Session.DispatchExtension("editor", scope.Session.Revision, new ImageEditorEvent(true)).Success);
        Assert.True(scope.Session.Snapshot.Values["showOriginal"].GetBoolean()); Assert.False(scope.Session.HasDraft);
        Assert.True((await scope.Session.SaveAsync()).Success);
        using ContentScope restored = h.Scope(); Assert.True((await restored.Session.RestoreAsync("images")).Success);
        Assert.Equal(preview, TestContent.Value(restored.Session, "previewArtifact")); Assert.Equal(2, restored.Session.Artifacts.Length);
        Assert.True(restored.Session.Snapshot.Values["showOriginal"].GetBoolean());
    }

    [Fact]
    public async Task NewNodes_RenderGalleryChartMarkdownAndAuthorizedArtifactThroughAiOutput()
    {
        using ContentHarness h = new(); using ContentScope scope = h.Scope();
        ContentArtifact text = await scope.Import("# Heading\n<script>unsafe</script>"u8.ToArray(), "notes.md", "text/markdown");
        await scope.Session.ConnectAsync("content"); scope.Session.SetArtifacts([text.Reference]);
        Assert.True((await scope.Session.SubmitAsync("Show content")).Success);
        scope.Session.Dispatch(new("files", UiEventKind.Select, scope.Session.Revision) { Value = JsonValues.From(text.Reference.Id) });
        await using HtmlRenderer renderer = new(scope.Services, scope.Services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            string html = (await renderer.RenderComponentAsync<AiOutput>()).ToHtmlString();
            Assert.Contains("<svg", html); Assert.Contains("Chart data", html); Assert.Contains("Select notes.md", html);
            Assert.Contains("<h2>Heading</h2>", html); Assert.Contains("&lt;script&gt;unsafe&lt;/script&gt;", html); Assert.DoesNotContain("<script>", html);
            Assert.Contains("Download notes.md", html); Assert.Contains("/intent-artifacts/", html);
        });
    }

    public sealed record CounterConfiguration(string StateKey);
    public sealed record CounterEvent(int Delta);
    public sealed class CounterContract : OutputExtensionContract<CounterConfiguration, CounterEvent>
    {
        public override ExtensionDescriptor Descriptor { get; } = new("counter", "1.0", "counter", "A test-owned public extension.", JsonValues.From(new CounterConfiguration("count")));
        public override OperationResult Validate(CounterConfiguration configuration, OutputContent content) =>
            content.Document.State.TryGetValue(configuration.StateKey, out StateDefinition? state) && state.Kind == StateKind.Number
                ? OperationResult.Ok : OperationResult.Fail("counter-state", "A numeric counter state is required.");
        public override ImmutableArray<string> EditableStates(CounterConfiguration configuration) => [configuration.StateKey];
        public override ImmutableArray<ExtensionEffect> Reduce(CounterConfiguration configuration, CounterEvent message, ExtensionState state) =>
            message.Delta == 99 ? [null!] : [new(configuration.StateKey, JsonValues.From(state.Values[configuration.StateKey].GetInt32() + message.Delta), true)];
    }
    public sealed class CounterView : IntentExtensionComponent<CounterConfiguration>
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        { builder.OpenElement(0, "p"); builder.AddContent(1, "Counter value: " + JsonValues.Display(Session.Snapshot.Values[Configuration.StateKey])); builder.CloseElement(); }
    }
    private static OutputContent CounterDocument() => new()
    {
        Document = new()
        {
            SchemaVersion = "0.3",
            Id = "counter",
            RootId = "counterNode",
            Requires = ImmutableDictionary<string, string>.Empty.Add("builtin", "0.1").Add("counter", "1.0"),
            State = ImmutableDictionary<string, StateDefinition>.Empty.Add("count", new() { Kind = StateKind.Number, InitialValue = JsonValues.From(0), Minimum = 0, Maximum = 3 }),
            Nodes = [new() { Id = "counterNode", Kind = NodeKind.Extension, Title = "Counter", Extension = new("counter", "1.0", "counter", JsonValues.From(new CounterConfiguration("count")), "<script>fallback</script>") }]
        }
    };
}
