using System.Diagnostics;
using System.Text.Json;
using IntentBlazor.Verification;

namespace IntentBlazor.Tests;

public sealed class DeploymentScriptTests
{
    [Fact]
    public async Task DeployWhatIf_ValidatesBundleWithoutReadingCredentialsOrStartingWebDeploy()
    {
        using OperationScratch bundle = DeploymentBundleTests.Bundle(); DeploymentBundle.Seal(bundle.Path, FoundationEvidenceTests.Candidate);
        using OperationScratch files = new(); string missingProfile = Path.Combine(files.Path, "must-not-be-opened.publishSettings");
        var result = await RunAsync("pwsh", ["-NoProfile", "-File", Path.Combine(Root, "scripts/Deploy-Docs.ps1"), "-PublishDirectory", bundle.Path,
            "-PublishSettingsPath", missingProfile, "-ExpectedSiteHost", "intentblazor.runasp.net", "-MSDeployPath", Path.Combine(files.Path, "must-not-be-started.exe"), "-WhatIf"]);
        Assert.True(result.Exit == 0, result.Output); Assert.Contains("Bundle verified:", result.Output); Assert.Contains("What if:", result.Output);
        Assert.False(File.Exists(missingProfile));
    }

    [Fact]
    public async Task DeployWhatIf_RejectsTamperingBeforeOpeningTheProfile()
    {
        using OperationScratch bundle = DeploymentBundleTests.Bundle(); DeploymentBundle.Seal(bundle.Path, FoundationEvidenceTests.Candidate);
        File.AppendAllText(Path.Combine(bundle.Path, "IntentBlazor.Docs.dll"), "tampered");
        var result = await RunAsync("pwsh", ["-NoProfile", "-File", Path.Combine(Root, "scripts/Deploy-Docs.ps1"), "-PublishDirectory", bundle.Path,
            "-PublishSettingsPath", Path.Combine(bundle.Path, "absent.publishSettings"), "-ExpectedSiteHost", "intentblazor.runasp.net", "-WhatIf"]);
        Assert.NotEqual(0, result.Exit); Assert.Contains("InvalidDataException", result.Output); Assert.DoesNotContain("What if:", result.Output);
    }

    [Fact]
    public async Task Readiness_EnvironmentBooleanCannotVerifyThePublicHost()
    {
        using OperationScratch scratch = new(); string report = Path.Combine(scratch.Path, "readiness.json");
        string framework = Path.GetFileName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var result = await RunAsync("dotnet", [Path.Combine(Root, "tools/IntentBlazor.Verification/bin/Release", framework, "IntentBlazor.Verification.dll"),
            "readiness", Root, FoundationEvidenceTests.Candidate.Version, scratch.Path, report], ("INTENT_PUBLIC_HOST_VERIFIED", "true"));
        Assert.Equal(0, result.Exit);
        ReadinessReport actual = JsonSerializer.Deserialize<ReadinessReport>(File.ReadAllText(report), VerificationJson.Options)!;
        Assert.False(actual.Ready); Assert.False(Assert.Single(actual.Gates, gate => gate.Gate == "public-host").Passed);
    }

    [Fact]
    public async Task HostRecorder_RequiresExplicitOptInBeforeAnyNetworkOrBrowserWork()
    {
        var result = await RunAsync("pwsh", ["-NoProfile", "-File", Path.Combine(Root, "scripts/Record-HostEvidence.ps1")]);
        Assert.NotEqual(0, result.Exit); Assert.Contains("Public network verification is separate from offline tests", result.Output);
    }

    private static string Root
    {
        get
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IntentBlazor.slnx"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }
    private static async Task<(int Exit, string Output)> RunAsync(string command, string[] arguments, (string Name, string Value)? environment = null)
    {
        ProcessStartInfo start = new(command) { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        if (environment is { } variable) start.Environment[variable.Name] = variable.Value;
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); }
        catch (TimeoutException) { process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await output + await error);
    }
}
