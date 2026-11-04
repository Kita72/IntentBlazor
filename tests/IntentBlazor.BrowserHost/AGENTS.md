# IntentBlazor.BrowserHost

Test-only Blazor Web App, Interactive Server per page, created with dotnet new blazor -int Server --empty. Keep Routes static; only the fixture page opts into interactivity. Require Development and Browser:Enabled. All state belongs to a private test directory; bind loopback.

This is a labelled deterministic fixture, never a model connection or deployment target. Use default AiInput/AiOutput and the actual AI/state validation pipeline with an in-process scripted client. Keep diagnostic controls here, outside framework/example pages. Follow root instructions; never start local models or paid inference.
