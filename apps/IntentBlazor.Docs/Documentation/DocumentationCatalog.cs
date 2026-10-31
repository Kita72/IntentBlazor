using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace IntentBlazor.Docs.Documentation;

public sealed record Guide(string Slug, string Title, string Group, string Summary, string Source)
{
    public string Url => "/docs/" + Slug;
    public string SourceUrl => DocumentationCatalog.SourceUrl(Source);
}
public sealed record ArticleHeading(string Id, string Text);
public sealed record GuideArticle(Guide Guide, string Html, ImmutableArray<ArticleHeading> Headings);

// This catalog serves only authored, embedded resources, never a user-supplied file path.
public sealed partial class DocumentationCatalog
{
    public static ImmutableArray<Guide> Guides { get; } =
    [
        new("start", "Developer journey", "Start", "How the two components fit your application.", "docs/site/start.md"),
        new("installation", "Installation", "Start", "Build from source or install locally packed artifacts.", "docs/site/installation.md"),
        new("quickstart", "Two-component quickstart", "Start", "Run a standalone app with compiled sample code.", "docs/site/quickstart.md"),
        new("services", "Expose a C# service", "Build", "Attribute and explicit registration, authorized read tools, and model connections.", "docs/site/services.md"),
        new("models", "Model connections", "Build", "Ollama, LM Studio, OpenAI, and the experimental Codex CLI adapter.", "docs/phase-2-model-connections.md"),
        new("profiles", "Provider evidence", "Build", "Implemented adapters versus observed live model results.", "docs/provider-evaluation.md"),
        new("workspaces", "State and persistence", "Build", "Ownership, history, durable restoration, actions, and conflicting saves.", "docs/phase-3-workspaces.md"),
        new("content", "Files and AI setup", "Build", "Content formats, private artifacts, image editing, setup, and inspection.", "docs/phase-4-content-and-setup.md"),
        new("extensions", "Create a renderer", "Build", "A compiled typed contract and interactive Razor renderer.", "docs/site/extensions.md"),
        new("nodes", "Output node reference", "Reference", "All node kinds, envelope members, and composition rules.", "docs/site/nodes.md"),
        new("bindings", "Bindings and events", "Reference", "Every state kind, binding source, event, and revision rule.", "docs/site/bindings.md"),
        new("errors", "Errors and recovery", "Reference", "Error codes, source locations, and preserving the last valid output.", "docs/site/errors.md"),
        new("error-codes", "Error code index", "Reference", "Generated index of framework validation, runtime, and setup failures.", "docs/site/error-codes.md"),
        new("schema-01", "Schema 0.1", "Reference", "Strict JSON, tree and data validation, and limits.", "specs/output-document/README.md"),
        new("actions", "Schema 0.2 actions", "Reference", "Registered write contracts, user confirmation, and record versions.", "specs/output-document/v0.2-actions.md"),
        new("schema-03", "Schema 0.3 extensions", "Reference", "Content nodes, typed extension configurations, and local effects.", "specs/output-document/v0.3-content-and-extensions.md"),
        new("examples", "Three example workflows", "Operate", "Run news, staff, and asset apps independently.", "docs/phase-5-example-apps.md"),
        new("hosting", "Hosting and migration", "Operate", "Session isolation, deployment boundaries, schema versions, and preview limitations.", "docs/site/hosting.md"),
        new("readiness", "Preview readiness", "Operate", "Candidate verification, package consumers, performance budgets and release gates.", "docs/phase-7-readiness.md"),
        new("publishing", "Release and deployment", "Operate", "NuGet trusted publishing and the MonsterASP IIS deployment journey.", "docs/site/releasing.md"),
        new("limitations", "Preview boundaries", "Operate", "Current support, open release gates, and deferred capabilities.", "docs/site/limitations.md"),
        new("architecture", "Architecture decisions", "Project", "Accepted design and extension boundaries.", "docs/architecture/README.md"),
        new("contributing", "Contribute", "Project", "Local checks, contract proposals, MIT terms, and maintainer workflow.", "CONTRIBUTING.md"),
        new("releases", "Release notes", "Project", "Implemented changes and unreleased status.", "CHANGELOG.md"),
        new("security", "Security policy", "Project", "Report vulnerabilities privately.", "SECURITY.md")
    ];
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables().UseAutoIdentifiers().DisableHtml().Build();
    private readonly ImmutableDictionary<string, GuideArticle> _articles;

