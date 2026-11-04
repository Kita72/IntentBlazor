namespace IntentBlazor.BrowserTests;

public sealed class FixtureBrowserTests(BrowserHarness browser) : IClassFixture<BrowserHarness>
{
    private static ILocator Selected(IPage page) => page.Locator("[data-row-key='one'] button");
    private static async Task DraftAsync(IPage page, string text = "Exact private fixture draft")
    {
        await Selected(page).ClickAsync();
        await page.GetByLabel("Fixture notes", new() { Exact = true }).FillAsync(text);
        await page.GetByLabel("Fixture notes", new() { Exact = true }).PressAsync("Tab");
        await Expect(page.Locator(".ib-draft")).ToBeVisibleAsync();
    }
    [Fact]
    public Task DefaultPair_SelectsSortsAndPreservesCommittedDraftAndSelectionThroughFollowup() =>
        browser.RunAsync(nameof(DefaultPair_SelectsSortsAndPreservesCommittedDraftAndSelectionThroughFollowup), "fixture", async test =>
        {
            IPage page = test.Page;
            await Expect(page.GetByRole(AriaRole.Region, new() { Name = "Dynamic output", Exact = true })).ToHaveCountAsync(1);
            await Expect(Selected(page)).ToHaveAttributeAsync("aria-pressed", "false");
            await DraftAsync(page);
            await page.GetByRole(AriaRole.Button, new() { Name = "Sort by Days waiting", Exact = true }).ClickAsync();
            await Expect(page.Locator("[data-node='jobs'] tbody tr").First).ToHaveAttributeAsync("data-row-key", "two");
            await Expect(page.Locator("[data-node='jobs'] th").Nth(1)).ToHaveAttributeAsync("aria-sort", "ascending");
            await page.GetByRole(AriaRole.Button, new() { Name = "Sort by Days waiting", Exact = true }).ClickAsync();
            await Expect(page.Locator("[data-node='jobs'] tbody tr").First).ToHaveAttributeAsync("data-row-key", "one");
            await Expect(page.Locator("[data-node='jobs'] th").Nth(1)).ToHaveAttributeAsync("aria-sort", "descending");
            await BrowserCase.SubmitAsync(page, "valid");
            await Expect(page.Locator("[data-node='notice']")).ToContainTextAsync("Accepted offline fixture");
            await Expect(page.GetByLabel("Fixture notes", new() { Exact = true })).ToHaveValueAsync("Exact private fixture draft");
            await Expect(Selected(page)).ToHaveAttributeAsync("aria-pressed", "true");
            await Expect(page.Locator("[data-node='jobs'] th").Nth(1)).ToHaveAttributeAsync("aria-sort", "descending");
            await Expect(page.GetByTestId("history")).ToHaveTextAsync("1");
        });
    [Theory]
    [InlineData("invalid")]
    [InlineData("partial")]
    [InlineData("unavailable")]
    [InlineData("budget")]
    [InlineData("timeout")]
    public Task FailedRequests_KeepAcceptedOutputAndDraftAndAllowRecovery(string fault) =>
        browser.RunAsync(nameof(FailedRequests_KeepAcceptedOutputAndDraftAndAllowRecovery) + "-" + fault, "fixture", async test =>
        {
            IPage page = test.Page; await DraftAsync(page);
            string? revision = await page.Locator(".ib-output").GetAttributeAsync("data-revision");
            await BrowserCase.SubmitAsync(page, fault);
            await Expect(page.Locator(".ib-errors")).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true })).ToBeEnabledAsync();
            await Expect(page.Locator(".ib-output")).ToHaveAttributeAsync("data-revision", revision!);
            await Expect(page.Locator("[data-node='notice']")).ToContainTextAsync("Initial offline fixture");
            await Expect(page.GetByLabel("Fixture notes", new() { Exact = true })).ToHaveValueAsync("Exact private fixture draft");
            await Expect(Selected(page)).ToHaveAttributeAsync("aria-pressed", "true");
            await Expect(page.Locator("body")).Not.ToContainTextAsync("Synthetic private upstream failure detail");
            await BrowserCase.SubmitAsync(page, "valid");
            await Expect(page.Locator("[data-node='notice']")).ToContainTextAsync("Accepted offline fixture");
            await Expect(page.Locator(".ib-errors")).ToHaveCountAsync(0);
            await Expect(page.GetByLabel("Fixture notes", new() { Exact = true })).ToHaveValueAsync("Exact private fixture draft");
        }, "--Browser:TimeoutMilliseconds", fault == "timeout" ? "1200" : "30000");
    [Fact]
    public Task CancelRequest_EndsBusyStatePreservesDraftAndAllowsNextRequest() =>
        browser.RunAsync(nameof(CancelRequest_EndsBusyStatePreservesDraftAndAllowsNextRequest), "fixture", async test =>
        {
            await DraftAsync(test.Page); await BrowserCase.SubmitAsync(test.Page, "cancel");
            await test.Page.GetByRole(AriaRole.Button, new() { Name = "Cancel request", Exact = true }).ClickAsync();
            await Expect(test.Page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true })).ToBeEnabledAsync();
            await Expect(test.Page.Locator(".ib-errors")).ToContainTextAsync("cancel");
            await Expect(test.Page.GetByLabel("Fixture notes", new() { Exact = true })).ToHaveValueAsync("Exact private fixture draft");
            await BrowserCase.SubmitAsync(test.Page, "valid");
            await Expect(test.Page.Locator("[data-node='notice']")).ToContainTextAsync("Accepted offline fixture");
        });
    [Fact]
    public Task LateResponse_RejectsOlderRevisionRetainsNewEditAndFocusedField() =>
        browser.RunAsync(nameof(LateResponse_RejectsOlderRevisionRetainsNewEditAndFocusedField), "fixture", async test =>
        {
            IPage page = test.Page; await DraftAsync(page); await BrowserCase.SubmitAsync(page, "hold");
            await Expect(page.GetByTestId("waiting")).ToHaveTextAsync("True");
            string? before = await page.Locator(".ib-output").GetAttributeAsync("data-revision");
            var notes = page.GetByLabel("Fixture notes", new() { Exact = true });
            await notes.FillAsync("Newer direct edit"); await notes.PressAsync("Tab");
            await Expect(page.Locator(".ib-output")).Not.ToHaveAttributeAsync("data-revision", before!);
            await notes.FocusAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Release fixture response" }).DispatchEventAsync("click");
            await Expect(page.Locator(".ib-errors")).ToContainTextAsync("stale-revision");
            await Expect(notes).ToHaveValueAsync("Newer direct edit"); await Expect(notes).ToBeFocusedAsync();
            await Expect(page.Locator("[data-node='notice']")).ToContainTextAsync("Initial offline fixture");
        });
    [Fact]
    public Task Recovery_DistinguishesSurvivingCircuitFromReloadAndProcessRestore() =>
        browser.RunAsync(nameof(Recovery_DistinguishesSurvivingCircuitFromReloadAndProcessRestore), "fixture", async test =>
        {
            IPage page = test.Page; await DraftAsync(page);
            await BrowserCase.SubmitAsync(page, "valid");
            await Expect(page.Locator("[data-node='notice']")).ToContainTextAsync("Accepted offline fixture");
            string circuit = await page.GetByTestId("circuit").InnerTextAsync();
            await Expect(page.GetByTestId("history")).ToHaveTextAsync("1");
            string history = await page.GetByTestId("history").InnerTextAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Save fixture checkpoint" }).ClickAsync();
            await Expect(page.GetByText("Fixture checkpoint saved.", new() { Exact = true })).ToBeVisibleAsync();
            await test.DisconnectAndReconnectAsync();
            await Expect(page.GetByTestId("circuit")).ToHaveTextAsync(circuit);
            await BrowserCase.ChangeAsync(page, "Fixture notes", "Edit after reconnect");
            string checkpoint = await page.GetByTestId("checkpoint").InnerTextAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Save fixture checkpoint" }).ClickAsync();
            await Expect(page.GetByTestId("checkpoint")).Not.ToHaveTextAsync(checkpoint);
            await page.ReloadAsync();
            await Expect(page.GetByTestId("circuit")).Not.ToHaveTextAsync(circuit);
            await Expect(page.GetByLabel("Fixture notes", new() { Exact = true })).ToHaveValueAsync("Edit after reconnect");
            await Expect(page.GetByTestId("history")).ToHaveTextAsync(history);
            string reloadedCircuit = await page.GetByTestId("circuit").InnerTextAsync();
            await test.Server.RestartAsync(); await test.OpenAsync();
            await Expect(page.GetByTestId("circuit")).Not.ToHaveTextAsync(reloadedCircuit);
            await Expect(page.GetByLabel("Fixture notes", new() { Exact = true })).ToHaveValueAsync("Edit after reconnect");
            await Expect(page.GetByTestId("history")).ToHaveTextAsync(history);
            await Expect(Selected(page)).ToHaveAttributeAsync("aria-pressed", "true");
        });
    [Fact]
    public Task SetupAndKeyboard_ApplyNextRequestAndExposeUnsupportedOptionsAndNarrowChartAlternative() =>
        browser.RunAsync(nameof(SetupAndKeyboard_ApplyNextRequestAndExposeUnsupportedOptionsAndNarrowChartAlternative), "fixture", async test =>
        {
            IPage page = test.Page;
            var request = page.GetByRole(AriaRole.Textbox, new() { Name = "Request", Exact = true });
            await request.FocusAsync(); await request.PressAsync("Tab");
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true })).ToBeFocusedAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true }).PressAsync("Shift+Tab");
            await Expect(request).ToBeFocusedAsync();
            await page.GetByLabel("Temperature", new() { Exact = true }).FillAsync("0.4");
            await page.GetByRole(AriaRole.Button, new() { Name = "Apply AI settings" }).ClickAsync();
            await BrowserCase.SubmitAsync(page, "valid");
            await Expect(page.GetByTestId("temperature")).ToHaveTextAsync("0.4");
            await page.GetByText("Advanced adapter options", new() { Exact = true }).ClickAsync();
            await Expect(page.GetByLabel("Top P", new() { Exact = true })).ToBeDisabledAsync();
            await Expect(page.GetByRole(AriaRole.Combobox, new() { Name = "Reasoning effort", Exact = true })).ToBeDisabledAsync();
            await page.SetViewportSizeAsync(360, 800);
            await page.Locator("[data-node='chart'] summary").ClickAsync();
            var chart = page.GetByRole(AriaRole.Region, new() { Name = "Fixture waiting days data table", Exact = true });
            await Expect(chart).ToBeVisibleAsync();
            await chart.FocusAsync(); await Expect(chart).ToBeFocusedAsync();
            string geometry = await page.EvaluateAsync<string>("() => JSON.stringify({ viewport:innerWidth, document:document.documentElement.scrollWidth, table:document.querySelector('[data-node=jobs]').clientWidth, content:document.querySelector('[data-node=jobs]').scrollWidth })");
            Assert.True(await page.Locator("[data-node='jobs']").EvaluateAsync<bool>("e => e.scrollWidth > e.clientWidth"), geometry);
            Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"), geometry);
        });
}
