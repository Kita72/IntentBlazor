using IntentBlazor.HostSmoke;
using IntentBlazor.Verification;

namespace IntentBlazor.BrowserTests;

public sealed class PublishedDocsBrowserTests(BrowserHarness browser) : IClassFixture<BrowserHarness>
{
    [Fact]
    public async Task IisBundle_HealthCircuitAndRollbackPreserveUnmanagedData()
    {
        string published = File.ReadAllText(Path.Combine(BrowserHarness.Root, "artifacts/phase7-docs-path.txt")).Trim();
        DeploymentManifest source = DeploymentBundle.Validate(published);
        Assert.Contains(source.Files, file => file.Path.Contains("SkiaSharp.NativeAssets.Win32/", StringComparison.OrdinalIgnoreCase) && file.Path.EndsWith("THIRD-PARTY-NOTICES.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(source.Files, file => file.Path == "IntentBlazor.Docs.pdb");
        string root = Path.Combine(Path.GetTempPath(), "IntentBlazor-Deployment-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string old = Path.Combine(root, "old"), next = Path.Combine(root, "next"), site = Path.Combine(root, "site");
            foreach (string bundle in new[] { old, next })
            {
                foreach (string path in source.Files.Select(file => file.Path).Append(DeploymentBundle.ManifestName))
                {
                    string target = Path.Combine(bundle, path); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(Path.Combine(published, path), target);
                }
            }
            File.WriteAllText(Path.Combine(old, "bundle-test-version.txt"), "old"); File.WriteAllText(Path.Combine(old, "obsolete-test-file.txt"), "obsolete managed file");
            File.WriteAllText(Path.Combine(next, "bundle-test-version.txt"), "next"); File.WriteAllText(Path.Combine(next, "added-test-file.txt"), "new managed file");
            DeploymentManifest previous = DeploymentBundle.Seal(old, source.Identity), incoming = DeploymentBundle.Seal(next, source.Identity);
            Directory.CreateDirectory(Path.Combine(site, "App_Data")); File.WriteAllText(Path.Combine(site, ".intentblazor-offline-fixture"), "owned local fixture");
            File.WriteAllText(Path.Combine(site, "App_Data/private.db"), "synthetic private database"); File.WriteAllText(Path.Combine(site, "host-owned.txt"), "keep this host file");
            List<HostObservation> observations = [];
            DeploymentBundle.ApplyRehearsal(old, site); await SmokeAsync("before-upgrade", previous);
            DeploymentPlan upgrade = DeploymentBundle.ApplyRehearsal(next, site);
            Assert.Contains("obsolete-test-file.txt", upgrade.Deletes); Assert.False(File.Exists(Path.Combine(site, "obsolete-test-file.txt")));
            await SmokeAsync("after-upgrade", incoming);
            DeploymentPlan rollback = DeploymentBundle.ApplyRehearsal(old, site);
            Assert.Contains("added-test-file.txt", rollback.Deletes); Assert.False(File.Exists(Path.Combine(site, "added-test-file.txt")));
            Assert.Equal("old", File.ReadAllText(Path.Combine(site, "bundle-test-version.txt"))); await SmokeAsync("after-rollback", previous);
            Assert.Equal("synthetic private database", File.ReadAllText(Path.Combine(site, "App_Data/private.db")));
            Assert.Equal("keep this host file", File.ReadAllText(Path.Combine(site, "host-owned.txt")));
            VerificationJson.Write(Path.Combine(BrowserHarness.Root, "artifacts/step5/deployment-rehearsal.json"), new
            {
                source.Identity,
                mode = "offline-loopback-kestrel-not-iis",
                bundleHash = source.BundleHash,
                bytes = source.TotalBytes,
                source.RuntimeIdentifier,
                nativeCheck = "Publish-Docs --verify-offline-publish",
                upgrade,
                rollback,
                observations
            });
            async Task SmokeAsync(string stage, DeploymentManifest expected) => await browser.RunAsync(nameof(IisBundle_HealthCircuitAndRollbackPreserveUnmanagedData) + "-" + stage, "docs", async test =>
            {
                HostObservation observation = await DocsHostSmoke.CaptureAsync(test.Page, test.Server.Url + "/", expected.Identity);
                Assert.True(observation.InteractiveRequest && observation.SelectionChanged && observation.DraftRetained);
                Assert.False(observation.Matches(expected.Identity, DateTimeOffset.UtcNow)); // Loopback never certifies the public HTTPS host.
                observations.Add(observation);
            }, site);
        }
        finally
        {
            if (Path.GetDirectoryName(root) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) || !Path.GetFileName(root).StartsWith("IntentBlazor-Deployment-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected deployment fixture root.");
            Directory.Delete(root, recursive: true);
        }
    }
}
