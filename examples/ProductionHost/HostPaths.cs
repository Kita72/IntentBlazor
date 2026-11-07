namespace IntentBlazor.ProductionHost;

public sealed record HostPaths(string Root)
{
    public string Workspaces => Path.Combine(Root, "workspaces.sqlite");
    public string Business => Path.Combine(Root, "business.sqlite");
    public string Usage => Path.Combine(Root, "usage.sqlite");
    public string Artifacts => Path.Combine(Root, "artifacts");
    public string Keys => Path.Combine(Root, "keys");

    public static HostPaths Create(string path, string contentRoot)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Host storage must use an absolute private directory.");
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string content = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentRoot));
        if (root == Path.GetPathRoot(root) || Inside(root, content) || Inside(content, root))
            throw new ArgumentException("Use a private storage directory separate from the application/content tree.");
        for (DirectoryInfo? current = new(root); current is not null; current = current.Parent)
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new ArgumentException("Private storage must not traverse linked directories.");
        Directory.CreateDirectory(root);
        return new(root);
    }

    private static bool Inside(string candidate, string parent) => candidate.Equals(parent, Comparison) ||
        candidate.StartsWith(parent + Path.DirectorySeparatorChar, Comparison);
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
