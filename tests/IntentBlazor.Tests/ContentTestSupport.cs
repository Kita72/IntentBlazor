using System.Collections.Immutable;
using IntentBlazor.Content;
using IntentBlazor.Packs.ImageEditor;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace IntentBlazor.Tests;

internal sealed class ContentHarness : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "IntentBlazor-content-" + Guid.NewGuid().ToString("N"));
    public ServiceProvider Provider { get; }
    public ContentHarness(ContentLimits? limits = null, long ownerBytes = 64 * 1024 * 1024, int maximumArtifacts = 128, Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = new(); services.AddLogging();
        services.AddIntentContent(DirectoryPath, limits, new(DirectoryPath, ownerBytes, maximumArtifacts)); services.AddIntentImageEditor();
        services.AddScoped<MutableWorkspaceIdentity>(); services.AddScoped<IWorkspaceIdentity>(sp => sp.GetRequiredService<MutableWorkspaceIdentity>());
        services.AddScoped<IWorkspaceAccess, ArtifactFixtureAccess>(); services.AddScoped<IInputProcessor, ArtifactFixtureProcessor>();
        configure?.Invoke(services);
        Provider = services.BuildServiceProvider(validateScopes: true);
    }
    public ContentScope Scope(string? owner = "owner-a") => new(Provider.CreateScope(), owner);
    public void Dispose() { Provider.Dispose(); Directory.Delete(DirectoryPath, recursive: true); }
}
internal sealed class ContentScope : IDisposable
{
    private readonly IServiceScope _scope;
    public IServiceProvider Services => _scope.ServiceProvider;
    public MutableWorkspaceIdentity Identity { get; }
    public WorkspaceContent Content { get; }
    public AiSession Session { get; }
    public ContentScope(IServiceScope scope, string? owner)
    {
        _scope = scope; Identity = Services.GetRequiredService<MutableWorkspaceIdentity>(); Identity.Owner = owner;
        Services.GetRequiredService<WorkspaceServices>().AutoSave = false;
        Content = Services.GetRequiredService<WorkspaceContent>(); Session = Services.GetRequiredService<AiSession>();
    }
    public async Task<ContentArtifact> Import(byte[] bytes, string name, string mediaType)
    { using MemoryStream stream = new(bytes); return await Content.ImportAsync(new(name, mediaType, bytes.Length), stream); }
    public void Dispose() => _scope.Dispose();
}
internal sealed class ArtifactFixtureProcessor(IWorkspaceContent content) : IInputProcessor
{
    public async ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken token)
    {
        List<ContentArtifact> artifacts = [];
        foreach (var reference in workspace.Artifacts) if (await content.DescribeAsync(reference, token) is { } artifact) artifacts.Add(artifact);
        return ImageEditorDocuments.Create(ArtifactData.Create(content, artifacts), input);
    }
}
internal sealed class ArtifactFixtureAccess(IWorkspaceIdentity identity, IWorkspaceContent content) : IWorkspaceAccess
{
    public async ValueTask<WorkspaceAccess> ReauthorizeAsync(string owner, ImmutableArray<string> ids, ImmutableArray<ArtifactReference> references, CancellationToken token)
    {
        if (await identity.GetOwnerIdAsync(token) != owner) return new(false, []);
        List<ContentArtifact> artifacts = [];
        foreach (var reference in references)
        { if (await content.DescribeAsync(reference, token) is not { } artifact) return new(false, []); artifacts.Add(artifact); }
        var data = ImmutableArray.CreateBuilder<OutputDataSet>();
        foreach (string id in ids)
        {
            if (id == "artifacts") data.Add(ArtifactData.Create(content, artifacts));
            else if (artifacts.FirstOrDefault(item => item.StructuredData?.Id == id)?.StructuredData is { } structured) data.Add(structured);
            else return new(false, []);
        }
        return new(true, data.ToImmutable());
    }
}
internal static class ContentSamples
{
    public static byte[] Image(bool jpeg = false, int width = 64, int height = 64)
    {
        using SKBitmap bitmap = new(width, height); using SKCanvas canvas = new(bitmap); canvas.Clear(SKColors.Gold);
        using SKImage image = SKImage.FromBitmap(bitmap); using SKData data = image.Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
    public static byte[] Pdf(int pages = 1)
    {
        PdfDocumentBuilder builder = new(); var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (int i = 0; i < pages; i++) builder.AddPage(200, 200).AddText("Original synthetic PDF text", 12, new PdfPoint(10, 160), font);
        return builder.Build();
    }
}
