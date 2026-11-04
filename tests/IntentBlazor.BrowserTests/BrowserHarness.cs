using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace IntentBlazor.BrowserTests;

public sealed class BrowserHarness : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    public IBrowser Browser { get; private set; } = null!;
    public static string Root { get; } = FindRoot();
    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Args = ["--disable-gpu", "--disable-webgl", "--disable-features=Vulkan"]
        });
    }
    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _playwright?.Dispose();
    }
    public async Task RunAsync(string name, string app, Func<BrowserCase, Task> test, params string[] arguments)
    {
        await using BrowserCase fixture = new(Browser, name, app, arguments);
        try { await fixture.StartAsync(); await test(fixture); fixture.AssertNoUnexpectedRequestsOrErrors(); }
        catch
        {
            try { await fixture.CaptureFailureAsync(); }
            catch (Exception error) when (error is PlaywrightException or IOException)
            { Console.Error.WriteLine("Browser failure diagnostics could not complete: " + error.GetType().Name); }
            throw;
        }
    }
    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IntentBlazor.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Run browser tests from a built repository.");
    }
}

public sealed class BrowserCase(IBrowser browser, string name, string app, string[] arguments) : IAsyncDisposable
{
    private readonly List<IBrowserContext> _contexts = [];
    private readonly ConcurrentQueue<string> _errors = new();
    private readonly Channel<IWebSocketRoute> _sockets = Channel.CreateUnbounded<IWebSocketRoute>();
    private IWebSocketRoute? _socket;
    private IWebSocketRoute? _serverSocket;
    private bool _tracing;
    private string? _failure;
    public int SuppressedFrames { get; private set; }
    public OwnedServer Server { get; } = new(app, arguments);
    public IBrowserContext Context { get; private set; } = null!;
    public IPage Page { get; private set; } = null!;
    public async Task StartAsync()
    {
        await Server.StartAsync();
        Context = await NewContextAsync();
        await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = false });
        _tracing = true;
        Page = await Context.NewPageAsync();
        await Page.RouteWebSocketAsync("**/*", route =>
        {
            if (!SameOrigin(route.Url)) { _errors.Enqueue("Unexpected WebSocket destination."); return; }
            _serverSocket = route.ConnectToServer();
            _socket = route; _sockets.Writer.TryWrite(route);
        });
        if (app is "fixture" or "packed") await OpenAsync();
        else
        {
            await Page.GotoAsync(Server.Url);
            await Expect(Page.GetByLabel("Example identity")).ToBeVisibleAsync();
        }
    }
    public async Task OpenAsync()
    {
        await Page.GotoAsync(Server.Url);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true })).ToBeEnabledAsync();
        while (_sockets.Reader.TryRead(out _)) { }
    }
    public async Task<IBrowserContext> NewContextAsync()
    {
        IBrowserContext context = await browser.NewContextAsync(new() { AcceptDownloads = true, ServiceWorkers = ServiceWorkerPolicy.Block });
        _contexts.Add(context); context.SetDefaultTimeout(15000); context.SetDefaultNavigationTimeout(30000);
        context.Page += (_, page) => page.PageError += (_, error) => _errors.Enqueue(error);
        await context.RouteAsync("**/*", async route =>
        {
            if (SameOrigin(route.Request.Url) || route.Request.Url.StartsWith("data:", StringComparison.Ordinal))
                await route.ContinueAsync();
            else { _errors.Enqueue("Unexpected external HTTP destination."); await route.AbortAsync(); }
        });
        return context;
    }
    private bool SameOrigin(string url)
    {
        Uri address = new(url);
        return address.IsLoopback && address.Port == new Uri(Server.Url).Port;
    }
    public async Task SignInAsync(IPage page, string identity = "private")
    {
        await page.GotoAsync(Server.Url);
        await page.GetByLabel("Example identity").SelectOptionAsync(identity);
        await page.GetByRole(AriaRole.Button, new() { Name = "Open example workspace" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true })).ToBeEnabledAsync();
    }
    public static async Task SubmitAsync(IPage page, string request)
    {
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Request", Exact = true }).FillAsync(request);
        await page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true }).ClickAsync();
    }
    public static async Task ChangeAsync(IPage page, string label, string value)
    {
        string? revision = await page.Locator(".ib-output").GetAttributeAsync("data-revision");
        var field = page.GetByLabel(label, new() { Exact = true });
        await field.FillAsync(value); await field.PressAsync("Tab");
        await Expect(page.Locator(".ib-output")).Not.ToHaveAttributeAsync("data-revision", revision!);
        await Expect(field).ToHaveValueAsync(value);
    }
    public async Task DisconnectAndReconnectAsync()
    {
        // Route the real server socket. Closing it exercises SignalR reconnect,
        // rather than replacing the document or simulating an application event.
        IWebSocketRoute current = _socket ?? throw new InvalidOperationException("No real circuit socket was observed.");
        while (_sockets.Reader.TryRead(out _)) { }
        await current.CloseAsync(new() { Code = 1012, Reason = "Offline fixture reconnect" });
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        await _sockets.Reader.ReadAsync(timeout.Token);
    }
    public void SuppressActionReply()
    {
        // After the real handshake, drop replies on this connection without
        // interpreting or re-encoding Blazor's binary protocol. Reconnect gets
        // a new unmodified socket and the framework can replay pending batches.
        (_serverSocket ?? throw new InvalidOperationException("No server socket was observed."))
            .OnMessage(_ => SuppressedFrames++);
    }
    public void AssertNoUnexpectedRequestsOrErrors() => Assert.Empty(_errors);
    public async Task CaptureFailureAsync()
    {
        string safeName = Regex.Replace(name, "[^A-Za-z0-9_.-]", "_");
        _failure = Path.Combine(BrowserHarness.Root, "artifacts", "browser", "failures", safeName + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_failure);
        await File.WriteAllLinesAsync(Path.Combine(_failure, "host.log"), Server.Logs);
        await File.WriteAllLinesAsync(Path.Combine(_failure, "browser-errors.txt"), _errors);
        if (Page is not null && !Page.IsClosed)
        {
            await File.WriteAllTextAsync(Path.Combine(_failure, "page.html"), await Page.ContentAsync());
            await Page.ScreenshotAsync(new() { Path = Path.Combine(_failure, "page.png"), FullPage = true, Timeout = 5000 });
        }
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_tracing) await Context.Tracing.StopAsync(new() { Path = _failure is null ? null : Path.Combine(_failure, "trace.zip") });
            foreach (var context in _contexts) await context.CloseAsync();
        }
        finally { await Server.DisposeAsync(); }
    }
}

