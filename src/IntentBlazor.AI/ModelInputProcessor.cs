using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using IntentBlazor.Core;
using IntentBlazor.Runtime;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IntentBlazor.AI;

public sealed class ModelInputProcessor(IChatClient client, ModelProfile profile, IOptions<ModelOptions> configured,
    IEnumerable<DataToolRegistration> registrations, IEnumerable<IOutputDataSource> sources,
    IServiceProvider services, DocumentValidator validator) : IStreamingInputProcessor
{
    public ModelRunReport? LastRun { get; private set; }
    public ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace, CancellationToken cancellationToken) =>
        ProcessAsync(input, workspace, SilentProgress.Instance, cancellationToken);

    public async ValueTask<OutputContent> ProcessAsync(string input, WorkspaceSnapshot workspace,
        IProgress<InputProgress> progress, CancellationToken cancellationToken)
    {
        ModelOptions options = configured.Value;
        options.Validate();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);
        CancellationToken token = timeout.Token;
        RunState state = new(options, progress);
        Stopwatch elapsed = Stopwatch.StartNew();
        string status = "failed";
        try
        {
            if (!profile.SupportsTemperature && options.Temperature is not null)
                throw new InputRejectedException("The selected connection does not support temperature settings.");
            foreach (IOutputDataSource source in sources)
                foreach (OutputDataSet set in await source.LoadAsync(workspace, token)) Accept(set);

            List<AITool> tools = [];
            foreach (DataToolRegistration registration in registrations)
            {
                AIFunction inner = AIFunctionFactory.Create(registration.Method, services.GetRequiredService(registration.ServiceType),
                    new AIFunctionFactoryOptions
                    {
                        Name = registration.Name,
                        Description = registration.Description,
                        SerializerOptions = ModelJson.Options,
                        MarshalResult = (value, _, _) => new(value)
                    });
                DataFunction function = new(inner, Accept, () =>
                {
                    token.ThrowIfCancellationRequested();
                    if (state.ToolCalls >= options.MaximumToolCalls)
                    {
                        state.ToolLimitHit = true;
                        throw new InputRejectedException("The data-call limit was reached.");
                    }
                    state.ToolCalls++;
                    state.Report("Loading application data");
                });
                if (profile.NativeTools) tools.Add(function);
                else
                {
                    if (registration.Method.GetParameters().Any(p => p.ParameterType != typeof(CancellationToken)))
                        throw new InputRejectedException("The Codex CLI connection requires parameterless data tools or an IOutputDataSource.");
                    await function.InvokeAsync(new AIFunctionArguments(), token);
                }
            }

            List<ChatMessage> messages = [new(ChatRole.System, ToolkitInstructions.Create(validator.Actions.Values)),
                new(ChatRole.User, JsonSerializer.Serialize(new
                {
                    request = input,
                    workspace = new { workspace.Document, workspace.Revision, workspace.Values, workspace.Sorting, workspace.DirtyStates, workspace.Conversation, workspace.Artifacts },
                    hostData = state.Data.Values.ToArray()
                }, ModelJson.Options))];

            // Agent Framework owns function orchestration; no application tool loop.
            BudgetChatClient budget = new(client, state, tools.OfType<AIFunction>().Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal));
            FunctionInvokingChatClient invocation = new(budget)
            {
                MaximumIterationsPerRequest = options.MaximumModelCalls,
                AllowConcurrentInvocation = false,
                IncludeDetailedErrors = false
            };
            ChatClientAgent agent = new(invocation, new ChatClientAgentOptions { UseProvidedChatClientAsIs = true });
            ChatClientAgentRunOptions run = new()
            {
                ChatOptions = new()
                {
                    ModelId = profile.Model,
                    Temperature = options.Temperature,
                    MaxOutputTokens = profile.SupportsOutputTokenLimit ? options.MaximumOutputTokens : null,
                    AllowMultipleToolCalls = false,
                    Tools = tools,
                    ResponseFormat = options.UseJsonSchema
                        ? ChatResponseFormat.ForJsonSchema(JsonSerializer.SerializeToElement(new
                        {
                            type = "object",
                            properties = new { documentJson = new { type = "string" } },
                            required = new[] { "documentJson" },
                            additionalProperties = false
                        }), "intent_output")
                        : ChatResponseFormat.Json
                }
            };

            for (int attempt = 0; attempt <= options.MaximumCorrections; attempt++)
            {
                state.Corrections = attempt;
                await foreach (var _ in agent.RunStreamingAsync(messages, options: run, cancellationToken: token)) { }
                token.ThrowIfCancellationRequested();
                try
                {
                    // The model supplies presentation only; it never supplies its own authoritative data.
                    using JsonDocument envelope = JsonDocument.Parse(state.Text.ToString());
                    if (envelope.RootElement.ValueKind != JsonValueKind.Object ||
                        envelope.RootElement.EnumerateObject().Count() != 1 ||
                        !envelope.RootElement.TryGetProperty("documentJson", out JsonElement value) || value.ValueKind != JsonValueKind.String)
                        throw new JsonException("Expected documentJson.");
                    OutputDocument document = OutputJson.ReadDocument(value.GetString()!);
                    OutputContent content = new()
                    {
                        Document = document,
                        Data = document.DataSets.Where(state.Data.ContainsKey).Select(id => state.Data[id]).ToImmutableArray()
                    };
                    OperationResult result = validator.Validate(content);
                    if (result.Success)
                    {
                        status = "completed";
                        return content;
                    }
                    Correct(string.Join(", ", result.Errors.Select(error => $"{error.Code} at {error.Path}: {error.Message}")));
                }
                catch (JsonException) { Correct("Return exactly {documentJson:string}, containing a valid schema 0.1 presentation document."); }
            }
            status = "invalid-output";
            throw new InputRejectedException("The model could not produce a valid output document within the correction limit.");

            void Correct(string error)
            {
                if (state.Corrections == options.MaximumCorrections) return;
                messages.Add(new(ChatRole.Assistant, state.Text.ToString()));
                messages.Add(new(ChatRole.User, "The validator rejected this proposal: " + error + " Correct the presentation only; use the same authorized data."));
                state.Report("Correcting the proposed document");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            status = "timeout";
            throw new InputRejectedException("The model request timed out. The current workspace is unchanged.");
        }
        catch (OperationCanceledException) { status = "cancelled"; throw; }
        finally
        {
            LastRun = new(status, profile.Provider, profile.Model, state.ModelCalls, state.ToolCalls, state.Corrections,
                state.Characters, state.InputTokens, state.OutputTokens, elapsed.Elapsed, state.TotalTokens);
        }

        void Accept(OutputDataSet set)
        {
            token.ThrowIfCancellationRequested();
            if (state.Data.ContainsKey(set.Id)) throw new InputRejectedException("Data sources must return unique dataset IDs within a request.");
            OutputContent probe = new()
            {
                Document = new()
                {
                    Id = "host-data",
                    RootId = "root",
                    DataSets = [.. state.Data.Keys, set.Id],
                    Nodes = [new() { Id = "root", Kind = NodeKind.Text, Value = ValueBinding.Constant("Data validation") }]
                },
                Data = [.. state.Data.Values, set]
            };
            if (!validator.Validate(probe).Success) throw new InputRejectedException("An application data source returned an invalid or oversized dataset.");
            state.Data.Add(set.Id, set);
        }
    }

    private sealed class SilentProgress : IProgress<InputProgress>
    {
        public static SilentProgress Instance { get; } = new();
        public void Report(InputProgress value) { }
    }
}

