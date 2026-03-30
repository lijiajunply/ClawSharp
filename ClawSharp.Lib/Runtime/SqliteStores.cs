namespace ClawSharp.Lib.Runtime;

using ClawSharp.Lib.Providers;

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
        : this(SqlitePersistenceFactory.CreateSessionRecordRepository(options))
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
    public Task<IReadOnlyList<SessionRecord>> ListByThreadSpaceAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default) =>
        _repository.ListByThreadSpaceAsync(threadSpaceId, cancellationToken);

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
        : this(SqlitePersistenceFactory.CreatePromptMessageRepository(options))
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
    public Task<PromptMessage> AppendBlocksAsync(SessionId sessionId, TurnId turnId, PromptMessageRole role, IReadOnlyList<ModelContentBlock> blocks, CancellationToken cancellationToken = default) =>
        _repository.AppendBlocksAsync(sessionId, turnId, role, blocks, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PromptMessage>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default) =>
        _repository.ListAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task DeleteBySessionAsync(SessionId sessionId, CancellationToken cancellationToken = default) =>
        _repository.DeleteBySessionAsync(sessionId, cancellationToken);
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
        : this(SqlitePersistenceFactory.CreateSessionEventRepository(options))
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

    /// <inheritdoc />
    public Task DeleteBySessionAsync(SessionId sessionId, CancellationToken cancellationToken = default) =>
        _repository.DeleteBySessionAsync(sessionId, cancellationToken);
}

