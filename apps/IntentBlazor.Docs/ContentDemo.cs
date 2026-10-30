using System.Collections.Immutable;
using IntentBlazor.AI;
using IntentBlazor.Content;
using IntentBlazor.Core;
using IntentBlazor.Examples;
using IntentBlazor.Packs.ImageEditor;
using IntentBlazor.Runtime;
using SkiaSharp;

namespace IntentBlazor.Docs;

public sealed class DemoAiAccess(IWorkspaceIdentity identity) : IAiSetupAccess
{
    public bool Restricted { get; set; }
    public async ValueTask<AiSetupPolicy> GetPolicyAsync(CancellationToken cancellationToken)
    {
        bool signedIn = (await identity.GetOwnerIdAsync(cancellationToken))?.StartsWith("demo-", StringComparison.Ordinal) == true;
        return new(CanView: signedIn, CanEdit: signedIn && !Restricted, CanProbe: signedIn && !Restricted, CanInspect: signedIn, AllowAdvanced: !Restricted);
    }
}
public sealed class DemoWorkspaceAccess(StaffWorkspaces staff, IWorkspaceContent content) : IWorkspaceAccess
{
    public async ValueTask<WorkspaceAccess> ReauthorizeAsync(string ownerId, ImmutableArray<string> dataSets,
        ImmutableArray<ArtifactReference> artifacts, CancellationToken cancellationToken)
    {
        if (!ownerId.StartsWith("demo-", StringComparison.Ordinal)) return new(false, []);
        var retained = new List<ContentArtifact>();
        foreach (ArtifactReference reference in artifacts)
        {
            if (reference == new ArtifactReference("asset-2", "image/svg+xml", "sample-1")) continue;
            ContentArtifact? artifact = await content.DescribeAsync(reference, cancellationToken);
            if (artifact is null) return new(false, []); retained.Add(artifact);
        }
        var data = ImmutableArray.CreateBuilder<OutputDataSet>();
        string[] domainIds = dataSets.Where(id => id is "staff" or "jobs").ToArray();
        if (domainIds.Length > 0)
        {
            WorkspaceAccess domain = await staff.ReauthorizeAsync(ownerId, [.. domainIds], [], cancellationToken);
            if (!domain.Allowed) return new(false, []); data.AddRange(domain.Data);
        }
        foreach (string id in dataSets.Except(domainIds))
        {
            if (id == "artifacts") data.Add(ArtifactData.Create(content, retained));
            else if (retained.FirstOrDefault(item => item.StructuredData?.Id == id)?.StructuredData is { } structured) data.Add(structured);
            else return new(false, []);
        }
        return new(true, data.ToImmutable());
    }
}

public sealed class ContentWorkspaceDemo(IWorkspaceContent content, WorkspaceContent operations, DocumentValidator validator,
    WorkspaceServices services) : IInputProcessor, IDisposable
{
    public AiSession Workspace { get; } = new(validator, new ContentProcessor(content), services);
    public async Task AddSampleAsync()
    {
        using SKBitmap bitmap = new(64, 64); using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.Transparent);
        using SKPaint paint = new() { Color = SKColors.DarkCyan }; canvas.DrawRect(8, 8, 48, 48, paint);
        paint.Color = SKColors.Gold; canvas.DrawRect(16, 16, 32, 32, paint);
        using SKImage image = SKImage.FromBitmap(bitmap); using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        byte[] bytes = encoded.ToArray(); using MemoryStream source = new(bytes);
        ContentArtifact artifact = await content.ImportAsync(new("intent-sample.png", "image/png", bytes.Length), source);
        Workspace.SetArtifacts(Workspace.Artifacts.Add(artifact.Reference)); await RefreshAsync();
    }
    public async Task RefreshAsync()
    {
        OutputContent next = await new ContentProcessor(content).ProcessAsync("Inspect the attached content", Workspace.Snapshot, CancellationToken.None);
        Workspace.ApplyUpdate(next, Workspace.Revision);
    }
    public async Task ExportAsync(string format)
    {
        OutputDataSet data = Workspace.Data.Single(set => set.Id == "artifacts");
        ContentArtifact export = await operations.ExportAsync(data, format, "attachment-metadata");
        Workspace.SetArtifacts(Workspace.Artifacts.Add(export.Reference)); await RefreshAsync();
    }
    public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken token) => new ContentProcessor(content).ProcessAsync(input, workspace, token);
    public void Dispose() => Workspace.Dispose();
}

public sealed class ContentProcessor(IWorkspaceContent content) : IInputProcessor
{
    public async ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken cancellationToken)
    {
        List<ContentArtifact> artifacts = [];
        foreach (ArtifactReference reference in workspace.Artifacts)
            if (await content.DescribeAsync(reference, cancellationToken) is { } artifact) artifacts.Add(artifact);
        OutputDataSet data = ArtifactData.Create(content, artifacts);
        return ImageEditorDocuments.Create(data, "Deterministic content view: " + input);
    }
}
