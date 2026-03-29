using Microsoft.Data.Sqlite;

namespace ClawSharp.Lib.Runtime;

internal sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}

internal static class SqliteSchema
{
    public static void Ensure(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
CREATE TABLE IF NOT EXISTS sessions (
  session_id TEXT PRIMARY KEY,
  agent_id TEXT NOT NULL,
  workspace_root TEXT NOT NULL,
  status INTEGER NOT NULL,
  started_at TEXT NOT NULL,
  ended_at TEXT NULL
);
CREATE TABLE IF NOT EXISTS messages (
  message_id TEXT PRIMARY KEY,
  session_id TEXT NOT NULL,
  turn_id TEXT NOT NULL,
  role INTEGER NOT NULL,
  content TEXT NOT NULL,
  name TEXT NULL,
  tool_call_id TEXT NULL,
  sequence_no INTEGER NOT NULL,
  created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS session_events (
  event_id TEXT PRIMARY KEY,
  session_id TEXT NOT NULL,
  turn_id TEXT NOT NULL,
  event_type TEXT NOT NULL,
  payload TEXT NOT NULL,
  sequence_no INTEGER NOT NULL,
  created_at TEXT NOT NULL
);
""";
        command.ExecuteNonQuery();
    }
}

/// <summary>
/// 基于 SQLite 的 session 记录存储实现。
/// </summary>
public sealed class SqliteSessionStore : ISessionStore
{
    private readonly SqliteConnectionFactory _factory;

    /// <summary>
    /// 创建一个 SQLite session store，并确保所需表结构存在。
    /// </summary>
    /// <param name="options">用于解析数据库路径的库配置。</param>
    public SqliteSessionStore(ClawSharp.Lib.Configuration.ClawOptions options)
    {
        var databasePath = Path.IsPathRooted(options.Sessions.DatabasePath)
            ? options.Sessions.DatabasePath
            : Path.Combine(options.Runtime.WorkspaceRoot, options.Sessions.DatabasePath);
        _factory = new SqliteConnectionFactory(databasePath);
        using var connection = _factory.Open();
        SqliteSchema.Ensure(connection);
    }

    /// <inheritdoc />
    public Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Open();
        var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO sessions(session_id, agent_id, workspace_root, status, started_at, ended_at)
VALUES($session_id, $agent_id, $workspace_root, $status, $started_at, $ended_at);
""";
        command.Parameters.AddWithValue("$session_id", session.SessionId.Value);
        command.Parameters.AddWithValue("$agent_id", session.AgentId);
        command.Parameters.AddWithValue("$workspace_root", session.WorkspaceRoot);
        command.Parameters.AddWithValue("$status", (int)session.Status);
        command.Parameters.AddWithValue("$started_at", session.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("$ended_at", (object?)session.EndedAt?.ToString("O") ?? DBNull.Value);
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SessionRecord?> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Open();
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT session_id, agent_id, workspace_root, status, started_at, ended_at
FROM sessions WHERE session_id = $session_id LIMIT 1;
""";
        command.Parameters.AddWithValue("$session_id", sessionId.Value);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Task.FromResult<SessionRecord?>(null);
        }

        var result = new SessionRecord(
            new SessionId(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            (SessionStatus)reader.GetInt32(3),
            DateTimeOffset.Parse(reader.GetString(4)),
            reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)));
        return Task.FromResult<SessionRecord?>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionRecord>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Open();
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT session_id, agent_id, workspace_root, status, started_at, ended_at
FROM sessions WHERE status IN ($created, $running, $waiting)
ORDER BY started_at DESC;
""";
        command.Parameters.AddWithValue("$created", (int)SessionStatus.Created);
        command.Parameters.AddWithValue("$running", (int)SessionStatus.Running);
        command.Parameters.AddWithValue("$waiting", (int)SessionStatus.WaitingForApproval);

        using var reader = command.ExecuteReader();
        var results = new List<SessionRecord>();
        while (reader.Read())
        {
            results.Add(new SessionRecord(
                new SessionId(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                (SessionStatus)reader.GetInt32(3),
                DateTimeOffset.Parse(reader.GetString(4)),
                reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5))));
        }

        return Task.FromResult<IReadOnlyList<SessionRecord>>(results);
    }

    /// <inheritdoc />
    public Task UpdateStatusAsync(SessionId sessionId, SessionStatus status, DateTimeOffset? endedAt = null, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Open();
        var command = connection.CreateCommand();
        command.CommandText = """
UPDATE sessions
SET status = $status, ended_at = $ended_at
WHERE session_id = $session_id;
""";
        command.Parameters.AddWithValue("$session_id", sessionId.Value);
        command.Parameters.AddWithValue("$status", (int)status);
        command.Parameters.AddWithValue("$ended_at", (object?)endedAt?.ToString("O") ?? DBNull.Value);
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }
}

/// <summary>
/// 基于 SQLite 的 prompt 历史存储实现。
/// </summary>
public sealed class SqlitePromptHistoryStore : IPromptHistoryStore
{
    private readonly SqliteConnectionFactory _factory;

