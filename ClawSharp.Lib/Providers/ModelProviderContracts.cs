using System.Net;
using System.Runtime.CompilerServices;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Providers;

/// <summary>
/// 模型消息角色。
/// </summary>
public enum ModelMessageRole
{
    /// <summary>
    /// 系统消息。
    /// </summary>
    System,

    /// <summary>
    /// 用户消息。
    /// </summary>
    User,

    /// <summary>
    /// assistant 消息。
    /// </summary>
    Assistant,

    /// <summary>
    /// 工具结果消息。
    /// </summary>
    Tool
}

/// <summary>
/// 模型停止生成的原因。
/// </summary>
public enum ModelStopReason
{
    /// <summary>
    /// 正常完成。
    /// </summary>
    Completed,

    /// <summary>
    /// 因为触发工具调用而暂停。
    /// </summary>
    ToolCall,

    /// <summary>
    /// 因长度限制而停止。
    /// </summary>
    Length,

    /// <summary>
    /// 因错误而停止。
    /// </summary>
    Error,

    /// <summary>
    /// 因取消而停止。
    /// </summary>
    Cancelled
}

/// <summary>
/// 描述一个 provider 实现的能力元数据。
/// </summary>
/// <param name="Name">provider 实现名。</param>
/// <param name="DisplayName">展示名称。</param>
/// <param name="SupportsStreaming">是否支持流式输出。</param>
/// <param name="SupportsTools">是否支持工具调用。</param>
public sealed record ModelProviderMetadata(
    string Name,
    string DisplayName,
    bool SupportsStreaming,
    bool SupportsTools);

/// <summary>
/// 表示已解析完成、可直接发请求的 provider 目标。
/// </summary>
/// <param name="ProviderName">配置名。</param>
/// <param name="ProviderType">provider 实现类型。</param>
/// <param name="BaseUrl">基础地址。</param>
/// <param name="Model">模型名。</param>
/// <param name="RequestPath">可选请求路径覆盖。</param>
/// <param name="Organization">可选组织标识。</param>
/// <param name="Project">可选项目标识。</param>
/// <param name="SupportsResponses">是否支持 Responses API 语义。</param>
/// <param name="SupportsChatCompletions">是否支持 Chat Completions 语义。</param>
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

/// <summary>
/// 表示 provider 实例与其目标配置的绑定结果。
/// </summary>
/// <param name="Provider">provider 实例。</param>
/// <param name="Target">解析后的 provider 目标。</param>
public sealed record ResolvedModelProvider(IModelProvider Provider, ResolvedModelTarget Target);

/// <summary>
/// 提供给模型的工具 schema 描述。
/// </summary>
/// <param name="Name">工具名。</param>
/// <param name="Description">工具描述。</param>
/// <param name="JsonSchema">可选 JSON Schema。</param>
public sealed record ModelToolSchema(string Name, string Description, string? JsonSchema = null);

/// <summary>
/// 模型发起的一次工具调用。
/// </summary>
/// <param name="Id">tool call 标识。</param>
/// <param name="Name">工具名。</param>
/// <param name="ArgumentsJson">参数 JSON。</param>
public sealed record ModelToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>
/// 模型使用量统计。
/// </summary>
/// <param name="InputTokens">输入 token 数。</param>
/// <param name="OutputTokens">输出 token 数。</param>
/// <param name="TotalTokens">总 token 数。</param>
public sealed record ModelUsage(int InputTokens, int OutputTokens, int TotalTokens);

/// <summary>
/// 发送给模型的一段结构化内容块。
/// </summary>
public abstract record ModelContentBlock;

/// <summary>
/// 纯文本内容块。
/// </summary>
/// <param name="Text">文本内容。</param>
public sealed record ModelTextBlock(string Text) : ModelContentBlock;

/// <summary>
/// assistant 发起的工具调用内容块。
/// </summary>
/// <param name="Id">tool call 标识。</param>
/// <param name="Name">工具名。</param>
/// <param name="ArgumentsJson">工具参数 JSON。</param>
public sealed record ModelToolUseBlock(string Id, string Name, string ArgumentsJson) : ModelContentBlock;

/// <summary>
/// 工具执行结果内容块。
/// </summary>
/// <param name="ToolCallId">对应的 tool call 标识。</param>
/// <param name="Content">工具结果内容。</param>
/// <param name="ToolName">可选工具名。</param>
public sealed record ModelToolResultBlock(string ToolCallId, string Content, string? ToolName = null) : ModelContentBlock;