public sealed class OwnedServer(string app, string[] arguments) : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _logs = new();
    private Process? _process;
    private string? _address;
    public string DataDirectory { get; } = Path.Combine(Path.GetTempPath(), "IntentBlazor-Browser-" + Guid.NewGuid().ToString("N"));
    public string Url => _address ?? throw new InvalidOperationException("The owned fixture server is not ready.");
    public string[] Logs => _logs.ToArray();
    public async Task StartAsync()
    {
        string directory = app switch
        {
            "fixture" => "tests/IntentBlazor.BrowserHost",
            "news" => "examples/GamingNews",
            "staff" => "examples/StaffBusiness",
            "assets" => "examples/AssetWorkbench",
            "packed" => File.ReadAllText(Path.Combine(BrowserHarness.Root, "artifacts/phase7-quickstart-path.txt")).Trim(),
            _ => throw new ArgumentException("Unknown offline browser application.")
        };
        string projectDirectory = Path.GetFullPath(directory, BrowserHarness.Root);
        string assembly = app switch
        {
            "fixture" => "IntentBlazor.BrowserHost",
            "news" => "IntentBlazor.GamingNews",
            "staff" => "IntentBlazor.StaffBusiness",
            "assets" => "IntentBlazor.AssetWorkbench",
            _ => "IntentBlazor.Quickstart"
        };
        Directory.CreateDirectory(DataDirectory);
        ProcessStartInfo start = new("dotnet") { WorkingDirectory = projectDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string arg in new[] { Path.Combine(projectDirectory, "bin/Release/net10.0", assembly + ".dll"),
            "--urls", _address ?? "http://127.0.0.1:0", "--environment", "Development", "--Ai:Provider", app == "packed" ? "" : "Fixture",
            "--News:FeedUrl", "", "--Example:DataDirectory", DataDirectory, "--Browser:Enabled", "true", "--Browser:DataDirectory", DataDirectory })
            start.ArgumentList.Add(arg);
        foreach (string arg in arguments) start.ArgumentList.Add(arg);
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        TaskCompletionSource<string> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _process = new() { StartInfo = start, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not { } line) return;
            _logs.Enqueue(line);
            Match match = Regex.Match(line, @"Now listening on: (http://127\.0\.0\.1:\d+)");
            if (match.Success) ready.TrySetResult(match.Groups[1].Value);
        };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is { } line) _logs.Enqueue(line); };
        _process.Exited += (_, _) => ready.TrySetException(new InvalidOperationException("The owned offline host exited before becoming ready."));
        _process.Start(); _process.BeginOutputReadLine(); _process.BeginErrorReadLine();
        _address = await ready.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }
    public async Task RestartAsync() { await StopAsync(); await StartAsync(); }
    private async Task StopAsync()
    {
        if (_process is null) return;
        if (!_process.HasExited) { _process.Kill(entireProcessTree: true); await _process.WaitForExitAsync(); }
        _process.Dispose(); _process = null;
    }
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        string full = Path.GetFullPath(DataDirectory);
        if (Path.GetDirectoryName(full) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) ||
            !Path.GetFileName(full).StartsWith("IntentBlazor-Browser-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to clean a directory outside the owned temporary browser root.");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