    /// <summary>
    /// 创建一个 SQLite prompt 历史 store，并确保所需表结构存在。
    /// </summary>
    /// <param name="options">用于解析数据库路径的库配置。</param>
    public SqlitePromptHistoryStore(ClawSharp.Lib.Configuration.ClawOptions options)
    {
        var databasePath = Path.IsPathRooted(options.Sessions.DatabasePath)
            ? options.Sessions.DatabasePath
            : Path.Combine(options.Runtime.WorkspaceRoot, options.Sessions.DatabasePath);
        _factory = new SqliteConnectionFactory(databasePath);
        using var connection = _factory.Open();
        SqliteSchema.Ensure(connection);
    }

    /// <inheritdoc />
    public Task<PromptMessage> AppendAsync(SessionId sessionId, TurnId turnId, PromptMessageRole role, string content, string? name = null, string? toolCallId = null, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Open();
        var sequenceNo = GetNextSequence(connection, "messages", sessionId);
        var message = new PromptMessage(MessageId.New(), sessionId, turnId, role, content, sequenceNo, DateTimeOffset.UtcNow, name, toolCallId);

        var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO messages(message_id, session_id, turn_id, role, content, name, tool_call_id, sequence_no, created_at)
VALUES($message_id, $session_id, $turn_id, $role, $content, $name, $tool_call_id, $sequence_no, $created_at);
""";
        command.Parameters.AddWithValue("$message_id", message.MessageId.Value);
        command.Parameters.AddWithValue("$session_id", message.SessionId.Value);
        command.Parameters.AddWithValue("$turn_id", message.TurnId.Value);
        command.Parameters.AddWithValue("$role", (int)message.Role);
        command.Parameters.AddWithValue("$content", message.Content);
        command.Parameters.AddWithValue("$name", (object?)message.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("$tool_call_id", (object?)message.ToolCallId ?? DBNull.Value);
        command.Parameters.AddWithValue("$sequence_no", message.SequenceNo);
        command.Parameters.AddWithValue("$created_at", message.CreatedAt.ToString("O"));
        command.ExecuteNonQuery();
        return Task.FromResult(message);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PromptMessage>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Open();
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT message_id, session_id, turn_id, role, content, sequence_no, created_at, name, tool_call_id
FROM messages WHERE session_id = $session_id ORDER BY sequence_no ASC;
""";
        command.Parameters.AddWithValue("$session_id", sessionId.Value);

        using var reader = command.ExecuteReader();
        var messages = new List<PromptMessage>();
        while (reader.Read())
        {
            messages.Add(new PromptMessage(
                new MessageId(reader.GetString(0)),
                new SessionId(reader.GetString(1)),
                new TurnId(reader.GetString(2)),
                (PromptMessageRole)reader.GetInt32(3),
                reader.GetString(4),
                reader.GetInt32(5),
                DateTimeOffset.Parse(reader.GetString(6)),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return Task.FromResult<IReadOnlyList<PromptMessage>>(messages);
    }

    private static int GetNextSequence(SqliteConnection connection, string table, SessionId sessionId)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT COALESCE(MAX(sequence_no), 0) + 1 FROM {table} WHERE session_id = $session_id;";
        command.Parameters.AddWithValue("$session_id", sessionId.Value);
        return Convert.ToInt32(command.ExecuteScalar());
    }
}

/// <summary>
/// 基于 SQLite 的 session 事件存储实现。
/// </summary>
public sealed class SqliteSessionEventStore : ISessionEventStore
{
    private readonly SqliteConnectionFactory _factory;

    /// <summary>
    /// 创建一个 SQLite session event store，并确保所需表结构存在。
    /// </summary>
    /// <param name="options">用于解析数据库路径的库配置。</param>
    public SqliteSessionEventStore(ClawSharp.Lib.Configuration.ClawOptions options)
    {
        var databasePath = Path.IsPathRooted(options.Sessions.DatabasePath)
            ? options.Sessions.DatabasePath
            : Path.Combine(options.Runtime.WorkspaceRoot, options.Sessions.DatabasePath);
        _factory = new SqliteConnectionFactory(databasePath);
        using var connection = _factory.Open();
        SqliteSchema.Ensure(connection);
    }

    /// <inheritdoc />
    public Task<SessionEvent> AppendAsync(SessionId sessionId, TurnId turnId, string eventType, System.Text.Json.JsonElement payload, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Open();
        var sequenceNo = GetNextSequence(connection, sessionId);
        var sessionEvent = new SessionEvent(EventId.New(), sessionId, turnId, eventType, payload, sequenceNo, DateTimeOffset.UtcNow);

        var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO session_events(event_id, session_id, turn_id, event_type, payload, sequence_no, created_at)
VALUES($event_id, $session_id, $turn_id, $event_type, $payload, $sequence_no, $created_at);
""";
        command.Parameters.AddWithValue("$event_id", sessionEvent.EventId.Value);
        command.Parameters.AddWithValue("$session_id", sessionEvent.SessionId.Value);
        command.Parameters.AddWithValue("$turn_id", sessionEvent.TurnId.Value);
        command.Parameters.AddWithValue("$event_type", sessionEvent.EventType);
        command.Parameters.AddWithValue("$payload", sessionEvent.Payload.GetRawText());
        command.Parameters.AddWithValue("$sequence_no", sessionEvent.SequenceNo);
        command.Parameters.AddWithValue("$created_at", sessionEvent.CreatedAt.ToString("O"));
        command.ExecuteNonQuery();
        return Task.FromResult(sessionEvent);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionEvent>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        using var connection = _factory.Open();
        var command = connection.CreateCommand();
        command.CommandText = """
SELECT event_id, session_id, turn_id, event_type, payload, sequence_no, created_at
FROM session_events WHERE session_id = $session_id ORDER BY sequence_no ASC;
""";
        command.Parameters.AddWithValue("$session_id", sessionId.Value);

        using var reader = command.ExecuteReader();
        var events = new List<SessionEvent>();
        while (reader.Read())
        {
            events.Add(new SessionEvent(
                new EventId(reader.GetString(0)),
                new SessionId(reader.GetString(1)),
                new TurnId(reader.GetString(2)),
                reader.GetString(3),
                System.Text.Json.JsonSerializer.SerializeToElement(System.Text.Json.JsonDocument.Parse(reader.GetString(4)).RootElement),
                reader.GetInt32(5),
                DateTimeOffset.Parse(reader.GetString(6))));
        }

        return Task.FromResult<IReadOnlyList<SessionEvent>>(events);
    }

    private static int GetNextSequence(SqliteConnection connection, SessionId sessionId)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(sequence_no), 0) + 1 FROM session_events WHERE session_id = $session_id;";
        command.Parameters.AddWithValue("$session_id", sessionId.Value);
        return Convert.ToInt32(command.ExecuteScalar());
    }
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
