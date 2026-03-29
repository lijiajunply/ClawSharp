using System.Text.Json;

namespace ClawSharp.Lib.Runtime;

public readonly record struct SessionId(string Value)
{
    public override string ToString() => Value;
    public static SessionId New() => new(Guid.NewGuid().ToString("N"));
}

public readonly record struct TurnId(string Value)
{
    public override string ToString() => Value;
    public static TurnId New() => new(Guid.NewGuid().ToString("N"));
}

public readonly record struct MessageId(string Value)
{
    public override string ToString() => Value;
    public static MessageId New() => new(Guid.NewGuid().ToString("N"));
}

public readonly record struct EventId(string Value)
{
    public override string ToString() => Value;
    public static EventId New() => new(Guid.NewGuid().ToString("N"));
}

public enum SessionStatus
{
    Created,
    Running,
    WaitingForApproval,
    Completed,
    Failed,
    Cancelled
}

public enum PromptMessageRole
{
    System,
    User,
    Assistant,
    Tool
}

public sealed record SessionRecord(
    SessionId SessionId,
    string AgentId,
    string WorkspaceRoot,
    SessionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt = null);

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

public sealed record SessionEvent(
    EventId EventId,
    SessionId SessionId,
    TurnId TurnId,
    string EventType,
    JsonElement Payload,
    int SequenceNo,
    DateTimeOffset CreatedAt);

public sealed record PromptHistoryEntry(
    int SequenceNo,
    PromptMessage? Message = null,
    SessionEvent? Event = null);

public interface ISessionSerializer
{
    string Serialize<T>(T value);

    JsonElement SerializeToElement<T>(T value);

    T? Deserialize<T>(string json);
}

public interface ISessionStore
{
    Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default);

    Task<SessionRecord?> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionRecord>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(SessionId sessionId, SessionStatus status, DateTimeOffset? endedAt = null, CancellationToken cancellationToken = default);
}

public interface IPromptHistoryStore
{
    Task<PromptMessage> AppendAsync(SessionId sessionId, TurnId turnId, PromptMessageRole role, string content, string? name = null, string? toolCallId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromptMessage>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default);
}

public interface ISessionEventStore
{
    Task<SessionEvent> AppendAsync(SessionId sessionId, TurnId turnId, string eventType, JsonElement payload, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionEvent>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default);
}

public interface ISessionManager
{
    Task<RuntimeSession> StartAsync(string agentId, string workspaceRoot, CancellationToken cancellationToken = default);

    Task<RuntimeSession> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    Task CancelAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    Task CompleteAsync(SessionId sessionId, SessionStatus status, CancellationToken cancellationToken = default);
}

public sealed class JsonSessionSerializer : ISessionSerializer
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, _options);

    public JsonElement SerializeToElement<T>(T value) => JsonSerializer.SerializeToElement(value, _options);

    public T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _options);
}
