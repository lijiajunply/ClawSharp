using System.Runtime.CompilerServices;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Providers;

public enum ModelMessageRole
{
    System,
    User,
    Assistant,
    Tool
}

public enum ModelStopReason
{
    Completed,
    ToolCall,
    Length,
    Error,
    Cancelled
}

public sealed record ModelProviderMetadata(string Name, string DisplayName, bool SupportsStreaming, bool SupportsTools);

public sealed record ModelToolSchema(string Name, string Description, string? JsonSchema = null);

public sealed record ModelToolCall(string Id, string Name, string ArgumentsJson);

public sealed record ModelUsage(int InputTokens, int OutputTokens, int TotalTokens);

public sealed record ModelMessage(
    ModelMessageRole Role,
    string Content,
    string? Name = null,
    string? ToolCallId = null);

public sealed record ModelRequest(
    string ProviderName,
    string Model,
    string SessionId,
    string TraceId,
    IReadOnlyList<ModelMessage> Messages,
    IReadOnlyList<ModelToolSchema> Tools,
    string? SystemPrompt = null,
    double? Temperature = null,
    int? MaxTokens = null);

public sealed record ModelResponseChunk(
    string? TextDelta = null,
    ModelToolCall? ToolCall = null,
    ModelUsage? Usage = null,
    ModelStopReason? StopReason = null,
    string? Error = null);

public sealed record ModelResponse(
    string ProviderName,
    string Model,
    string Content,
    IReadOnlyList<ModelToolCall> ToolCalls,
    ModelUsage? Usage,
    ModelStopReason StopReason);

public interface IModelProvider
{
    ModelProviderMetadata Metadata { get; }

    IAsyncEnumerable<ModelResponseChunk> StreamAsync(ModelRequest request, CancellationToken cancellationToken = default);

    async Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken = default)
    {
        var text = new List<string>();
        var toolCalls = new List<ModelToolCall>();
        ModelUsage? usage = null;
        var stopReason = ModelStopReason.Completed;

        await foreach (var chunk in StreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(chunk.TextDelta))
            {
                text.Add(chunk.TextDelta);
            }

            if (chunk.ToolCall is not null)
            {
                toolCalls.Add(chunk.ToolCall);
            }

            usage = chunk.Usage ?? usage;
            stopReason = chunk.StopReason ?? stopReason;
        }

        return new ModelResponse(
            Metadata.Name,
            request.Model,
            string.Concat(text),
            toolCalls,
            usage,
            stopReason);
    }
}

public interface IModelProviderRegistry
{
    IModelProvider Get(string name);

    IReadOnlyCollection<ModelProviderMetadata> GetAll();
}

public interface IModelProviderResolver
{
    IModelProvider Resolve(AgentDefinition agent);
}

public sealed class ModelProviderRegistry(IEnumerable<IModelProvider> providers) : IModelProviderRegistry
{
    private readonly Dictionary<string, IModelProvider> _providers =
        providers.ToDictionary(provider => provider.Metadata.Name, StringComparer.OrdinalIgnoreCase);

    public IModelProvider Get(string name)
    {
        if (_providers.TryGetValue(name, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"Model provider '{name}' was not found.");
    }

    public IReadOnlyCollection<ModelProviderMetadata> GetAll() => _providers.Values.Select(x => x.Metadata).ToArray();
}

public sealed class ModelProviderResolver(ClawOptions options, IModelProviderRegistry registry) : IModelProviderResolver
{
    public IModelProvider Resolve(AgentDefinition agent)
    {
        var configured = options.Providers.DefaultProvider;
        return registry.Get(configured);
    }
}

public sealed class StubModelProvider : IModelProvider
{
    public ModelProviderMetadata Metadata { get; } = new("stub", "Stub Provider", true, true);

    public async IAsyncEnumerable<ModelResponseChunk> StreamAsync(
        ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var lastMessage = request.Messages.LastOrDefault(message => message.Role is ModelMessageRole.User or ModelMessageRole.Tool);
        var lastUser = request.Messages.LastOrDefault(message => message.Role == ModelMessageRole.User);

        if (lastMessage?.Role == ModelMessageRole.Tool)
        {
            yield return new ModelResponseChunk(TextDelta: $"Tool result received: {lastMessage.Content}");
            yield return new ModelResponseChunk(Usage: new ModelUsage(request.Messages.Count, 4, request.Messages.Count + 4), StopReason: ModelStopReason.Completed);
            yield break;
        }

        if (lastUser is not null &&
            lastUser.Content.StartsWith("tool:", StringComparison.OrdinalIgnoreCase) &&
            request.Tools.Count > 0)
        {
            var remainder = lastUser.Content["tool:".Length..];
            var parts = remainder.Split(':', 2);
            var toolName = parts[0].Trim();
            var argumentsJson = parts.Length > 1 ? parts[1].Trim() : "{}";
            yield return new ModelResponseChunk(ToolCall: new ModelToolCall(Guid.NewGuid().ToString("N"), toolName, argumentsJson));
            yield return new ModelResponseChunk(Usage: new ModelUsage(request.Messages.Count, 1, request.Messages.Count + 1), StopReason: ModelStopReason.ToolCall);
            yield break;
        }

        var content = lastUser?.Content ?? "No prompt supplied.";
        string[] segments = content.Length <= 8
            ? [content]
            : [content[..Math.Min(8, content.Length)], content[Math.Min(8, content.Length)..]];

        foreach (var segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ModelResponseChunk(TextDelta: segment);
        }

        yield return new ModelResponseChunk(
            Usage: new ModelUsage(request.Messages.Count, segments.Sum(x => x.Length), request.Messages.Count + segments.Sum(x => x.Length)),
            StopReason: ModelStopReason.Completed);
    }
}
