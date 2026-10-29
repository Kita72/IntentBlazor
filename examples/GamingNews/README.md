# Gaming News and Tips example

The Phase 2 starter runs independently with the public two-component API. By default it opens the [authored news fixture](../../specs/fixtures/news.json): headlines, selected article, source, and rating. No live news feed is connected.

From the repository root:

~~~powershell
dotnet run --project examples/GamingNews --no-launch-profile -- --environment Development --urls http://localhost:5181
~~~

Open http://localhost:5181. In fixture mode, submit `news` to reopen the workspace. For optional model mode, select Ai:Provider and Ai:Model in trusted host configuration as described in the [connection guide](../../docs/phase-2-model-connections.md). The attributed NewsDataService returns synthetic articles; the selected model composes the view. Both AiInput and AiOutput use the default scoped session. No inference happens on page load.

Model mode is restricted to Development in this anonymous sample. Real model verification is deferred; see the [provider matrix](../../docs/provider-evaluation.md). Bookmarks, live content sources, durable workspaces, and the complete Phase 5 workflow remain planned.

Demonstrate dated content, sources, game preferences, comparison, bookmarks, and saved workspaces. Use fixture content by default and a configurable content service for live data.

Acceptance: inspect an update, compare it with another, save the result, and reopen it while retaining source attribution and the current selection.
