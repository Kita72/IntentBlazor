using IntentBlazor.ProductionHost;

var app = ProductionHostApp.Build(new WebApplicationOptions { Args = args });
app.Run();
