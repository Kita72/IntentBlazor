# Third-party dependencies

IntentBlazor source is licensed under [MIT](LICENSE). Dependencies retain their own licenses and notices. Central package versions are pinned in [Directory.Packages.props](Directory.Packages.props), with transitive dependencies recorded in each package lock file.

The optional content package uses:

| Dependency | Use | Upstream license / source |
| --- | --- | --- |
| SkiaSharp and native assets | PNG/JPEG decoding and deterministic PNG resize | [MIT and bundled notices](https://github.com/mono/SkiaSharp) |
| PdfPig | PDF text extraction | [Apache-2.0](https://github.com/UglyToad/PdfPig/blob/master/LICENSE) |

Review the packages' bundled license/notice files when distributing an application, including their native/transitive dependencies. This table records direct content dependencies; it does not replace those upstream notices.

The IIS docs publisher now exports license metadata and bundled license/notice files from its exact restored dependency graph into `dependency-notices.json` and `licenses/` in the host artifact. This includes SkiaSharp.NativeAssets.Win32's `THIRD-PARTY-NOTICES.txt`; removing native debug PDBs does not remove its notices or runtime DLL. Preserve those files when distributing the app. The November 7, 2026 baseline advisory query covered all 20 projects and transitive packages with no affected packages reported; it is a dated advisory result, not a security guarantee. See the [release operations record](docs/foundation-step5-release-operations.md).

Documentation sample sprites and the generated geometric PNG are original IntentBlazor samples under this repository's MIT license. Tests create synthetic text, tabular data, images, and PDFs; no third-party game assets or private documents are included.

Staff Business uses Microsoft's [EF Core SQLite provider](https://github.com/dotnet/efcore), licensed under MIT. HTTP identity tests use [ASP.NET Core TestHost](https://github.com/dotnet/aspnetcore), also under MIT. The new app shells/reconnect UI were generated from the official ASP.NET Core templates. See package/upstream notices for transitive and native dependency terms. The [asset sample notice](examples/AssetWorkbench/ASSET-LICENSE.md) records the provenance of the procedural PNG set.

The documentation host alone uses [Markdig](https://github.com/xoofx/markdig), licensed under [BSD-2-Clause](https://github.com/xoofx/markdig/blob/main/license.txt), for authored repository guides. It is not a dependency of any IntentBlazor library package. The framework Markdown node keeps its own bounded renderer.
