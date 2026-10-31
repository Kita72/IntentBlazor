# Two-component quickstart

A minimal standalone Blazor Web App using public IntentBlazor APIs. No dependency on the docs host or any project-operated service. Data is original synthetic text under the repository MIT license. The default authored processor always opens one catalog; it does not simulate AI.

From the repository root with the SDK from [global.json](../../global.json):

~~~powershell
dotnet run --project examples/Quickstart --no-launch-profile -- --urls http://localhost:5184
~~~

Submit any request, select an item, change the score through a registered custom renderer, and clear the local draft. No application database is changed. State lasts for the circuit; refresh starts a new workspace. Use the [workspace guide](../../docs/phase-3-workspaces.md) to add ownership and persistence.

For optional live mode configure server-side `Ai:Provider` (`Ollama`, `LMStudio`, or `OpenAI`) and `Ai:Model` with your installed/enabled model ID. Only OpenAI needs `Ai:ApiKey`, supplied through host secret storage, never source. `Ai:ExplicitTools=true` selects explicit registration instead of attributes. The model has a 45-second request limit, four model calls, four data-tool calls, one correction, and 4,096 output tokens per request. No write action is registered. The HTTP provider path has deterministic adapter tests; no live model is certified yet. Use this local learning sample with synthetic data; a public live app also needs authentication, quotas, rate limits and operational controls.

The [quickstart](../../docs/site/quickstart.md), [service guide](../../docs/site/services.md), and [renderer tutorial](../../docs/site/extensions.md) display these exact compiled source files inside the docs site. Their source placeholders are expanded by the docs host; on GitHub follow the source links.
