using System.Text.Json;
using IntentBlazor.Core;
using IntentBlazor.Docs.Documentation;

namespace IntentBlazor.Tests;

public sealed class Phase6DocumentationTests
{
    private readonly DocumentationCatalog _catalog = new();

    [Fact]
    public void Articles_ResolveEmbeddedResourcesAndCompiledSamples()
    {
        Assert.Equal(DocumentationCatalog.Guides.Length, DocumentationCatalog.Guides.Select(guide => guide.Url).Distinct().Count());
        foreach (Guide guide in DocumentationCatalog.Guides)
        {
            GuideArticle article = Assert.IsType<GuideArticle>(_catalog.Find(guide.Slug));
            Assert.Contains("<h1", article.Html);
            Assert.DoesNotContain("{{sample:", article.Html);
        }
        string quickstart = _catalog.Find("quickstart")!.Html;
        Assert.Contains("&lt;AiInput /&gt;", quickstart);
        Assert.Contains("&lt;AiOutput /&gt;", quickstart);
        Assert.Contains("AddQuickstart", quickstart);
        Assert.Contains("CatalogInputProcessor", quickstart);
        Assert.Null(_catalog.Find("../../private"));
        Assert.Throws<InvalidOperationException>(() => DocumentationCatalog.ReadResource("missing.md"));
    }

    [Theory]
    [InlineData("../phase-3-workspaces.md", "/docs/workspaces")]
    [InlineData("extensions.md#render-accessible-controls", "/docs/extensions#render-accessible-controls")]
    [InlineData("../../src/IntentBlazor.Core/OutputDocument.cs", "https://github.com/Kita72/IntentBlazor/blob/main/src/IntentBlazor.Core/OutputDocument.cs")]
    [InlineData("#example", "/docs/quickstart#example")]
    [InlineData("javascript:alert(1)", "#")]
    [InlineData("data:text/html,test", "#")]
    [InlineData("//elsewhere.example/path", "#")]
    [InlineData("https://learn.microsoft.com/aspnet/core/blazor/", "https://learn.microsoft.com/aspnet/core/blazor/")]
    public void Links_MapGuidesAndSourceWhileRejectingUnsafeSchemes(string target, string expected) =>
        Assert.Equal(expected, DocumentationCatalog.ResolveLink("docs/site/quickstart.md", target));

    [Fact]
    public void Markdown_EncodesHtmlAndPreservesNavigableHeadingsAndCode()
    {
        Guide guide = DocumentationCatalog.Guides[0];
        GuideArticle article = _catalog.Render(guide, "# Sample\n\n## Read the data\n\n<script>alert(1)</script>\n\n[unsafe](javascript:alert)\n\n~~~csharp\n<AiInput />\n~~~\n");
        Assert.DoesNotContain("<script>", article.Html);
        Assert.DoesNotContain("href=\"javascript:", article.Html);
        Assert.Contains("&lt;AiInput /&gt;", article.Html);
        Assert.Contains("tabindex=\"0\"", article.Html);
        Assert.Equal(new ArticleHeading("read-the-data", "Read the data"), Assert.Single(article.Headings));
    }

    [Fact]
    public void Reference_CoversEverySupportedNodeBindingAndEvent()
    {
        string nodes = DocumentationCatalog.ReadResource("docs/site/nodes.md");
        string bindings = DocumentationCatalog.ReadResource("docs/site/bindings.md");
        foreach (NodeKind kind in Enum.GetValues<NodeKind>()) Assert.Contains("| " + JsonNamingPolicy.CamelCase.ConvertName(kind.ToString()) + " |", nodes);
        foreach (BindingSource source in Enum.GetValues<BindingSource>()) Assert.Contains("| " + JsonNamingPolicy.CamelCase.ConvertName(source.ToString()) + " |", bindings);
        foreach (UiEventKind kind in Enum.GetValues<UiEventKind>()) Assert.Contains("| " + JsonNamingPolicy.CamelCase.ConvertName(kind.ToString()) + " |", bindings);
    }

    [Fact]
    public void Search_ReturnsMatchingTopicsAndAnEmptyResultForUnknownTopics()
    {
        Assert.Contains(_catalog.Search(" STATE "), guide => guide.Slug == "workspaces");
        Assert.Empty(_catalog.Search("unfindable-topic-xyz"));
        Assert.Equal(DocumentationCatalog.Guides.Length, _catalog.Search(null).Count());
    }
}