/// <summary>
/// 默认的 session 生命周期管理器。
/// </summary>
/// <param name="sessions">底层 session store。</param>
public sealed class SessionManager(ISessionStore sessions) : ISessionManager
{
    /// <inheritdoc />
    public async Task<RuntimeSession> StartAsync(string agentId, ThreadSpaceId threadSpaceId, string workspaceRoot, CancellationToken cancellationToken = default)
    {
        var record = new SessionRecord(SessionId.New(), threadSpaceId, agentId, workspaceRoot, SessionStatus.Created, DateTimeOffset.UtcNow);
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

/// <summary>
/// 基于 EF Core SQLite 的 ThreadSpace 存储实现。
/// </summary>
public sealed class SqliteThreadSpaceStore : IThreadSpaceStore
{
    private readonly IThreadSpaceRepository _repository;

    /// <summary>
    /// 创建一个 SQLite ThreadSpace store，并确保所需表结构存在。
    /// </summary>
    /// <param name="options">用于解析数据库路径的库配置。</param>
    public SqliteThreadSpaceStore(Configuration.ClawOptions options)
        : this(SqlitePersistenceFactory.CreateThreadSpaceRepository(options))
    {
    }

    internal SqliteThreadSpaceStore(IThreadSpaceRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public Task CreateAsync(ThreadSpaceRecord threadSpace, CancellationToken cancellationToken = default) =>
        _repository.CreateAsync(threadSpace, cancellationToken);

    /// <inheritdoc />
    public Task<ThreadSpaceRecord?> GetGlobalAsync(CancellationToken cancellationToken = default) =>
        _repository.GetGlobalAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ThreadSpaceRecord?> GetAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(threadSpaceId, cancellationToken);

    /// <inheritdoc />
    public Task<ThreadSpaceRecord?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        _repository.GetByNameAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task<ThreadSpaceRecord?> GetByBoundFolderPathAsync(string boundFolderPath, CancellationToken cancellationToken = default) =>
        _repository.GetByBoundFolderPathAsync(boundFolderPath, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ThreadSpaceRecord>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default) =>
        _repository.ListAsync(includeArchived, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAsync(ThreadSpaceRecord threadSpace, CancellationToken cancellationToken = default) =>
        _repository.UpdateAsync(threadSpace, cancellationToken);

    /// <inheritdoc />
    public Task ArchiveAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default) =>
        _repository.ArchiveAsync(threadSpaceId, DateTimeOffset.UtcNow, cancellationToken);
}

/// <summary>
/// 默认的 ThreadSpace 生命周期管理器。
/// </summary>
public sealed class ThreadSpaceManager(IThreadSpaceStore threadSpaces, ISessionStore sessions, Configuration.ClawOptions options) : IThreadSpaceManager
{
    private const string GlobalName = "global";

    /// <inheritdoc />
    public async Task<ThreadSpaceRecord> EnsureDefaultAsync(CancellationToken cancellationToken = default)
    {
        var existing = await threadSpaces.GetGlobalAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var global = new ThreadSpaceRecord(ThreadSpaceId.New(), GlobalName, null, true, DateTimeOffset.UtcNow);
        await threadSpaces.CreateAsync(global, cancellationToken).ConfigureAwait(false);
        return global;
    }

    /// <inheritdoc />
    public async Task<ThreadSpaceRecord> GetGlobalAsync(CancellationToken cancellationToken = default)
    {
        return await EnsureDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ThreadSpaceRecord> CreateAsync(CreateThreadSpaceRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();
        await EnsureDefaultAsync(cancellationToken).ConfigureAwait(false);

        var normalizedName = request.Name.Trim();
        if (string.Equals(normalizedName, GlobalName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("ThreadSpace name 'global' is reserved.");
        }

        if (await threadSpaces.GetByNameAsync(normalizedName, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException($"ThreadSpace '{normalizedName}' already exists.");
        }

        string? boundFolderPath = null;
        if (!string.IsNullOrWhiteSpace(request.BoundFolderPath))
        {
            boundFolderPath = NormalizeAndValidateBoundFolderPath(request.BoundFolderPath);
            Directory.CreateDirectory(boundFolderPath);

            if (await threadSpaces.GetByBoundFolderPathAsync(boundFolderPath, cancellationToken).ConfigureAwait(false) is not null)
            {
                throw new InvalidOperationException($"ThreadSpace for '{boundFolderPath}' already exists.");
            }
        }

        var threadSpace = new ThreadSpaceRecord(ThreadSpaceId.New(), normalizedName, boundFolderPath, false, DateTimeOffset.UtcNow);
        await threadSpaces.CreateAsync(threadSpace, cancellationToken).ConfigureAwait(false);
        return threadSpace;
    }

    /// <inheritdoc />
    public async Task<ThreadSpaceRecord> GetAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultAsync(cancellationToken).ConfigureAwait(false);
        return await threadSpaces.GetAsync(threadSpaceId, cancellationToken).ConfigureAwait(false)
               ?? throw new KeyNotFoundException($"ThreadSpace '{threadSpaceId}' was not found.");
    }

    /// <inheritdoc />
    public async Task<ThreadSpaceRecord?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultAsync(cancellationToken).ConfigureAwait(false);
        return await threadSpaces.GetByNameAsync(name, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ThreadSpaceRecord?> GetByBoundFolderPathAsync(string boundFolderPath, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultAsync(cancellationToken).ConfigureAwait(false);
        return await threadSpaces.GetByBoundFolderPathAsync(NormalizeBoundFolderPath(boundFolderPath), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ThreadSpaceRecord>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultAsync(cancellationToken).ConfigureAwait(false);
        return await threadSpaces.ListAsync(includeArchived, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ThreadSpaceRecord> UpdateAsync(ThreadSpaceId threadSpaceId, string? newName = null, string? newBoundFolderPath = null, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(threadSpaceId, cancellationToken).ConfigureAwait(false);
        if (existing.IsGlobal)
        {
            throw new InvalidOperationException("Reserved 'global' ThreadSpace cannot be updated.");
        }

        var name = existing.Name;
        if (!string.IsNullOrWhiteSpace(newName))
        {
            name = newName.Trim();
            if (string.Equals(name, GlobalName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("ThreadSpace name 'global' is reserved.");
            }

            var other = await threadSpaces.GetByNameAsync(name, cancellationToken).ConfigureAwait(false);
            if (other is not null && other.ThreadSpaceId != threadSpaceId)
            {
                throw new InvalidOperationException($"ThreadSpace with name '{name}' already exists.");
            }
        }

        var path = existing.BoundFolderPath;
        if (!string.IsNullOrWhiteSpace(newBoundFolderPath))
        {
            path = NormalizeAndValidateBoundFolderPath(newBoundFolderPath!);
            var other = await threadSpaces.GetByBoundFolderPathAsync(path, cancellationToken).ConfigureAwait(false);
            if (other is not null && other.ThreadSpaceId != threadSpaceId)
            {
                throw new InvalidOperationException($"ThreadSpace for path '{path}' already exists.");
            }
            Directory.CreateDirectory(path);
        }

        var updated = existing with { Name = name, BoundFolderPath = path };
        await threadSpaces.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(threadSpaceId, cancellationToken).ConfigureAwait(false);
        if (existing.IsGlobal)
        {
            throw new InvalidOperationException("Reserved 'global' ThreadSpace cannot be archived.");
        }

        await threadSpaces.ArchiveAsync(threadSpaceId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionRecord>> ListSessionsAsync(ThreadSpaceId threadSpaceId, CancellationToken cancellationToken = default)
    {
        await GetAsync(threadSpaceId, cancellationToken).ConfigureAwait(false);
        return await sessions.ListByThreadSpaceAsync(threadSpaceId, cancellationToken).ConfigureAwait(false);
    }

    private string NormalizeAndValidateBoundFolderPath(string boundFolderPath)
    {
        var normalized = NormalizeBoundFolderPath(boundFolderPath);
        var workspaceRoot = NormalizeBoundFolderPath(options.Runtime.WorkspaceRoot);
        var normalizedWorkspaceRoot = EnsureTrailingSeparator(workspaceRoot);
        var candidateWithSeparator = EnsureTrailingSeparator(normalized);

        if (!candidateWithSeparator.StartsWith(normalizedWorkspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"ThreadSpace path '{normalized}' must be within workspace root '{workspaceRoot}'.");
        }

        return normalized;
    }

    private static string NormalizeBoundFolderPath(string path) => Path.GetFullPath(path.Trim());

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}
