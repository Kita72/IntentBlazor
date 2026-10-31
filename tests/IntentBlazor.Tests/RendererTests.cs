using System.Text.Json;
using IntentBlazor.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntentBlazor.Tests;

public sealed class RendererTests
{
    [Theory]
    [InlineData("news", "headlines", "news-1", "Example RPG patch 1.2")]
    [InlineData("staff", "jobs", "job-101", "Confirm delivery window.")]
    [InlineData("assets", "gallery", "asset-2", "/samples/lantern.svg")]
    public async Task Output_RendersAllDomainsThroughTheSameComponent(string fixture, string node, string key, string expected)
    {
        await WithRenderer(async (renderer, session) =>
        {
            session.Open(TestContent.Catalog.Get(fixture));
            TestContent.Select(session, node, key);
            var root = await renderer.RenderComponentAsync<AiOutput>();
            string html = root.ToHtmlString();
            Assert.Contains(expected, html);
            Assert.Contains("data-node=", html);
            Assert.DoesNotContain("<script", html);
        });
    }

    [Fact]
    public async Task Output_RerendersSelectionAndRetainsOutputOnFailure()
    {
        await WithRenderer(async (renderer, session) =>
        {
            var root = await renderer.RenderComponentAsync<AiOutput>();
            Assert.Contains("No output yet", root.ToHtmlString());
            session.Open(TestContent.Catalog.Get("news"));
            TestContent.Select(session, "headlines", "news-2");
            Assert.Contains("reserve unit", root.ToHtmlString());
            session.OpenJson("{}", []);
            string html = root.ToHtmlString();
            Assert.Contains("role=\"alert\"", html);
            Assert.Contains("reserve unit", html);
        });
    }

    [Fact]
    public async Task Output_EncodesContentAndBlocksNonlocalImages()
    {
        OutputContent content = TestContent.Catalog.Get("assets");
        content = TestContent.WithRows(content, content.Data[0].Rows.SetItem(0,
            content.Data[0].Rows[0].SetItem("name", JsonValues.From("<script>alert(1)</script>"))
                .SetItem("image", JsonValues.From("javascript:alert(1)"))));
        await WithRenderer(async (renderer, session) =>
        {
            session.Open(content);
            TestContent.Select(session, "gallery", "asset-1");
            string html = (await renderer.RenderComponentAsync<AiOutput>()).ToHtmlString();
            Assert.Contains("&lt;script&gt;", html);
            Assert.DoesNotContain("<script>", html);
            Assert.DoesNotContain("src=\"javascript:", html);
            Assert.Contains("Image unavailable", html);
        });
    }

    [Fact]
    public async Task Output_RendersEmptyDatasetAndDirtyDraftState()
    {
        await WithRenderer(async (renderer, session) =>
        {
            session.Open(TestContent.WithRows(TestContent.Catalog.Get("news"), []));
            var root = await renderer.RenderComponentAsync<AiOutput>();
            Assert.Contains("No records available", root.ToHtmlString());
            session.Open(TestContent.Catalog.Get("staff"));
            TestContent.Select(session, "jobs", "job-101");
            session.Change("notesInput", "Local note", session.Revision);
            string html = root.ToHtmlString();
            Assert.Contains("Unsaved local draft", html);
            Assert.Contains("Local note", html);
            Assert.Contains("for=\"ib-", html);
            Assert.Contains("Clear staff draft", html);
        });
    }

    [Fact]
    public async Task Input_RendersAccessibleControlsWithoutExtraPageWiring()
    {
        await WithRenderer(async (renderer, _) =>
        {
            string html = (await renderer.RenderComponentAsync<AiInput>()).ToHtmlString();
            Assert.Contains("<label for=", html);
            Assert.Contains("<textarea", html);
            Assert.Contains("maxlength=\"2048\"", html);
            Assert.Contains("type=\"submit\"", html);
            Assert.Contains("Connecting", html);
            Assert.Contains("disabled", html);
        });
    }

    [Fact]
    public async Task Components_UseExplicitSessionAndRenderBusyCancellationWithoutLosingOutput()
    {
        await WithRenderer(async (renderer, defaultSession) =>
        {
            defaultSession.Open(TestContent.Catalog.Get("news"));
            TaskCompletionSource<OutputContent> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using AiSession explicitSession = TestContent.Session(new DelegateInputProcessor((_, _, token) =>
                new(pending.Task.WaitAsync(token))));
            explicitSession.Open(TestContent.Catalog.Get("assets"));
            TestContent.Select(explicitSession, "gallery", "asset-1");
            ParameterView parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { ["Session"] = explicitSession });
            var input = await renderer.RenderComponentAsync<AiInput>(parameters);
            var output = await renderer.RenderComponentAsync<AiOutput>(parameters);
            Task<OperationResult> request = explicitSession.SubmitAsync("pending");
            Assert.Contains("Preparing output", output.ToHtmlString());
            Assert.Contains("/samples/crate.svg", output.ToHtmlString());
            Assert.DoesNotContain("Gaming updates", output.ToHtmlString());
            Assert.Contains("Cancel request", input.ToHtmlString());
            Assert.Contains("disabled", input.ToHtmlString());
            explicitSession.Cancel();
            Assert.False((await request).Success);
            Assert.Contains("cancelled", output.ToHtmlString());
            Assert.Contains("/samples/crate.svg", output.ToHtmlString());
            Assert.DoesNotContain("Cancel request", input.ToHtmlString());
        });
    }

    [Fact]
    public async Task Table_ExposesAFocusableNamedScrollRegion()
    {
        await WithRenderer(async (renderer, session) =>
        {
            session.Open(TestContent.Catalog.Get("staff"));
            string html = (await renderer.RenderComponentAsync<AiOutput>()).ToHtmlString();
            Assert.Contains("data-node=\"jobs\" tabindex=\"0\"", html);
            Assert.Contains("role=\"region\" aria-label=\"Jobs waiting on materials table\"", html);
            Assert.Contains("Sort by Customer", html);
        });
    }

    private static async Task WithRenderer(Func<HtmlRenderer, AiSession, Task> check)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddIntentBlazor();
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        await using HtmlRenderer renderer = new(scope.ServiceProvider, provider.GetRequiredService<ILoggerFactory>());
        AiSession session = scope.ServiceProvider.GetRequiredService<AiSession>();
        await renderer.Dispatcher.InvokeAsync(() => check(renderer, session));
    }
}