/// <summary>
/// 发送给模型的一条消息。
/// </summary>
/// <param name="Role">消息角色。</param>
/// <param name="Blocks">结构化内容块列表。</param>
public sealed record ModelMessage(ModelMessageRole Role, IReadOnlyList<ModelContentBlock> Blocks)
{
    /// <summary>
    /// 用单段文本创建消息。
    /// </summary>
    public ModelMessage(ModelMessageRole role, string content)
        : this(role, [new ModelTextBlock(content)])
    {
    }

    /// <summary>
    /// 创建 assistant 工具调用消息。
    /// </summary>
    public static ModelMessage AssistantToolUse(string id, string name, string argumentsJson) =>
        new(ModelMessageRole.Assistant, [new ModelToolUseBlock(id, name, argumentsJson)]);

    /// <summary>
    /// 创建工具结果消息。
    /// </summary>
    public static ModelMessage ToolResult(string toolCallId, string content, string? toolName = null) =>
        new(ModelMessageRole.Tool, [new ModelToolResultBlock(toolCallId, content, toolName)]);

    /// <summary>
    /// 返回消息中的所有文本块拼接结果。
    /// </summary>
    public string TextContent => string.Concat(Blocks.OfType<ModelTextBlock>().Select(block => block.Text));

    /// <summary>
    /// 返回消息中的第一个工具调用块。
    /// </summary>
    public ModelToolUseBlock? ToolUse => Blocks.OfType<ModelToolUseBlock>().FirstOrDefault();

    /// <summary>
    /// 返回消息中的第一个工具结果块。
    /// </summary>
    public ModelToolResultBlock? ToolResultBlock => Blocks.OfType<ModelToolResultBlock>().FirstOrDefault();
}

/// <summary>
/// 发送给 provider 的完整请求。
/// </summary>
/// <param name="Target">已解析的 provider 目标。</param>
/// <param name="SessionId">session 标识。</param>
/// <param name="TraceId">追踪标识。</param>
/// <param name="Messages">输入消息列表。</param>
/// <param name="Tools">可用工具列表。</param>
/// <param name="SystemPrompt">可选系统提示词。</param>
/// <param name="Temperature">可选温度。</param>
/// <param name="MaxTokens">可选输出 token 上限。</param>
/// <param name="ToolChoice">工具选择策略。</param>
/// <param name="Metadata">可选元数据。</param>
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

/// <summary>
/// provider 流式输出的一段响应。
/// </summary>
/// <param name="TextDelta">文本增量。</param>
/// <param name="ToolCall">工具调用信息。</param>
/// <param name="Usage">使用量统计。</param>
/// <param name="StopReason">停止原因。</param>
/// <param name="Error">错误文本。</param>
public sealed record ModelResponseChunk(
    string? TextDelta = null,
    ModelToolCall? ToolCall = null,
    ModelUsage? Usage = null,
    ModelStopReason? StopReason = null,
    string? Error = null);

/// <summary>
/// 聚合后的完整模型响应。
/// </summary>
/// <param name="ProviderName">provider 配置名。</param>
/// <param name="Model">模型名。</param>
/// <param name="Content">最终文本内容。</param>
/// <param name="ToolCalls">聚合出的工具调用列表。</param>
/// <param name="Usage">最终使用量统计。</param>
/// <param name="StopReason">最终停止原因。</param>
public sealed record ModelResponse(
    string ProviderName,
    string Model,
    string Content,
    IReadOnlyList<ModelToolCall> ToolCalls,
    ModelUsage? Usage,
    ModelStopReason StopReason);

/// <summary>
/// provider 调用失败时抛出的异常。
/// </summary>
public sealed class ModelProviderException(
    string message,
    string providerName,
    HttpStatusCode? statusCode = null,
    string? providerErrorCode = null,
    bool isRetryable = false,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// 发生错误的 provider 名称。
    /// </summary>
    public string ProviderName { get; } = providerName;

    /// <summary>
    /// provider 返回的 HTTP 状态码。
    /// </summary>
    public HttpStatusCode? StatusCode { get; } = statusCode;

    /// <summary>
    /// provider 错误码。
    /// </summary>
    public string? ProviderErrorCode { get; } = providerErrorCode;

    /// <summary>
    /// 是否建议重试。
    /// </summary>
    public bool IsRetryable { get; } = isRetryable;
}

/// <summary>
/// 模型 provider 抽象。
/// </summary>
public interface IModelProvider
{
    /// <summary>
    /// provider 能力元数据。
    /// </summary>
    ModelProviderMetadata Metadata { get; }

