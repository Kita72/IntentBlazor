# Asset Workbench

Independently runnable Phase 5 Blazor Web App using default AiInput/AiOutput, private content services, and the public image-editor pack. An app-owned typed version-picker extension reports live dimensions and resolves retained originals without modifying core.

~~~powershell
dotnet run --project examples/AssetWorkbench --no-launch-profile -- --environment Development --urls http://localhost:5183
~~~

Open http://localhost:5183 and create a private example identity. Attach PNG/JPEG images through AiInput and ask to inspect them, or import the original sample set. Select an image, change dimensions, and run Resize preview. Compare original/preview, zoom, use the original as the working version, choose a derived version again, and download the displayed PNG. Save/reload to recover the working references. Original files remain immutable; retain their attachment references for parent comparison.

Private files/checkpoints default to ignored artifacts/phase5-assets, outside wwwroot. Set Example:DataDirectory for another private directory. [AssetScenario](AssetScenario.cs), the [version contract](AssetVersionsContract.cs), and [renderer](Components/AssetVersions.razor) contain the example logic. Sample pixels are generated from original project code; see [ASSET-LICENSE](ASSET-LICENSE.md).

Ai:Provider/Ai:Model opt into existing chat adapters. Fixture is default; UserSecretsId is IntentBlazor.Examples.AssetWorkbench. Selecting a chat model does not imply vision or generative artwork. See the [Phase 5 guide](../../docs/phase-5-example-apps.md) and [content capability table](../../docs/phase-4-content-and-setup.md) for bounds, explicit identity, optional model setup, and deferred live verification. Synthetic authentication is Development-only.
