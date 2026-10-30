# Gaming News and Tips

Independently runnable Phase 5 Blazor Web App. It uses default AiInput/AiOutput with example-owned news services, private bookmarks/comparison snapshots, SQLite checkpoints, and a registered citation renderer. Default news and accounts are synthetic; no model request or real news feed is required.

From the repository root:

~~~powershell
dotnet run --project examples/GamingNews --no-launch-profile -- --environment Development --urls http://localhost:5181
~~~

Open http://localhost:5181 and create your private example identity. Prioritize Emberfall, select its 1.4.2 article, choose its 1.4.1 article as the comparison, and submit a follow-up. Bookmark/save, choose the saved comparison, save the workspace, and reload. Source/date/version attribution and current selection survive. Source links use example.com and represent fictional reports.

Data defaults to ignored artifacts/phase5-news, outside wwwroot. News:FeedUrl optionally selects a host-configured HTTPS JSON feed with the [sample shape](sample-feed.json); no search/scraping is built into core. Ai:Provider and Ai:Model independently select an optional existing model adapter. UserSecretsId is IntentBlazor.Examples.GamingNews. Live-provider verification is deferred.

See the [Phase 5 guide](../../docs/phase-5-example-apps.md) for bounds, identity lifetime, private workspace controls, configured-feed validation, and model setup. Synthetic authentication is Development-only. The [source contract](NewsSource.cs), [scenario/actions](NewsScenario.cs), [documents](NewsDocuments.cs), and [citation renderer](NewsCitations.razor) show the developer implementation. Source/news content is original synthetic MIT material; external feeds require host-supplied redistribution rights.
