using System.Net;
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

public sealed record ModelProviderMetadata(
    string Name,
    string DisplayName,
    bool SupportsStreaming,
    bool SupportsTools);

public sealed record ResolvedModelTarget(
    string ProviderName,
    string ProviderType,
    string BaseUrl,
    string Model,
    string? RequestPath = null,
    string? Organization = null,
    string? Project = null,
    bool SupportsResponses = false,
    bool SupportsChatCompletions = false);

public sealed record ResolvedModelProvider(IModelProvider Provider, ResolvedModelTarget Target);

public sealed record ModelToolSchema(string Name, string Description, string? JsonSchema = null);

public sealed record ModelToolCall(string Id, string Name, string ArgumentsJson);

public sealed record ModelUsage(int InputTokens, int OutputTokens, int TotalTokens);

public sealed record ModelMessage(
    ModelMessageRole Role,
    string Content,
    string? Name = null,
    string? ToolCallId = null);

public sealed record ModelRequest(
    ResolvedModelTarget Target,
    string SessionId,
    string TraceId,
    IReadOnlyList<ModelMessage> Messages,
    IReadOnlyList<ModelToolSchema> Tools,
    string? SystemPrompt = null,
    double? Temperature = null,
    int? MaxTokens = null,
    string? ToolChoice = "auto",
    IReadOnlyDictionary<string, string>? Metadata = null);

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

public sealed class ModelProviderException(
    string message,
    string providerName,
    HttpStatusCode? statusCode = null,
    string? providerErrorCode = null,
    bool isRetryable = false,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string ProviderName { get; } = providerName;

    public HttpStatusCode? StatusCode { get; } = statusCode;

    public string? ProviderErrorCode { get; } = providerErrorCode;

    public bool IsRetryable { get; } = isRetryable;
}

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

            if (!string.IsNullOrWhiteSpace(chunk.Error))
            {
                stopReason = ModelStopReason.Error;
            }
        }

        return new ModelResponse(
            request.Target.ProviderName,
            request.Target.Model,
            string.Concat(text),
            toolCalls,
            usage,
            stopReason);
    }
}

public interface IModelProviderRegistry
{
    IModelProvider Get(string providerType);

    IReadOnlyCollection<ModelProviderMetadata> GetAll();
}

public interface IModelProviderResolver
{
    ResolvedModelProvider Resolve(AgentDefinition agent);
}

public sealed class ModelProviderRegistry(IEnumerable<IModelProvider> providers) : IModelProviderRegistry
{
    private readonly Dictionary<string, IModelProvider> _providers =
        providers.ToDictionary(provider => provider.Metadata.Name, StringComparer.OrdinalIgnoreCase);

    public IModelProvider Get(string providerType)
    {
        if (_providers.TryGetValue(providerType, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"Model provider implementation '{providerType}' was not found.");
    }

    public IReadOnlyCollection<ModelProviderMetadata> GetAll() => _providers.Values.Select(x => x.Metadata).ToArray();
}

public sealed class ModelProviderResolver(ClawOptions options, IModelProviderRegistry registry) : IModelProviderResolver
{
    public ResolvedModelProvider Resolve(AgentDefinition agent)
    {
        var configuredProvider = options.Providers.DefaultProvider;
        var providerConfig = options.Providers.Models.FirstOrDefault(model => string.Equals(model.Name, configuredProvider, StringComparison.OrdinalIgnoreCase))
                             ?? CreateFallback(configuredProvider, options);

        if (providerConfig is null)
        {
            throw new ModelProviderException(
                $"Configured provider '{configuredProvider}' was not found.",
                configuredProvider);
        }

        var model = !string.IsNullOrWhiteSpace(agent.Model)
            ? agent.Model
            : !string.IsNullOrWhiteSpace(providerConfig.DefaultModel)
                ? providerConfig.DefaultModel
                : options.Providers.DefaultModel;

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ModelProviderException(
                $"Provider '{providerConfig.Name}' does not define a model and agent '{agent.Id}' did not override one.",
                providerConfig.Name);
        }

        var provider = registry.Get(providerConfig.Type);
        var target = new ResolvedModelTarget(
            providerConfig.Name,
            providerConfig.Type,
            providerConfig.BaseUrl,
            model,
            providerConfig.RequestPath,
            providerConfig.Organization,
            providerConfig.Project,
            providerConfig.SupportsResponses,
            providerConfig.SupportsChatCompletions);

        return new ResolvedModelProvider(provider, target);
    }

    private static ModelProviderOptions? CreateFallback(string configuredProvider, ClawOptions options)
    {
        if (!string.Equals(configuredProvider, "stub", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new ModelProviderOptions
        {
            Name = "stub",
            Type = "stub",
            BaseUrl = "http://localhost/stub",
            DefaultModel = string.IsNullOrWhiteSpace(options.Providers.DefaultModel) ? "stub-model" : options.Providers.DefaultModel,
            SupportsResponses = true
        };
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
