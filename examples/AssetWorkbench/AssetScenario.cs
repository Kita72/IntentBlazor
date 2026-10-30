using System.Collections.Immutable;
using IntentBlazor.Content;
using IntentBlazor.Core;
using IntentBlazor.ExampleHost;
using IntentBlazor.Packs.ImageEditor;
using IntentBlazor.Runtime;
using SkiaSharp;

namespace IntentBlazor.AssetWorkbench;

public sealed class AssetScenario(IWorkspaceContent content, IExampleActor actor) : IExampleScenario
{
    public string InitialRequest => "Inspect attached assets and dimension groups. Select a version, resize it, compare with its original, or use the original as the working version.";
    public async ValueTask<ImmutableArray<OutputDataSet>> LoadAsync(WorkspaceSnapshot workspace, CancellationToken token)
    {
        string? owner = (await actor.ReadAsync(token))?.Id;
        if (owner is null) throw new InputRejectedException("Sign in to inspect private assets.");
        List<ContentArtifact> artifacts = [];
        foreach (var reference in workspace.Artifacts)
            artifacts.Add(await content.DescribeAsync(reference, token) ?? throw new InputRejectedException("An attached asset is unavailable for this identity."));
        if ((await actor.ReadAsync(token))?.Id != owner) throw new InputRejectedException("The asset identity changed.");
        return [ArtifactData.Create(content, artifacts)];
    }
    public async ValueTask<WorkspaceAccess> ReauthorizeAsync(string owner, ImmutableArray<string> ids, ImmutableArray<ArtifactReference> artifacts, CancellationToken token)
    {
        if ((await actor.ReadAsync(token))?.Id != owner || ids.Any(id => id != "artifacts")) return new(false, []);
        try { return new(true, ids.IsEmpty ? [] : await LoadAsync(ExampleData.EmptyWorkspace with { Artifacts = artifacts }, token)); }
        catch (InputRejectedException) { return new(false, []); }
    }
    public async ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken token)
    {
        var data = (await LoadAsync(workspace, token))[0];
        OutputContent document = ImageEditorDocuments.Create(data, input);
        return document with
        {
            Document = document.Document with
            {
                Id = "asset-workbench",
                Requires = document.Document.Requires.Add("assets", "1.0"),
                Nodes = document.Document.Nodes.Select(node => node.Id switch
                {
                    "editor" => node with { Children = node.Children.Add("useOriginal") },
                    "files" => new OutputNode { Id = "files", Kind = NodeKind.Extension, Title = "Asset versions", Extension = new("assets", "1.0", "versions", JsonValues.From(new AssetVersionsConfiguration("selectedArtifact", "previewArtifact", "showOriginal", "isImage")), "The asset version picker is unavailable.") },
                    _ => node
                }).ToImmutableArray().Add(ExampleData.Button("useOriginal", "Use original as working version", "useOriginal")),
                Interactions = document.Document.Interactions.Where(interaction => interaction.Id != "chooseFile").ToImmutableArray()
                .Add(new() { Id = "useOriginal", Event = UiEventKind.Activate, Assignments = [new("previewArtifact", ValueBinding.FromState("selectedArtifact")), new("showOriginal", ValueBinding.Constant(false))] })
            }
        };
    }
}
public static class AssetSamples
{
    public static async Task<ImmutableArray<ArtifactReference>> CreateAsync(IWorkspaceContent content, CancellationToken token = default)
    {
        var references = ImmutableArray.CreateBuilder<ArtifactReference>();
        foreach (var sample in new[] { (Name: "gold-tile.png", Width: 64, Height: 64, Color: SKColors.Gold), (Name: "teal-tile.png", Width: 48, Height: 32, Color: SKColors.Teal) })
        {
            using SKBitmap bitmap = new(sample.Width, sample.Height); using SKCanvas canvas = new(bitmap); canvas.Clear(sample.Color);
            using SKPaint border = new() { Color = SKColors.DarkSlateGray, Style = SKPaintStyle.Stroke, StrokeWidth = 4 };
            canvas.DrawRect(2, 2, sample.Width - 4, sample.Height - 4, border);
            using SKImage image = SKImage.FromBitmap(bitmap); using SKData png = image.Encode(SKEncodedImageFormat.Png, 100); using MemoryStream stream = new(png.ToArray());
            references.Add((await content.ImportAsync(new(sample.Name, "image/png", stream.Length), stream, token)).Reference);
        }
        return references.ToImmutable();
    }
}
