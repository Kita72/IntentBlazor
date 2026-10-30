using IntentBlazor.AssetWorkbench;
using IntentBlazor.Content;
using IntentBlazor.Packs.ImageEditor;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace IntentBlazor.Tests;

public sealed class Phase5AssetTests
{
    [Fact]
    public async Task ImportsDimensionGroupsResizeVersionSelectionOriginalRecoveryAndPngExport_UsePublicComponents()
    {
        using Phase5Harness h = await Phase5Harness.CreateAsync("assets"); using Phase5Scope scope = h.Scope(); await scope.StartAsync();
        var content = scope.Services.GetRequiredService<IWorkspaceContent>(); var samples = await AssetSamples.CreateAsync(content);
        Assert.True(scope.Session.SetArtifacts(samples).Success); Assert.True((await scope.Session.SubmitAsync("Find inconsistent dimensions")).Success);
        string html = (await scope.RenderAsync()).Output; Assert.Contains("Multiple dimension groups", html); Assert.Contains("64", html); Assert.Contains("48", html);
        string original = samples[0].Id;
        Assert.True(scope.Session.DispatchExtension("files", scope.Session.Revision, new AssetVersionEvent(original, original, true)).Success);
        scope.Change("width", "16"); scope.Change("height", "24"); Assert.Equal(ActionStatus.Succeeded, (await scope.ActivateAsync("resize")).Status);
        string derivedId = TestContent.Value(scope.Session, "previewArtifact"); Assert.NotEqual(original, derivedId);
        ArtifactReference derived = Assert.Single(scope.Session.Artifacts, item => item.Id == derivedId);
        Assert.Contains("data-dimension=\"16x24\"", (await scope.RenderAsync()).Output);
        var store = h.Provider.GetRequiredService<IArtifactStore>(); byte[] png = (await store.ReadAsync("alex", derived))!;
        using SKBitmap bitmap = SKBitmap.Decode(png); Assert.Equal(16, bitmap.Width); Assert.Equal(24, bitmap.Height); Assert.Equal(SKColors.Gold, bitmap.GetPixel(8, 12));
        Assert.Equal(samples[0], (await content.DescribeAsync(derived))!.Parent);
        Assert.True(scope.Session.DispatchExtension("files", scope.Session.Revision, new AssetVersionEvent(original, derivedId, true)).Success);
        Assert.True(scope.Session.DispatchExtension("editor", scope.Session.Revision, new ImageEditorEvent(true)).Success);
        Assert.Contains("Original: gold-tile.png", (await scope.RenderAsync()).Output);
        Assert.True(scope.Session.Dispatch(new("useOriginal", UiEventKind.Activate, scope.Session.Revision)).Success);
        Assert.Equal(original, TestContent.Value(scope.Session, "previewArtifact")); Assert.False(scope.Session.Snapshot.Values["showOriginal"].GetBoolean());
        Assert.True(scope.Session.DispatchExtension("files", scope.Session.Revision, new AssetVersionEvent(original, derivedId, true)).Success);
        Assert.True((await scope.Session.SaveAsync()).Success); using Phase5Scope restored = h.Scope(); await restored.StartAsync();
        Assert.Equal(original, TestContent.Value(restored.Session, "selectedArtifact")); Assert.Equal(derivedId, TestContent.Value(restored.Session, "previewArtifact"));
        string output = (await restored.RenderAsync()).Output; Assert.Contains(content.Url(derived, download: true).Replace("&", "&amp;"), output); Assert.DoesNotContain("@onclick", output);
        using Phase5Scope other = h.Scope("sam"); Assert.Null(await other.Services.GetRequiredService<IWorkspaceContent>().DescribeAsync(derived));
        Assert.True((await other.Session.ConnectAsync("main")).Success); Assert.Null(other.Session.Document);
    }
}
