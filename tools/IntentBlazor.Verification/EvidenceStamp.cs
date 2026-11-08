using System.Diagnostics;

namespace IntentBlazor.Verification;

public sealed record EvidenceStamp(string Version, string SourceRevision, string SourceFingerprint, bool Clean, DateTimeOffset CheckedAt)
{
    public string Protocol { get; init; } = global::IntentBlazor.Verification.SourceFingerprint.Protocol;
    public bool Valid => Protocol == global::IntentBlazor.Verification.SourceFingerprint.Protocol && IsHash(SourceRevision, 40) && IsHash(SourceFingerprint, 64) &&
        !string.IsNullOrWhiteSpace(Version) && CheckedAt != default;
    public bool Matches(EvidenceStamp candidate, DateTimeOffset now) => Valid && candidate.Valid &&
        Version == candidate.Version && SourceRevision == candidate.SourceRevision && SourceFingerprint == candidate.SourceFingerprint &&
        Protocol == candidate.Protocol && Clean == candidate.Clean && CheckedAt >= now.AddDays(-7) && CheckedAt <= now.AddMinutes(5);
    public static bool IsHash(string? value, int length) => value?.Length == length && value.All(char.IsAsciiHexDigit);
}

public static class CandidateEvidence
{
    public static EvidenceStamp Capture(string root, string version)
    {
        if (!Path.GetFullPath(Git(root, "rev-parse", "--show-toplevel").Trim()).Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Evidence must use the repository root.");
        return new(version, Git(root, "rev-parse", "HEAD").Trim(), SourceFingerprint.Compute(root),
            string.IsNullOrWhiteSpace(Git(root, "status", "--porcelain")), DateTimeOffset.UtcNow);
    }

    private static string Git(string root, params string[] arguments)
    {
        ProcessStartInfo start = new("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("-C"); start.ArgumentList.Add(Path.GetFullPath(root));
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Git source identity is unavailable.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10000)) { process.Kill(entireProcessTree: true); throw new InvalidOperationException("Git source identity timed out."); }
        Task.WhenAll(output, error).GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new InvalidOperationException("Git source identity is unavailable.");
        return output.GetAwaiter().GetResult();
    }
}

public sealed record HostObservation(EvidenceStamp Identity, string Origin, int StatusCode, string Version, string SourceRevision,
    string SourceFingerprint, string Mode, string Hosting, string Architecture, string Runtime, string CircuitSourceRevision, DateTimeOffset CircuitCheckedAt,
    bool InteractiveRequest, bool SelectionChanged, bool DraftRetained)
{
    public bool Matches(EvidenceStamp candidate, DateTimeOffset now, string expectedOrigin = "https://intentblazor.runasp.net/") => Identity is not null && Identity.Matches(candidate, now) &&
        Uri.TryCreate(Origin, UriKind.Absolute, out Uri? uri) && uri.Scheme == "https" && !uri.IsLoopback &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath == "/" &&
        uri.AbsoluteUri == expectedOrigin && StatusCode == 200 && Version == candidate.Version && SourceRevision == candidate.SourceRevision && SourceFingerprint == candidate.SourceFingerprint &&
        Mode == "fixture" && Hosting == "InteractiveServer" && Architecture == "X64" && Runtime?.StartsWith(".NET 10.", StringComparison.Ordinal) == true &&
        CircuitSourceRevision == SourceRevision && CircuitCheckedAt >= now.AddDays(-7) && CircuitCheckedAt <= now.AddMinutes(5) &&
        InteractiveRequest && SelectionChanged && DraftRetained;
}
