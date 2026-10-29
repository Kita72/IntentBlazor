using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using IntentBlazor.Runtime;
using Microsoft.Extensions.AI;

namespace IntentBlazor.AI;

public sealed class CodexOptions
{
    public string ExecutablePath { get; set; } = "codex";
    public string Model { get; set; } = "";
    public int MaximumEventBytes { get; set; } = 524288;
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ExecutablePath) || string.IsNullOrWhiteSpace(Model) || Model.Length > 200 ||
            MaximumEventBytes is < 1024 or > 2097152)
            throw new ArgumentException("Configure a Codex executable, model, and bounded event size.");
    }
}

public sealed record CodexProcessRequest(string Prompt, string OutputSchema);

public interface ICodexProcessRunner
{
    IAsyncEnumerable<string> RunAsync(CodexProcessRequest request, CancellationToken cancellationToken);
}

// A developer-workstation connection to the documented CLI, not the current desktop chat.
public sealed class CodexChatClient(CodexOptions options, ICodexProcessRunner runner) : IChatClient
{
    private const string Schema = """
        {"type":"object","properties":{"documentJson":{"type":"string"}},"required":["documentJson"],"additionalProperties":false}
        """;

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? chatOptions = null,
        CancellationToken cancellationToken = default)
    {
        List<ChatResponseUpdate> updates = [];
        await foreach (var update in GetStreamingResponseAsync(messages, chatOptions, cancellationToken)) updates.Add(update);
        return updates.ToChatResponse();
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options.Validate();
        if (chatOptions?.Tools is { Count: > 0 } || chatOptions?.Temperature is not null || chatOptions?.MaxOutputTokens is not null)
            throw new NotSupportedException("The Codex CLI adapter supports preloaded host data and default model generation settings only.");
        string prompt = string.Join("\n\n", messages.Select(message => $"{message.Role}:\n{message.Text}"));
        string? final = null;
        bool completed = false;
        long total = 0;
        UsageDetails? usage = null;
        await foreach (string line in runner.RunAsync(new(prompt, Schema), cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += Encoding.UTF8.GetByteCount(line);
            if (total > options.MaximumEventBytes) throw new InputRejectedException("The Codex event limit was reached.");
            using JsonDocument parsed = JsonDocument.Parse(line);
            JsonElement root = parsed.RootElement;
            string? type = root.GetProperty("type").GetString();
            if (type is "turn.failed" or "error") throw new InputRejectedException("The Codex request failed. Check the CLI installation and sign-in.");
            if (type == "item.completed" && root.TryGetProperty("item", out JsonElement item) &&
                item.GetProperty("type").GetString() == "agent_message" &&
                (!item.TryGetProperty("phase", out JsonElement phase) || phase.GetString() is null or "final_answer"))
                final = item.GetProperty("text").GetString();
            if (type == "turn.completed")
            {
                completed = true;
                if (root.TryGetProperty("usage", out JsonElement reported))
                    usage = new()
                    {
                        InputTokenCount = reported.TryGetProperty("input_tokens", out JsonElement input) ? input.GetInt64() : null,
                        OutputTokenCount = reported.TryGetProperty("output_tokens", out JsonElement output) ? output.GetInt64() : null
                    };
            }
        }
        if (!completed || string.IsNullOrWhiteSpace(final))
            throw new InputRejectedException("Codex ended without a completed final response.");
        yield return new(ChatRole.Assistant, final);
        if (usage is not null) yield return new() { Contents = [new UsageContent(usage)] };
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is not null ? null :
        serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("Codex CLI", defaultModelId: options.Model) :
        serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}

public sealed class CodexProcessRunner(CodexOptions options) : ICodexProcessRunner
{
    public async IAsyncEnumerable<string> RunAsync(CodexProcessRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        options.Validate();
        string directory = Path.Combine(Path.GetTempPath(), "IntentBlazor-Codex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using Process process = new();
        Task? drainError = null;
        try
        {
            string schemaPath = Path.Combine(directory, "output-schema.json");
            await File.WriteAllTextAsync(schemaPath, request.OutputSchema, cancellationToken);
            ProcessStartInfo start = CreateStartInfo(options, directory, schemaPath);
            process.StartInfo = start;
            try { if (!process.Start()) throw new InvalidOperationException(); }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
            { throw new InputRejectedException("Cannot start Codex. Configure the native executable path on the trusted application host."); }
            using CancellationTokenRegistration cancel = cancellationToken.Register(() => Stop(process));
            drainError = DrainAsync(process.StandardError, cancellationToken);
            await process.StandardInput.WriteAsync(request.Prompt.AsMemory(), cancellationToken);
            process.StandardInput.Close();
            int received = 0;
            // Read bounded lines without allocating an unbounded ReadLine buffer.
            StringBuilder line = new();
            char[] buffer = new char[2048];
            int read;
            while ((read = await process.StandardOutput.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
            {
                received += Encoding.UTF8.GetByteCount(buffer, 0, read);
                if (received > options.MaximumEventBytes) throw new InputRejectedException("The Codex event limit was reached.");
                for (int index = 0; index < read; index++)
                {
                    if (buffer[index] == '\n')
                    {
                        string value = line.ToString().TrimEnd('\r');
                        line.Clear();
                        if (value.Length != 0) yield return value;
                    }
                    else line.Append(buffer[index]);
                }
            }
            if (line.Length != 0) yield return line.ToString();
            await process.WaitForExitAsync(cancellationToken);
            await drainError;
            if (process.ExitCode != 0) throw new InputRejectedException("The Codex process failed. Check installation and sign-in; private diagnostics are not included.");
        }
        finally
        {
            Stop(process);
            if (drainError is not null)
            {
                try { await drainError.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (Exception error) when (error is OperationCanceledException or IOException or TimeoutException) { }
            }
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static ProcessStartInfo CreateStartInfo(CodexOptions options, string directory, string schemaPath)
    {
        ProcessStartInfo start = new(options.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = directory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (string argument in new[] { "exec", "--json", "--ephemeral", "--ignore-user-config", "--ignore-rules",
            "--skip-git-repo-check", "--sandbox", "read-only", "--disable", "shell_tool", "--disable", "apps",
            "-c", "web_search=\"disabled\"", "-m", options.Model, "-C", directory, "--output-schema", schemaPath, "-" })
            start.ArgumentList.Add(argument);
        return start;
    }

    private static void Stop(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken token)
    {
        char[] buffer = new char[2048];
        while (await reader.ReadAsync(buffer.AsMemory(), token) != 0) { }
    }
}
