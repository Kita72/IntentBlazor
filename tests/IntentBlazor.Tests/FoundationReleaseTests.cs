using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using IntentBlazor.Verification;

namespace IntentBlazor.Tests;

public sealed class FoundationReleaseTests
{
    [Fact]
    public void PublicPackageComparison_AllowsRepositorySigningAndZipOrderWithoutChangingPayload()
    {
        using OperationScratch scratch = new();
        string original = Package(scratch, "original.nupkg", [("lib/net10.0/Example.dll", "assembly"), ("Example.nuspec", "metadata")]);
        string signed = Package(scratch, "signed.nupkg", [("Example.nuspec", "metadata"), (".signature.p7s", "repository signature"), ("lib/net10.0/Example.dll", "assembly")]);
        Assert.NotEqual(PackageAudit.FileHash(original), PackageAudit.FileHash(signed));
        Assert.Equal(PackagePayload.Hash(original), PackagePayload.Hash(signed));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("extra")]
    [InlineData("missing")]
    public void PublicPackageComparison_RejectsChangedExtraOrMissingPayload(string fault)
    {
        using OperationScratch scratch = new();
        string original = Package(scratch, "original.nupkg", [("Example.nuspec", "metadata"), ("Example.dll", "assembly")]);
        List<(string, string)> files = [("Example.nuspec", "metadata"), ("Example.dll", fault == "changed" ? "changed assembly" : "assembly")];
        if (fault == "extra") files.Add(("unexpected.dll", "additional code"));
        if (fault == "missing") files.RemoveAt(1);
        string changed = Package(scratch, "changed.nupkg", files);
        Assert.NotEqual(PackagePayload.Hash(original), PackagePayload.Hash(changed));
    }

    [Theory]
    [InlineData("traversal")]
    [InlineData("case-collision")]
    [InlineData("empty")]
    public void PublicPackageComparison_RejectsUnsafeAmbiguousOrSignatureOnlyArchives(string fault)
    {
        using OperationScratch scratch = new();
        (string, string)[] entries = fault switch
        {
            "traversal" => [("../outside", "payload")],
            "case-collision" => [("Example.dll", "first"), ("EXAMPLE.dll", "second")],
            _ => [(".signature.p7s", "only a signature")]
        };
        string package = Package(scratch, "malformed.nupkg", entries);
        Assert.Throws<InvalidDataException>(() => PackagePayload.Hash(package));
    }

    [Fact]
    public async Task PublicConsumerConfiguration_ExcludesLocalSourcesAndFallbackFolders()
    {
        using OperationScratch scratch = new(); string config = Path.Combine(scratch.Path, "NuGet.Config");
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "IntentBlazor.slnx"))) root = root.Parent;
        string script = Path.Combine(root!.FullName, "scripts/Repository.ps1");
        ProcessStartInfo start = new("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string argument in new[] { "-NoProfile", "-Command", ". '" + script.Replace("'", "''", StringComparison.Ordinal) + "'; Write-IntentPackageSources -Path '" + config.Replace("'", "''", StringComparison.Ordinal) + "' -PublicFeed" }) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); await Task.WhenAll(output, error);
        Assert.True(process.ExitCode == 0, await error);
        XDocument actual = XDocument.Load(config);
        XElement source = Assert.Single(actual.Descendants("packageSources").Elements("add"));
        Assert.Equal("nuget.org", source.Attribute("key")?.Value); Assert.Equal("https://api.nuget.org/v3/index.json", source.Attribute("value")?.Value);
        Assert.Single(actual.Descendants("packageSources").Elements("clear")); Assert.Single(actual.Descendants("fallbackPackageFolders").Elements("clear"));
        Assert.Equal("nuget.org", Assert.Single(actual.Descendants("packageSourceMapping").Elements("packageSource")).Attribute("key")?.Value);
    }

    private static string Package(OperationScratch scratch, string name, IEnumerable<(string Path, string Content)> entries)
    {
        string path = Path.Combine(scratch.Path, name);
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries) { using StreamWriter writer = new(zip.CreateEntry(entry.Path).Open()); writer.Write(entry.Content); }
        return path;
    }
}
