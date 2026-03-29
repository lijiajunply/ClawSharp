namespace ClawSharp.Lib.Runtime;

/// <summary>
/// 基于 EF Core SQLite 的 session 记录存储实现。
/// </summary>
public sealed class SqliteSessionStore : ISessionStore
{
    private readonly ISessionRecordRepository _repository;

    /// <summary>
    /// 创建一个 SQLite session store，并确保所需表结构存在。
    /// </summary>
    /// <param name="options">用于解析数据库路径的库配置。</param>
    public SqliteSessionStore(Configuration.ClawOptions options)
        : this(new EfSessionRecordRepository(new ClawSqliteDbContextFactory(options)))
    {
    }

    internal SqliteSessionStore(ISessionRecordRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default) =>
        _repository.CreateAsync(session, cancellationToken);

    /// <inheritdoc />
    public Task<SessionRecord?> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionRecord>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        _repository.ListActiveAsync(cancellationToken);

    /// <inheritdoc />
    public Task UpdateStatusAsync(SessionId sessionId, SessionStatus status, DateTimeOffset? endedAt = null, CancellationToken cancellationToken = default) =>
        _repository.UpdateStatusAsync(sessionId, status, endedAt, cancellationToken);
}

/// <summary>
/// 基于 EF Core SQLite 的 prompt 历史存储实现。
/// </summary>
public sealed class SqlitePromptHistoryStore : IPromptHistoryStore
{
    private readonly IPromptMessageRepository _repository;

    /// <summary>
    /// 创建一个 SQLite prompt 历史 store，并确保所需表结构存在。
    /// </summary>
    /// <param name="options">用于解析数据库路径的库配置。</param>
    public SqlitePromptHistoryStore(Configuration.ClawOptions options)
        : this(new EfPromptMessageRepository(new ClawSqliteDbContextFactory(options)))
    {
    }

    internal SqlitePromptHistoryStore(IPromptMessageRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public Task<PromptMessage> AppendAsync(SessionId sessionId, TurnId turnId, PromptMessageRole role, string content, string? name = null, string? toolCallId = null, CancellationToken cancellationToken = default) =>
        _repository.AppendAsync(sessionId, turnId, role, content, name, toolCallId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PromptMessage>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default) =>
        _repository.ListAsync(sessionId, cancellationToken);
}

/// <summary>
/// 基于 EF Core SQLite 的 session 事件存储实现。
/// </summary>
public sealed class SqliteSessionEventStore : ISessionEventStore
{
    private readonly ISessionEventRepository _repository;

    /// <summary>
    /// 创建一个 SQLite session event store，并确保所需表结构存在。
    /// </summary>
    /// <param name="options">用于解析数据库路径的库配置。</param>
    public SqliteSessionEventStore(Configuration.ClawOptions options)
        : this(new EfSessionEventRepository(new ClawSqliteDbContextFactory(options)))
    {
    }

    internal SqliteSessionEventStore(ISessionEventRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public Task<SessionEvent> AppendAsync(SessionId sessionId, TurnId turnId, string eventType, System.Text.Json.JsonElement payload, CancellationToken cancellationToken = default) =>
        _repository.AppendAsync(sessionId, turnId, eventType, payload, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionEvent>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default) =>
        _repository.ListAsync(sessionId, cancellationToken);
}

/// <summary>
/// 默认的 session 生命周期管理器。
/// </summary>
/// <param name="sessions">底层 session store。</param>
public sealed class SessionManager(ISessionStore sessions) : ISessionManager
{
    /// <inheritdoc />
    public async Task<RuntimeSession> StartAsync(string agentId, string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var record = new SessionRecord(SessionId.New(), agentId, workspaceRoot, SessionStatus.Created, DateTimeOffset.UtcNow);
        await sessions.CreateAsync(record, cancellationToken).ConfigureAwait(false);
        return new RuntimeSession(record, null, null);
    }

    /// <inheritdoc />
    public async Task<RuntimeSession> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        var record = await sessions.GetAsync(sessionId, cancellationToken).ConfigureAwait(false)
                     ?? throw new KeyNotFoundException($"Session '{sessionId}' was not found.");
        return new RuntimeSession(record, null, null);
    }

    /// <inheritdoc />
    public Task CancelAsync(SessionId sessionId, CancellationToken cancellationToken = default) =>
        sessions.UpdateStatusAsync(sessionId, SessionStatus.Cancelled, DateTimeOffset.UtcNow, cancellationToken);

    /// <inheritdoc />
    public Task CompleteAsync(SessionId sessionId, SessionStatus status, CancellationToken cancellationToken = default) =>
        sessions.UpdateStatusAsync(sessionId, status, DateTimeOffset.UtcNow, cancellationToken);
}
