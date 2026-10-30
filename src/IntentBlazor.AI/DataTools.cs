using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntentBlazor.Core;
using IntentBlazor.Runtime;
using Microsoft.Extensions.AI;

namespace IntentBlazor.AI;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AiDataToolAttribute(string name, string description) : Attribute
{
    public string Name { get; } = name;
    public string Description { get; } = description;
}

public sealed record DataToolRegistration(Type ServiceType, MethodInfo Method, string Name, string Description)
{
    internal static DataToolRegistration Create(Type service, string method, string name, string description)
    {
        MethodInfo[] matches = service.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(candidate => candidate.Name == method).ToArray();
        if (matches.Length != 1 || matches[0].ContainsGenericParameters ||
            name.Length is < 1 or > 64 || !char.IsAsciiLetter(name[0]) ||
            !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ||
            string.IsNullOrWhiteSpace(description) || description.Length > 500)
            throw new ArgumentException("Tools require one public instance method, a unique identifier, and a bounded description.");
        MethodInfo info = matches[0];
        Type result = info.ReturnType;
        if (result.IsGenericType && (result.GetGenericTypeDefinition() == typeof(Task<>) || result.GetGenericTypeDefinition() == typeof(ValueTask<>)))
            result = result.GenericTypeArguments[0];
        if (result != typeof(OutputDataSet) || info.GetParameters().Any(p => p.ParameterType.IsByRef ||
            !(p.ParameterType == typeof(string) || p.ParameterType == typeof(int) || p.ParameterType == typeof(bool) || p.ParameterType == typeof(CancellationToken))))
            throw new ArgumentException("Read-only data tools return OutputDataSet, Task<OutputDataSet>, or ValueTask<OutputDataSet>; parameters may be string, int, bool, or CancellationToken.");
        return new(service, info, name, description);
    }
}

internal sealed class DataFunction(AIFunction inner, Action<OutputDataSet> accept, Action count, Action<ModelToolObservation>? observe = null) : DelegatingAIFunction(inner)
{
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        count();
        try
        {
            JsonElement schema = JsonSchema;
            var parameters = schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            if (arguments.Keys.Any(name => !parameters.Contains(name)) ||
                (schema.TryGetProperty("required", out JsonElement required) &&
                 required.EnumerateArray().Any(name => !arguments.ContainsKey(name.GetString()!))) ||
                JsonSerializer.Serialize(arguments, ModelJson.Options).Length > 4096)
                throw new InvalidOperationException("The data tool arguments are invalid.");
            object? result = await InnerFunction.InvokeAsync(arguments, cancellationToken);
            if (result is not OutputDataSet data) throw new InvalidOperationException("A data tool returned no dataset.");
            accept(data);
            observe?.Invoke(new(Name, "completed", data.Id, data.Rows.Length, elapsed.Elapsed));
            return JsonSerializer.SerializeToElement(data, ModelJson.Options);
        }
        catch (OperationCanceledException) { observe?.Invoke(new(Name, "cancelled", null, null, elapsed.Elapsed)); throw; }
        catch (Exception error) when (error is not OutOfMemoryException)
        { observe?.Invoke(new(Name, "failed", null, null, elapsed.Elapsed)); throw new InvalidOperationException("The application data tool failed or returned invalid data."); }
    }
}

internal static class ModelJson
{
    public static JsonSerializerOptions Options { get; } = Create();
    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 32
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
