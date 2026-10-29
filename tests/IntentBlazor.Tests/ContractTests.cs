using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IntentBlazor.Tests;

public sealed class ContractTests
{
    private readonly DocumentValidator _validator = new();

    [Theory]
    [InlineData("news", "news-reader", 7)]
    [InlineData("staff", "staff-followups", 9)]
    [InlineData("assets", "asset-gallery", 8)]
    public void AuthoredFixtures_RoundTripWithSeparateHostData(string name, string id, int nodes)
    {
        OutputContent original = TestContent.Catalog.Get(name);
        OutputContent restored = OutputJson.ReadContent(OutputJson.SerializeContent(original));

        Assert.True(_validator.Validate(restored).Success);
        Assert.Equal(id, restored.Document.Id);
        Assert.Equal(nodes, restored.Document.Nodes.Length);
        Assert.Equal(2, restored.Data[0].Rows.Length);
        Assert.Equal(original.Data[0].Rows[0]["id"].GetString(), restored.Data[0].Rows[0]["id"].GetString());
        Assert.DoesNotContain("Fictional fixture:", OutputJson.SerializeDocument(restored.Document));
        Assert.Contains("dataSets", OutputJson.SerializeDocument(restored.Document));
    }

    [Theory]
    [InlineData("unknown-property")]
    [InlineData("duplicate-property")]
    [InlineData("integer-kind")]
    [InlineData("unknown-kind")]
    [InlineData("null-document")]
    [InlineData("excess-depth")]
    public void ReadDocument_RejectsAmbiguousOrUnsupportedJson(string caseId)
    {
        string valid = OutputJson.SerializeDocument(TestContent.Catalog.Get("news").Document);
        JsonNode root = JsonNode.Parse(valid)!;
        string invalid;
        switch (caseId)
        {
            case "unknown-property": root["executableCode"] = "unsupported"; invalid = root.ToJsonString(); break;
            case "duplicate-property": invalid = valid.Replace("\"schemaVersion\": \"0.1\"", "\"schemaVersion\":\"0.1\",\"schemaVersion\":\"0.1\""); break;
            case "integer-kind": root["nodes"]![0]!["kind"] = 99; invalid = root.ToJsonString(); break;
            case "unknown-kind": root["nodes"]![0]!["kind"] = "unregisteredCanvas"; invalid = root.ToJsonString(); break;
            case "null-document": invalid = "null"; break;
            default: invalid = string.Concat(Enumerable.Repeat("{\"nested\":", 40)) + "0" + new string('}', 40); break;
        }
        Assert.ThrowsAny<JsonException>(() => OutputJson.ReadDocument(invalid));
    }

    [Fact]
    public void ReadDocument_EnforcesExactUtf8ByteBoundary()
    {
        string json = OutputJson.SerializeDocument(TestContent.Catalog.Get("news").Document);
        string atLimit = json + new string(' ', OutputJson.MaximumBytes - Encoding.UTF8.GetByteCount(json));
        Assert.Equal("news-reader", OutputJson.ReadDocument(atLimit).Id);
        Assert.Throws<JsonException>(() => OutputJson.ReadDocument(atLimit + " "));
    }