    /// <summary>
    /// 流式执行一次模型请求。
    /// </summary>
    /// <param name="request">模型请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>响应 chunk 流。</returns>
    IAsyncEnumerable<ModelResponseChunk> StreamAsync(ModelRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将流式输出聚合为一个完整响应。
    /// </summary>
    /// <param name="request">模型请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>聚合后的完整响应。</returns>
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

/// <summary>
/// provider 实现注册表。
/// </summary>
public interface IModelProviderRegistry
{
    /// <summary>
    /// 按实现类型获取 provider。
    /// </summary>
    /// <param name="providerType">provider 实现类型。</param>
    /// <returns>匹配的 provider 实例。</returns>
    IModelProvider Get(string providerType);

    /// <summary>
    /// 获取所有已注册 provider 的元数据。
    /// </summary>
    /// <returns>provider 元数据集合。</returns>
    IReadOnlyCollection<ModelProviderMetadata> GetAll();
}

/// <summary>
/// 负责将 agent 与配置解析为具体 provider 调用目标。
/// </summary>
public interface IModelProviderResolver
{
    /// <summary>
    /// 为指定 agent 解析 provider 与目标配置。
    /// </summary>
    /// <param name="agent">agent 定义。</param>
    /// <returns>解析后的 provider 绑定结果。</returns>
    ResolvedModelProvider Resolve(AgentDefinition agent);
}

/// <summary>
/// 默认的 provider 注册表实现。
/// </summary>
/// <param name="providers">要注册的 provider 集合。</param>
public sealed class ModelProviderRegistry(IEnumerable<IModelProvider> providers) : IModelProviderRegistry
{
    private readonly Dictionary<string, IModelProvider> _providers =
        providers.ToDictionary(provider => provider.Metadata.Name, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IModelProvider Get(string providerType)
    {
        if (_providers.TryGetValue(providerType, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"Model provider implementation '{providerType}' was not found.");
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ModelProviderMetadata> GetAll() => _providers.Values.Select(x => x.Metadata).ToArray();
}

/// <summary>
/// 默认的 provider 解析器。
/// </summary>
/// <param name="options">provider 配置来源。</param>
/// <param name="registry">provider 注册表。</param>
public sealed class ModelProviderResolver(ClawOptions options, IModelProviderRegistry registry) : IModelProviderResolver
{
    /// <inheritdoc />
    public ResolvedModelProvider Resolve(AgentDefinition agent)
    {
        var configuredProvider = string.IsNullOrWhiteSpace(agent.Provider)
            ? options.Providers.DefaultProvider
            : agent.Provider;
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

/// <summary>
/// 用于测试和离线回退的 stub provider。
/// </summary>
public sealed class StubModelProvider : IModelProvider
{
    /// <inheritdoc />
    public ModelProviderMetadata Metadata { get; } = new("stub", "Stub Provider", true, true);

    /// <inheritdoc />
    public async IAsyncEnumerable<ModelResponseChunk> StreamAsync(
        ModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var lastMessage = request.Messages.LastOrDefault(message => message.Role is ModelMessageRole.User or ModelMessageRole.Tool);
        var lastUser = request.Messages.LastOrDefault(message => message.Role == ModelMessageRole.User);

        if (lastMessage?.Role == ModelMessageRole.Tool)
        {
            var toolResult = lastMessage.ToolResultBlock?.Content ?? lastMessage.TextContent;
            yield return new ModelResponseChunk(TextDelta: $"Tool result received: {toolResult}");
            yield return new ModelResponseChunk(Usage: new ModelUsage(request.Messages.Count, 4, request.Messages.Count + 4), StopReason: ModelStopReason.Completed);
            yield break;
        }

        if (lastUser is not null &&
            lastUser.TextContent.StartsWith("tool:", StringComparison.OrdinalIgnoreCase) &&
            request.Tools.Count > 0)
        {
            var remainder = lastUser.TextContent["tool:".Length..];
            var parts = remainder.Split(':', 2);
            var toolName = parts[0].Trim();
            var argumentsJson = parts.Length > 1 ? parts[1].Trim() : "{}";
            yield return new ModelResponseChunk(ToolCall: new ModelToolCall(Guid.NewGuid().ToString("N"), toolName, argumentsJson));
            yield return new ModelResponseChunk(Usage: new ModelUsage(request.Messages.Count, 1, request.Messages.Count + 1), StopReason: ModelStopReason.ToolCall);
            yield break;
        }

        var content = lastUser?.TextContent ?? "No prompt supplied.";
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