internal sealed class RunState(ModelOptions options, IProgress<InputProgress> progress)
{
    public ModelOptions Options { get; } = options;
    public Dictionary<string, OutputDataSet> Data { get; } = new(StringComparer.Ordinal);
    public StringBuilder Text { get; } = new();
    public int ModelCalls, ToolCalls, Corrections, Characters;
    public bool ToolLimitHit;
    public long? InputTokens, OutputTokens, TotalTokens;
    public void Report(string stage) => progress.Report(new(stage, ModelCalls, ToolCalls, Characters));
}

internal sealed class BudgetChatClient(IChatClient inner, RunState state, HashSet<string> toolNames) : DelegatingChatClient(inner)
{
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatMessage[] materialized = messages.ToArray();
        int context = materialized.Sum(message => message.Text.Length + message.Contents.Sum(ContentLength)) +
            (options?.Tools?.OfType<AIFunction>().Sum(tool => tool.JsonSchema.GetRawText().Length + tool.Description.Length) ?? 0);
        if (context > state.Options.MaximumContextCharacters) throw new InputRejectedException("The model context exceeds the configured character budget.");
        if (state.ToolLimitHit) throw new InputRejectedException("The data-call limit was reached.");
        if (state.ModelCalls >= state.Options.MaximumModelCalls) throw new InputRejectedException("The model-call limit was reached.");
        state.ModelCalls++;
        state.Text.Clear();
        state.Report("Generating the output document");
        await foreach (ChatResponseUpdate update in InnerClient.GetStreamingResponseAsync(materialized, options, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (FunctionCallContent call in update.Contents.OfType<FunctionCallContent>())
                if (!toolNames.Contains(call.Name)) throw new InputRejectedException("The model requested an unregistered data tool.");
            state.Text.Append(update.Text);
            state.Characters += update.Text.Length;
            if (state.Text.Length > OutputJson.MaximumBytes) throw new InputRejectedException("The model output exceeded the size limit.");
            foreach (UsageContent usage in update.Contents.OfType<UsageContent>())
            {
                if (usage.Details.InputTokenCount < 0 || usage.Details.OutputTokenCount < 0 || usage.Details.TotalTokenCount < 0)
                    throw new InputRejectedException("The provider reported invalid token usage.");
                if (usage.Details.InputTokenCount is { } input) state.InputTokens = (state.InputTokens ?? 0) + input;
                if (usage.Details.OutputTokenCount is { } output) state.OutputTokens = (state.OutputTokens ?? 0) + output;
                long? total = usage.Details.TotalTokenCount ??
                    (usage.Details.InputTokenCount.HasValue || usage.Details.OutputTokenCount.HasValue
                        ? checked((usage.Details.InputTokenCount ?? 0) + (usage.Details.OutputTokenCount ?? 0)) : null);
                if (total is { } count) state.TotalTokens = checked((state.TotalTokens ?? 0) + count);
                if (state.TotalTokens > state.Options.MaximumReportedTokens)
                    throw new InputRejectedException("The reported token budget was reached.");
            }
            state.Report("Receiving the proposed document");
            yield return update;
        }
    }

    private static int ContentLength(AIContent content) => content switch
    {
        FunctionResultContent result => JsonSerializer.Serialize(result.Result, ModelJson.Options).Length,
        FunctionCallContent call => JsonSerializer.Serialize(call.Arguments, ModelJson.Options).Length,
        _ => 0
    };
}
