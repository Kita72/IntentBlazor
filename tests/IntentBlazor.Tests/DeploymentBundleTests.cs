using System.Collections.Immutable;
using System.Text;
using IntentBlazor.Verification;

namespace IntentBlazor.Tests;

public sealed class DeploymentBundleTests
{
    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute")]
    [InlineData("C:/absolute")]
    [InlineData("one\\two")]
    [InlineData("one//two")]
    [InlineData("one/./two")]
    [InlineData("one/COM9.txt")]
    [InlineData("LPT2")]
    [InlineData("file.")]
    [InlineData("file ")]
    [InlineData("file:stream")]
    [InlineData("file?.txt")]
    public void Manifest_RejectsUnsafeWindowsManagedPaths(string path)
    {
        using OperationScratch bundle = Bundle(); DeploymentManifest original = DeploymentBundle.Seal(bundle.Path, FoundationEvidenceTests.Candidate);
        ImmutableArray<ManagedFile> files = [.. original.Files, new(path, 1, new('A', 64))];
        Assert.Throws<InvalidDataException>(() => DeploymentBundle.ValidateManifest(original with { Files = files, BundleHash = DeploymentBundle.InventoryHash(files) }));
    }

    [Theory]
    [InlineData("App_Data/private.db")]
    [InlineData("keys/key.xml")]
    [InlineData("appsettings.Local.json")]
    [InlineData(".env.production")]
    [InlineData("site.publishSettings")]
    [InlineData("identity.pfx")]
    public void Manifest_RejectsPrivateFilesEvenWithValidHashes(string path)
    {
        using OperationScratch bundle = Bundle(); bundle.Write(path, "synthetic private data");
        Assert.Throws<InvalidDataException>(() => DeploymentBundle.Seal(bundle.Path, FoundationEvidenceTests.Candidate));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("legacy")]
    [InlineData("case")]
    [InlineData("architecture")]
    public void BundleValidation_RejectsChangedIncompleteOrAmbiguousPayload(string fault)
    {
        using OperationScratch bundle = Bundle(); DeploymentManifest manifest = DeploymentBundle.Seal(bundle.Path, FoundationEvidenceTests.Candidate);
        switch (fault)
        {
            case "changed": bundle.Write("IntentBlazor.Docs.dll", "different binary"); break;
            case "missing": File.Delete(System.IO.Path.Combine(bundle.Path, "web.config")); break;
            case "extra": bundle.Write("unlisted.txt", "unlisted"); break;
            case "legacy": VerificationJson.Write(System.IO.Path.Combine(bundle.Path, DeploymentBundle.ManifestName), manifest with { FormatVersion = "1" }); break;
            case "case":
                ImmutableArray<ManagedFile> files = [.. manifest.Files, manifest.Files[0] with { Path = manifest.Files[0].Path.ToUpperInvariant() }];
                VerificationJson.Write(System.IO.Path.Combine(bundle.Path, DeploymentBundle.ManifestName), manifest with { Files = files, BundleHash = DeploymentBundle.InventoryHash(files) }); break;
            case "architecture": Native(bundle, 0x14c); break;
        }
        Assert.Throws<InvalidDataException>(() => DeploymentBundle.Validate(bundle.Path));
    }

    [Fact]
    public void Seal_RemovesOnlyRecognizedNativeSymbolsAndRetainsPortableSymbols()
    {
        using OperationScratch bundle = Bundle(); bundle.Write("libSkiaSharp.pdb", "Microsoft C/C++ MSF 7.00\r\n" + new string('x', 64));
        string portable = bundle.Write("IntentBlazor.Docs.pdb", "portable symbol bytes");
        DeploymentManifest manifest = DeploymentBundle.Seal(bundle.Path, FoundationEvidenceTests.Candidate);
        Assert.False(File.Exists(System.IO.Path.Combine(bundle.Path, "libSkiaSharp.pdb"))); Assert.True(File.Exists(portable));
        Assert.Contains(manifest.Files, file => file.Path == "IntentBlazor.Docs.pdb");
        bundle.Write("libSkiaSharp.pdb", new string('x', 64));
        Assert.Throws<InvalidDataException>(() => DeploymentBundle.Seal(bundle.Path, FoundationEvidenceTests.Candidate));
        Assert.True(File.Exists(System.IO.Path.Combine(bundle.Path, "libSkiaSharp.pdb")));
    }

