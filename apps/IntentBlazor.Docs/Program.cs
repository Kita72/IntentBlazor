using IntentBlazor.Docs;

if (args.Contains("--verify-offline-publish")) { OfflinePublishCheck.Run(); return; }
var app = DocsHost.Build(new WebApplicationOptions { Args = args });
app.Run();
