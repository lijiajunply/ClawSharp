using System.Text.Json;

namespace ClawSharp.Lib.Runtime;

/// <summary>
/// session 标识值对象。
/// </summary>
/// <param name="Value">session 的字符串值。</param>
public readonly record struct SessionId(string Value)
{
    /// <summary>
    /// 返回底层字符串值。
    /// </summary>
    /// <returns>session 标识。</returns>
    public override string ToString() => Value;

    /// <summary>
    /// 创建一个新的 session 标识。
    /// </summary>
    /// <returns>新的 <see cref="SessionId"/>。</returns>
    public static SessionId New() => new(Guid.NewGuid().ToString("N"));
}

/// <summary>
/// turn 标识值对象。
/// </summary>
/// <param name="Value">turn 的字符串值。</param>
public readonly record struct TurnId(string Value)
{
    /// <summary>
    /// 返回底层字符串值。
    /// </summary>
    /// <returns>turn 标识。</returns>
    public override string ToString() => Value;

    /// <summary>
    /// 创建一个新的 turn 标识。
    /// </summary>
    /// <returns>新的 <see cref="TurnId"/>。</returns>
    public static TurnId New() => new(Guid.NewGuid().ToString("N"));
}

/// <summary>
/// 消息标识值对象。
/// </summary>
/// <param name="Value">消息的字符串值。</param>
public readonly record struct MessageId(string Value)
{
    /// <summary>
    /// 返回底层字符串值。
    /// </summary>
    /// <returns>消息标识。</returns>
    public override string ToString() => Value;

    /// <summary>
    /// 创建一个新的消息标识。
    /// </summary>
    /// <returns>新的 <see cref="MessageId"/>。</returns>
    public static MessageId New() => new(Guid.NewGuid().ToString("N"));
}

/// <summary>
/// session 事件标识值对象。
/// </summary>
/// <param name="Value">事件的字符串值。</param>
public readonly record struct EventId(string Value)
{
    /// <summary>
    /// 返回底层字符串值。
    /// </summary>
    /// <returns>事件标识。</returns>
    public override string ToString() => Value;

    /// <summary>
    /// 创建一个新的事件标识。
    /// </summary>
    /// <returns>新的 <see cref="EventId"/>。</returns>
    public static EventId New() => new(Guid.NewGuid().ToString("N"));
}

/// <summary>
/// 表示 session 在生命周期中的状态。
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// session 已创建但尚未开始执行 turn。
    /// </summary>
    Created,

    /// <summary>
    /// session 正在执行 turn。
    /// </summary>
    Running,

    /// <summary>
    /// session 因工具审批而暂停。
    /// </summary>
    WaitingForApproval,

    /// <summary>
    /// 最近一次 turn 已完成。
    /// </summary>
    Completed,

    /// <summary>
    /// 最近一次 turn 执行失败。
    /// </summary>
    Failed,

    /// <summary>
    /// session 被主动取消。
    /// </summary>
    Cancelled
}

/// <summary>
/// prompt 历史中的消息角色。
/// </summary>
public enum PromptMessageRole
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
/// 描述一个 session 的持久化记录。
/// </summary>
/// <param name="SessionId">session 标识。</param>
/// <param name="AgentId">关联的 agent 标识。</param>
/// <param name="WorkspaceRoot">session 对应的 workspace 根目录。</param>
/// <param name="Status">当前 session 状态。</param>
/// <param name="StartedAt">创建时间。</param>
/// <param name="EndedAt">结束时间；未结束时为 <see langword="null"/>。</param>
public sealed record SessionRecord(
    SessionId SessionId,
    string AgentId,
    string WorkspaceRoot,
    SessionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt = null);

/// <summary>
/// 描述写入 prompt 历史的一条消息。
/// </summary>
/// <param name="MessageId">消息标识。</param>
/// <param name="SessionId">所属 session 标识。</param>
/// <param name="TurnId">所属 turn 标识。</param>
/// <param name="Role">消息角色。</param>
/// <param name="Content">消息内容。</param>
/// <param name="SequenceNo">在 session 历史中的顺序号。</param>
/// <param name="CreatedAt">创建时间。</param>
/// <param name="Name">关联名称，例如工具名。</param>
/// <param name="ToolCallId">关联的 tool call 标识。</param>
public sealed record PromptMessage(
    MessageId MessageId,
    SessionId SessionId,
    TurnId TurnId,
    PromptMessageRole Role,
    string Content,
    int SequenceNo,
    DateTimeOffset CreatedAt,
    string? Name = null,
    string? ToolCallId = null);

/// <summary>
/// 描述 session 中记录的一条结构化事件。
/// </summary>
/// <param name="EventId">事件标识。</param>
/// <param name="SessionId">所属 session 标识。</param>
/// <param name="TurnId">所属 turn 标识。</param>
/// <param name="EventType">事件类型名。</param>
/// <param name="Payload">事件 payload。</param>
/// <param name="SequenceNo">在 session 历史中的顺序号。</param>
/// <param name="CreatedAt">创建时间。</param>
public sealed record SessionEvent(
    EventId EventId,
    SessionId SessionId,
    TurnId TurnId,
    string EventType,
    JsonElement Payload,
    int SequenceNo,
    DateTimeOffset CreatedAt);

