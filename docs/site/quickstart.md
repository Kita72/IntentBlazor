# Two components, one working app

This sample is an independent Blazor Web App. Its default processor opens an authored synthetic catalog for every request. **It makes no AI call and needs no account.** It demonstrates selection, shared state, a rating, a custom score renderer, and draft reset through the same AiInput/AiOutput pair.

## Run it

From the repository root after [installation](installation.md):

~~~powershell
dotnet run --project examples/Quickstart --no-launch-profile -- --urls http://localhost:5184
~~~

Open `http://localhost:5184`, submit `show catalog`, select Brass lantern, and increase the score. The rating updates locally. Clear the score draft to restore its baseline. No business data is saved; refresh creates a new circuit and workspace.

## Place the components

This is the sample's actual [Home.razor](../../examples/Quickstart/Components/Home.razor), compiled with the application. The site expands source placeholders into code; the linked source is also readable directly on GitHub.

{{sample: Components/Home.razor}}

The components resolve one DI-scoped `AiSession`. Keep the pair inside the same interactive circuit. For independent workspaces in a circuit, create separate sessions and pass the same `Session` parameter to each pair; dispose any sessions you create. The [workspace guide](../phase-3-workspaces.md) explains durable logical sessions and ownership.

## Register the host

[Program.cs](../../examples/Quickstart/Program.cs) keeps the usual Blazor server setup:

{{sample: Program.cs}}

`AddQuickstart` calls `AddIntentBlazor()`, registers the app-owned processor and service, and installs the optional sample renderer. With `Ai:Provider` unset it uses `CatalogInputProcessor`. The processor returns host data separately from its presentation document:

{{sample: CatalogInputProcessor.cs}}

This is intentionally small, not a production identity or storage setup. `AddIntentBlazor()` alone supplies a rejecting default processor: use it to render authored output before supplying an input processor. You can replace this sample processor with your own implementation, or let [AddIntentBlazorAI](services.md) register the model processor.

## Add a model and service

Follow [Expose a C# service](services.md) for the sample's complete attribute/explicit registration and provider switch. Selecting a model changes input processing; it does not change the two components. Use your provider's installed/enabled model identifier. No specific live model is certified yet; [provider evidence](../provider-evaluation.md) records the distinction.

The model can choose supported nodes or the installed score renderer. It is not required to reproduce the authored fixture exactly. Bad proposals leave the last valid workspace available.

## Extend the output

The score widget is a developer-registered typed renderer. Its event changes a bounded numeric state and participates in dirty-field preservation. The [renderer tutorial](extensions.md) shows both the contract and Razor source. A custom renderer does not become an application write handler.

For complete workflows, use the [news, staff, and asset apps](../phase-5-example-apps.md). They add ownership, business persistence and version checks, or actual image artifacts as appropriate.