    [Theory]
    [InlineData("schema", "schema-version")]
    [InlineData("toolkit", "toolkit")]
    [InlineData("duplicate-node", "node-identity")]
    [InlineData("root", "root")]
    [InlineData("child", "child-reference")]
    [InlineData("cycle", "node-cycle")]
    [InlineData("unreachable", "unreachable-node")]
    [InlineData("state-type", "state-value")]
    [InlineData("record-reference", "state-value")]
    [InlineData("dataset", "dataset-reference")]
    [InlineData("duplicate-row", "duplicate-key")]
    [InlineData("field", "binding-field")]
    [InlineData("state-binding", "binding-state")]
    [InlineData("action", "unsupported-action")]
    [InlineData("event-kind", "event-kind")]
    [InlineData("empty-effect", "empty-effect")]
    [InlineData("leaf-children", "leaf-children")]
    public void Validate_RejectsInvalidGraphsDataAndCapabilities(string caseId, string code)
    {
        OutputContent content = TestContent.Catalog.Get("news");
        OutputDocument doc = content.Document;
        content = caseId switch
        {
            "schema" => content with { Document = doc with { SchemaVersion = "99" } },
            "toolkit" => content with { Document = doc with { Requires = doc.Requires.Add("custom", "1") } },
            "duplicate-node" => content with { Document = doc with { Nodes = doc.Nodes.Add(doc.Nodes[0]) } },
            "root" => content with { Document = doc with { RootId = "absent" } },
            "child" => TestContent.ChangeNode(content, "root", node => node with { Children = ["absent"] }),
            "cycle" => TestContent.ChangeNode(content, "root", node => node with { Children = ["root"] }),
            "unreachable" => content with { Document = doc with { Nodes = doc.Nodes.Add(new() { Id = "orphan", Kind = NodeKind.Text, Value = ValueBinding.Constant("orphan") }) } },
            "state-type" => content with { Document = doc with { State = doc.State.SetItem("selectedArticle", new() { Kind = StateKind.Number, InitialValue = JsonValues.From("wrong") }) } },
            "record-reference" => content with { Document = doc with { State = doc.State.SetItem("selectedArticle", doc.State["selectedArticle"] with { InitialValue = JsonValues.From("absent") }) } },
            "dataset" => content with { Document = doc with { DataSets = ["missing"] } },
            "duplicate-row" => TestContent.WithRows(content, content.Data[0].Rows.Add(content.Data[0].Rows[0])),
            "field" => TestContent.ChangeNode(content, "body", node => node with { Value = ValueBinding.Selected("selectedArticle", "articles", "privateField") }),
            "state-binding" => TestContent.ChangeNode(content, "body", node => node with { Value = ValueBinding.FromState("missing") }),
            "action" => content with { Document = doc with { Interactions = [doc.Interactions[0] with { Action = "Jobs.Save" }] } },
            "event-kind" => content with { Document = doc with { Interactions = [doc.Interactions[0] with { Event = UiEventKind.Activate }] } },
            "empty-effect" => content with { Document = doc with { Interactions = [doc.Interactions[0] with { Assignments = [] }] } },
            _ => TestContent.ChangeNode(content, "body", node => node with { Children = ["source"] })
        };
        OperationResult result = _validator.Validate(content);
        Assert.False(result.Success);
        Assert.Equal(code, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void Validate_BoundsGraphDepth(int depth, bool accepted)
    {
        OperationResult result = _validator.Validate(TestContent.Tree(depth));
        Assert.Equal(accepted, result.Success);
        if (!accepted) Assert.Equal("depth-limit", Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData(250, true)]
    [InlineData(251, false)]
    public void Validate_BoundsDataRows(int count, bool accepted)
    {
        OutputContent content = TestContent.Catalog.Get("news");
        var rows = Enumerable.Range(0, count).Select(index =>
            content.Data[0].Rows[0].SetItem("id", JsonValues.From($"row-{index}"))).ToImmutableArray();
        OperationResult result = _validator.Validate(TestContent.WithRows(content, rows));
        Assert.Equal(accepted, result.Success);
        if (!accepted) Assert.Equal("row-limit", Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Validate_BoundsNodeCount(int count, bool accepted)
    {
        OutputNode[] leaves = Enumerable.Range(1, count - 1).Select(index => new OutputNode
        { Id = $"leaf{index}", Kind = NodeKind.Text, Value = ValueBinding.Constant("Text") }).ToArray();
        OutputContent content = new()
        {
            Document = new()
            {
                Id = "bounded-tree",
                RootId = "root",
                Nodes = [new() { Id = "root", Kind = NodeKind.Layout, Children = leaves.Select(node => node.Id).ToImmutableArray() }, .. leaves]
            }
        };
        OperationResult result = _validator.Validate(content);
        Assert.Equal(accepted, result.Success);
        if (!accepted) Assert.Equal("node-limit", Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Validate_BoundsStateCount(int count, bool accepted)
    {
        OutputContent content = TestContent.Tree(0);
        content = content with
        {
            Document = content.Document with
            {
                State = Enumerable.Range(0, count).ToImmutableDictionary(index => $"state{index}",
                _ => new StateDefinition { Kind = StateKind.Text, InitialValue = JsonValues.From("") })
            }
        };
        OperationResult result = _validator.Validate(content);
        Assert.Equal(accepted, result.Success);
        if (!accepted) Assert.Equal("state-limit", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Validate_RejectsImageSourcesOutsideHostBindings()
    {
        OutputContent content = TestContent.ChangeNode(TestContent.Catalog.Get("assets"), "preview",
            node => node with { Value = ValueBinding.Constant("/private/arbitrary-image.png") });
        Assert.Equal("image-binding", Assert.Single(_validator.Validate(content).Errors).Code);
    }

    [Theory]
    [InlineData("local", true)]
    [InlineData("absolute", false)]
    [InlineData("scheme-relative", false)]
    [InlineData("script", false)]
    [InlineData("backslash", false)]
    [InlineData("control", false)]
    public void ImagePolicy_OnlyAllowsApplicationLocalSources(string caseId, bool accepted)
    {
        string source = caseId switch
        {
            "local" => "/samples/sprite.svg",
            "absolute" => "https://example.invalid/image.png",
            "scheme-relative" => "//example.invalid/image.png",
            "script" => "javascript:alert(1)",
            "backslash" => "/\\example.invalid/image.png",
            _ => "/samples/\rimage.png"
        };
        Assert.Equal(accepted, ImageSourcePolicy.IsAllowed(source));
    }
}