/// <summary>
/// 统一描述 prompt 历史中的消息项或事件项。
/// </summary>
/// <param name="SequenceNo">统一顺序号。</param>
/// <param name="Message">消息项；如果本条是事件则为 <see langword="null"/>。</param>
/// <param name="Event">事件项；如果本条是消息则为 <see langword="null"/>。</param>
public sealed record PromptHistoryEntry(
    int SequenceNo,
    PromptMessage? Message = null,
    SessionEvent? Event = null);

/// <summary>
/// 在字符串与 <see cref="JsonElement"/> 之间序列化 session 相关 payload 的抽象。
/// </summary>
public interface ISessionSerializer
{
    /// <summary>
    /// 将对象序列化为 JSON 字符串。
    /// </summary>
    /// <typeparam name="T">对象类型。</typeparam>
    /// <param name="value">要序列化的值。</param>
    /// <returns>JSON 字符串。</returns>
    string Serialize<T>(T value);

    /// <summary>
    /// 将对象序列化为 <see cref="JsonElement"/>。
    /// </summary>
    /// <typeparam name="T">对象类型。</typeparam>
    /// <param name="value">要序列化的值。</param>
    /// <returns>序列化后的 JSON 元素。</returns>
    JsonElement SerializeToElement<T>(T value);

    /// <summary>
    /// 从 JSON 字符串反序列化对象。
    /// </summary>
    /// <typeparam name="T">目标类型。</typeparam>
    /// <param name="json">JSON 文本。</param>
    /// <returns>反序列化后的对象；失败时可能返回 <see langword="null"/>。</returns>
    T? Deserialize<T>(string json);
}

/// <summary>
/// session 记录存储抽象。
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// 创建一条新的 session 记录。
    /// </summary>
    /// <param name="session">要保存的 session。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按标识读取 session 记录。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>匹配的 session；不存在时返回 <see langword="null"/>。</returns>
    Task<SessionRecord?> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出仍处于活跃状态的 session。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>活跃 session 列表。</returns>
    Task<IReadOnlyList<SessionRecord>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新 session 状态。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="status">新状态。</param>
    /// <param name="endedAt">结束时间；未结束时可为 <see langword="null"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpdateStatusAsync(SessionId sessionId, SessionStatus status, DateTimeOffset? endedAt = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// prompt 历史消息存储抽象。
/// </summary>
public interface IPromptHistoryStore
{
    /// <summary>
    /// 追加一条 prompt 历史消息。
    /// </summary>
    /// <param name="sessionId">所属 session。</param>
    /// <param name="turnId">所属 turn。</param>
    /// <param name="role">消息角色。</param>
    /// <param name="content">消息内容。</param>
    /// <param name="name">可选名称，例如工具名。</param>
    /// <param name="toolCallId">可选 tool call 标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已保存的消息对象。</returns>
    Task<PromptMessage> AppendAsync(SessionId sessionId, TurnId turnId, PromptMessageRole role, string content, string? name = null, string? toolCallId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出某个 session 的全部 prompt 历史消息。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按顺序排列的消息列表。</returns>
    Task<IReadOnlyList<PromptMessage>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// session 事件存储抽象。
/// </summary>
public interface ISessionEventStore
{
    /// <summary>
    /// 追加一条 session 事件。
    /// </summary>
    /// <param name="sessionId">所属 session。</param>
    /// <param name="turnId">所属 turn。</param>
    /// <param name="eventType">事件类型。</param>
    /// <param name="payload">事件 payload。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已保存的事件对象。</returns>
    Task<SessionEvent> AppendAsync(SessionId sessionId, TurnId turnId, string eventType, JsonElement payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出某个 session 的全部事件。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按顺序排列的事件列表。</returns>
    Task<IReadOnlyList<SessionEvent>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// session 生命周期管理抽象。
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// 为指定 agent 启动一个新的 session。
    /// </summary>
    /// <param name="agentId">agent 标识。</param>
    /// <param name="workspaceRoot">session 使用的 workspace 根目录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新建的 runtime session。</returns>
    Task<RuntimeSession> StartAsync(string agentId, string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取一个已存在的 session。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>对应的 runtime session。</returns>
    Task<RuntimeSession> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消一个 session。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task CancelAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以指定状态结束一个 session。
    /// </summary>
    /// <param name="sessionId">session 标识。</param>
    /// <param name="status">完成后的最终状态。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task CompleteAsync(SessionId sessionId, SessionStatus status, CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于 <see cref="System.Text.Json"/> 的默认 session 序列化器。
/// </summary>
public sealed class JsonSessionSerializer : ISessionSerializer
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, _options);

    /// <inheritdoc />
    public JsonElement SerializeToElement<T>(T value) => JsonSerializer.SerializeToElement(value, _options);

    /// <inheritdoc />
    public T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);
}