    [Fact]
    public void Rehearsal_UpgradesAndRollsBackOnlyUnchangedManagedFilesPreservingPrivateData()
    {
        using OperationScratch old = Bundle(); old.Write("wwwroot/version.txt", "old"); old.Write("wwwroot/obsolete.txt", "old owned file");
        DeploymentManifest previous = DeploymentBundle.Seal(old.Path, FoundationEvidenceTests.Candidate);
        using OperationScratch next = Bundle(); next.Write("wwwroot/version.txt", "new"); next.Write("wwwroot/added.txt", "new owned file");
        DeploymentManifest incoming = DeploymentBundle.Seal(next.Path, FoundationEvidenceTests.Candidate);
        using OperationScratch site = new(); site.Write(".intentblazor-offline-fixture", "owned local fixture");
        site.Write("App_Data/private.db", "private database"); site.Write("appsettings.Local.json", "private configuration"); site.Write("wwwroot/custom.txt", "host content");
        DeploymentBundle.ApplyRehearsal(old.Path, site.Path);
        DeploymentPlan upgrade = DeploymentBundle.ApplyRehearsal(next.Path, site.Path);
        Assert.Equal(previous.BundleHash, upgrade.PreviousBundleHash); Assert.Equal(incoming.BundleHash, upgrade.NextBundleHash);
        Assert.Equal(new[] { "wwwroot/obsolete.txt" }, upgrade.Deletes);
        Assert.Equal("new", File.ReadAllText(System.IO.Path.Combine(site.Path, "wwwroot/version.txt")));
        Assert.False(File.Exists(System.IO.Path.Combine(site.Path, "wwwroot/obsolete.txt")));
        Assert.Contains("App_Data/private.db", upgrade.Preserves);
        DeploymentPlan rollback = DeploymentBundle.ApplyRehearsal(old.Path, site.Path);
        Assert.Equal(new[] { "wwwroot/added.txt" }, rollback.Deletes);
        Assert.Equal("old", File.ReadAllText(System.IO.Path.Combine(site.Path, "wwwroot/version.txt")));
        Assert.True(File.Exists(System.IO.Path.Combine(site.Path, "wwwroot/obsolete.txt")));
        Assert.Equal("private database", File.ReadAllText(System.IO.Path.Combine(site.Path, "App_Data/private.db")));
        Assert.Equal("private configuration", File.ReadAllText(System.IO.Path.Combine(site.Path, "appsettings.Local.json")));
        Assert.Equal("host content", File.ReadAllText(System.IO.Path.Combine(site.Path, "wwwroot/custom.txt")));
    }

    [Theory]
    [InlineData("modified")]
    [InlineData("obsolete-modified")]
    [InlineData("collision")]
    [InlineData("directory")]
    [InlineData("parent-file")]
    public void Rehearsal_RejectsConflictsBeforeAnyWriteOrDelete(string fault)
    {
        using OperationScratch old = Bundle(); old.Write("obsolete.txt", "owned old file"); DeploymentBundle.Seal(old.Path, FoundationEvidenceTests.Candidate);
        using OperationScratch next = Bundle(); next.Write("incoming/file.txt", "new content"); DeploymentBundle.Seal(next.Path, FoundationEvidenceTests.Candidate);
        using OperationScratch site = new(); site.Write(".intentblazor-offline-fixture", "owned local fixture"); DeploymentBundle.ApplyRehearsal(old.Path, site.Path);
        switch (fault)
        {
            case "modified": site.Write("IntentBlazor.Docs.dll", "host modification"); break;
            case "obsolete-modified": site.Write("obsolete.txt", "host modification"); break;
            case "collision": site.Write("incoming/file.txt", "unmanaged collision"); break;
            case "directory": Directory.CreateDirectory(System.IO.Path.Combine(site.Path, "incoming/file.txt")); break;
            case "parent-file": site.Write("incoming", "unmanaged file"); break;
        }
        Dictionary<string, string> before = Directory.GetFiles(site.Path, "*", SearchOption.AllDirectories).ToDictionary(path => path, PackageAudit.FileHash);
        Assert.Throws<InvalidDataException>(() => DeploymentBundle.ApplyRehearsal(next.Path, site.Path));
        Assert.Equal(before.Count, Directory.GetFiles(site.Path, "*", SearchOption.AllDirectories).Length);
        Assert.All(before, pair => Assert.Equal(pair.Value, PackageAudit.FileHash(pair.Key)));
    }

    [Fact]
    public void Rehearsal_RequiresOwnedMarkerAndNonOverlappingRoots()
    {
        using OperationScratch bundle = Bundle(); DeploymentBundle.Seal(bundle.Path, FoundationEvidenceTests.Candidate);
        using OperationScratch site = new();
        Assert.Throws<InvalidOperationException>(() => DeploymentBundle.ApplyRehearsal(bundle.Path, site.Path));
        Assert.Throws<InvalidDataException>(() => DeploymentBundle.Plan(bundle.Path, bundle.Path));
        Assert.Empty(Directory.GetFiles(site.Path));
    }

    internal static OperationScratch Bundle()
    {
        OperationScratch scratch = new();
        scratch.Write("web.config", """<configuration><system.webServer><aspNetCore processPath="dotnet" arguments=".\IntentBlazor.Docs.dll" hostingModel="inprocess"><environmentVariables><environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" /></environmentVariables></aspNetCore></system.webServer></configuration>""");
        scratch.Write("IntentBlazor.Docs.runtimeconfig.json", """{"runtimeOptions":{"tfm":"net10.0"}}""");
        scratch.Write("IntentBlazor.Docs.deps.json", "{}"); scratch.Write("IntentBlazor.Docs.dll", "synthetic managed assembly");
        scratch.Write("Microsoft.Data.Sqlite.dll", "legitimate dependency"); Native(scratch, 0x8664); return scratch;
    }
    private static void Native(OperationScratch scratch, ushort machine)
    {
        byte[] bytes = new byte[128]; using MemoryStream memory = new(bytes); using BinaryWriter writer = new(memory, Encoding.ASCII);
        writer.Write((ushort)0x5A4D); memory.Position = 0x3c; writer.Write(64); memory.Position = 64; writer.Write(0x00004550); writer.Write(machine);
        File.WriteAllBytes(System.IO.Path.Combine(scratch.Path, "libSkiaSharp.dll"), bytes);
    }
}