    public DocumentationCatalog() => _articles = Guides.ToImmutableDictionary(guide => guide.Slug,
        guide => Render(guide, ReadResource(guide.Source)), StringComparer.Ordinal);

    public GuideArticle? Find(string? slug) => slug is not null ? _articles.GetValueOrDefault(slug) : null;
    public IEnumerable<Guide> Search(string? query) => string.IsNullOrWhiteSpace(query) ? Guides :
        Guides.Where(guide => (guide.Title + " " + guide.Summary).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

    public GuideArticle Render(Guide guide, string markdown)
    {
        string expanded = SamplePattern().Replace(markdown, match =>
        {
            string path = match.Groups[1].Value;
            string source = ReadResource("samples/" + path);
            string language = path.EndsWith(".razor", StringComparison.Ordinal) ? "razor" : "csharp";
            return $"\n~~~~{language}\n{source.TrimEnd()}\n~~~~\n";
        });
        MarkdownDocument document = Markdown.Parse(expanded, Pipeline);
        foreach (LinkInline link in document.Descendants<LinkInline>())
        {
            link.GetDynamicUrl = null;
            link.Url = ResolveLink(guide.Source, link.Url ?? "");
            if (link.IsImage) link.IsImage = false;
        }
        ImmutableArray<ArticleHeading> headings = document.Descendants<HeadingBlock>()
            .Where(heading => heading.Level == 2)
            .Select(heading => new ArticleHeading(heading.GetAttributes().Id ?? "", string.Concat(
                heading.Inline?.Descendants().Select(inline => inline switch
                {
                    LiteralInline literal => literal.Content.ToString(),
                    CodeInline code => code.Content,
                    _ => ""
                }) ?? [])))
            .ToImmutableArray();
        string html = document.ToHtml(Pipeline)
            .Replace("<pre>", "<pre tabindex=\"0\" aria-label=\"Code sample\">", StringComparison.Ordinal)
            .Replace("<table>", "<div class=\"table-wrap\" tabindex=\"0\" role=\"region\" aria-label=\"Scrollable reference table\"><table>", StringComparison.Ordinal)
            .Replace("</table>", "</table></div>", StringComparison.Ordinal);
        return new(guide, html, headings);
    }

    public static string ResolveLink(string source, string target)
    {
        if (string.IsNullOrEmpty(target) || target.Any(char.IsControl) || target.Contains('\\') || target.StartsWith("//", StringComparison.Ordinal)) return "#";
        if (target.StartsWith('#')) return (Guides.FirstOrDefault(guide => guide.Source == source)?.Url ?? SourceUrl(source)) + target;
        if (Uri.TryCreate(target, UriKind.Absolute, out Uri? absolute))
            return absolute.Scheme == "https" || (absolute.Scheme == "http" && absolute.IsLoopback) ? absolute.AbsoluteUri : "#";
        if (!Uri.TryCreate(new Uri("https://docs.invalid/" + source), target, out Uri? resolved) || resolved.Query.Length != 0) return "#";
        string path = Uri.UnescapeDataString(resolved.AbsolutePath.TrimStart('/'));
        Guide? guide = Guides.FirstOrDefault(item => item.Source == path);
        return (guide?.Url ?? SourceUrl(path)) + resolved.Fragment;
    }

    public static string SourceUrl(string path) => "https://github.com/Kita72/IntentBlazor/blob/main/" +
        string.Join('/', path.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));

    public static string ReadResource(string path)
    {
        Assembly assembly = typeof(DocumentationCatalog).Assembly;
        string? name = assembly.GetManifestResourceNames().FirstOrDefault(candidate => candidate.Replace('\\', '/') == path);
        using Stream stream = name is not null ? assembly.GetManifestResourceStream(name)! :
            throw new InvalidOperationException($"Missing embedded documentation: {path}");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"\{\{sample: ([A-Za-z0-9_/.]+)\}\}")]
    private static partial Regex SamplePattern();
}
