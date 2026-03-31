using System.Data.Common;
using System.Globalization;
using ClawSharp.Lib.Providers;
using DuckDB.NET.Data;
using Microsoft.EntityFrameworkCore;

namespace ClawSharp.Lib.Runtime;

internal interface IDuckDbAnalyticsProjector
{
    Task RebuildAsync(CancellationToken cancellationToken = default);
}

internal sealed class DuckDbAnalyticsProjector(
    ClawSharp.Lib.Configuration.ClawOptions options,
    IDbContextFactory<ClawDbContext> sqliteFactory,
    ClawSqliteDatabaseInitializer sqliteInitializer,
    DuckDbConnectionFactory duckDbFactory) : IDuckDbAnalyticsProjector
{
    public async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Databases.DuckDb.Enabled)
        {
            return;
        }

        sqliteInitializer.EnsureInitialized();
        await using var sqlite = await sqliteFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var sessions = (await sqlite.Sessions.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(x => x.StartedAt)
            .ToList();
        var messageEntities = await sqlite.Messages.AsNoTracking().OrderBy(x => x.SequenceNo).ToListAsync(cancellationToken).ConfigureAwait(false);
        var messages = messageEntities.Select(RuntimeEntityMapper.ToRecord).ToList();
        var events = (await sqlite.SessionEvents.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(x => x.SequenceNo)
            .ToList();

        await using var duck = duckDbFactory.Open();
        ExecuteNonQuery(duck, """
DROP TABLE IF EXISTS sessions;
DROP TABLE IF EXISTS messages;
DROP TABLE IF EXISTS message_blocks;
DROP TABLE IF EXISTS session_events;
CREATE TABLE sessions (
  session_id VARCHAR PRIMARY KEY,
  agent_id VARCHAR NOT NULL,
  workspace_root VARCHAR NOT NULL,
  status INTEGER NOT NULL,
  started_at TIMESTAMP WITH TIME ZONE NOT NULL,
  ended_at TIMESTAMP WITH TIME ZONE NULL
);
CREATE TABLE messages (
  message_id VARCHAR PRIMARY KEY,
  session_id VARCHAR NOT NULL,
  turn_id VARCHAR NOT NULL,
  role INTEGER NOT NULL,
  content VARCHAR NOT NULL,
  name VARCHAR NULL,
  tool_call_id VARCHAR NULL,
  blocks_json VARCHAR NULL,
  sequence_no INTEGER NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE NOT NULL
);
CREATE TABLE message_blocks (
  message_id VARCHAR NOT NULL,
  session_id VARCHAR NOT NULL,
  role INTEGER NOT NULL,
  block_index INTEGER NOT NULL,
  block_type VARCHAR NOT NULL,
  text VARCHAR NULL,
  name VARCHAR NULL,
  tool_call_id VARCHAR NULL,
  arguments_json VARCHAR NULL,
  tool_name VARCHAR NULL
);
CREATE TABLE session_events (
  event_id VARCHAR PRIMARY KEY,
  session_id VARCHAR NOT NULL,
  turn_id VARCHAR NOT NULL,
  event_type VARCHAR NOT NULL,
  payload VARCHAR NOT NULL,
  sequence_no INTEGER NOT NULL,
  created_at TIMESTAMP WITH TIME ZONE NOT NULL
);
""");

        foreach (var session in sessions)
        {
            ExecuteNonQuery(duck, $$"""
INSERT INTO sessions(session_id, agent_id, workspace_root, status, started_at, ended_at)
VALUES ({{ToSqlLiteral(session.SessionId)}}, {{ToSqlLiteral(session.AgentId)}}, {{ToSqlLiteral(session.WorkspaceRoot)}}, {{((int)session.Status).ToString(CultureInfo.InvariantCulture)}}, {{ToSqlLiteral(session.StartedAt)}}, {{ToSqlLiteral(session.EndedAt)}});
""");
        }

        foreach (var message in messages)
        {
            ExecuteNonQuery(duck, $$"""
INSERT INTO messages(message_id, session_id, turn_id, role, content, name, tool_call_id, blocks_json, sequence_no, created_at)
VALUES ({{ToSqlLiteral(message.MessageId.Value)}}, {{ToSqlLiteral(message.SessionId.Value)}}, {{ToSqlLiteral(message.TurnId.Value)}}, {{((int)message.Role).ToString(CultureInfo.InvariantCulture)}}, {{ToSqlLiteral(message.Content)}}, {{ToSqlLiteral(message.Name)}}, {{ToSqlLiteral(message.ToolCallId)}}, {{ToSqlLiteral(JsonSessionSerializerHelper.SerializeBlocks(message.Blocks))}}, {{message.SequenceNo.ToString(CultureInfo.InvariantCulture)}}, {{ToSqlLiteral(message.CreatedAt)}});
""");

            for (var index = 0; index < message.Blocks.Count; index++)
            {
                var block = message.Blocks[index];
                var blockType = block switch
                {
                    ModelTextBlock => "text",
                    ModelToolUseBlock => "tool_use",
                    ModelToolResultBlock => "tool_result",
                    _ => "unknown"
                };
                var text = block is ModelTextBlock textBlock ? textBlock.Text : null;
                var name = block is ModelToolUseBlock toolUseBlock ? toolUseBlock.Name : null;
                var toolCallId = block switch
                {
                    ModelToolUseBlock toolUseIdBlock => toolUseIdBlock.Id,
                    ModelToolResultBlock toolResultBlock => toolResultBlock.ToolCallId,
                    _ => null
                };
                var argumentsJson = block is ModelToolUseBlock toolUse ? toolUse.ArgumentsJson : null;
                var toolName = block is ModelToolResultBlock toolResult ? toolResult.ToolName : null;

                ExecuteNonQuery(duck, $$"""
INSERT INTO message_blocks(message_id, session_id, role, block_index, block_type, text, name, tool_call_id, arguments_json, tool_name)
VALUES ({{ToSqlLiteral(message.MessageId.Value)}}, {{ToSqlLiteral(message.SessionId.Value)}}, {{((int)message.Role).ToString(CultureInfo.InvariantCulture)}}, {{index.ToString(CultureInfo.InvariantCulture)}}, {{ToSqlLiteral(blockType)}}, {{ToSqlLiteral(text)}}, {{ToSqlLiteral(name)}}, {{ToSqlLiteral(toolCallId)}}, {{ToSqlLiteral(argumentsJson)}}, {{ToSqlLiteral(toolName)}});
""");
            }
        }

        foreach (var sessionEvent in events)
        {
            ExecuteNonQuery(duck, $$"""
INSERT INTO session_events(event_id, session_id, turn_id, event_type, payload, sequence_no, created_at)
VALUES ({{ToSqlLiteral(sessionEvent.EventId)}}, {{ToSqlLiteral(sessionEvent.SessionId)}}, {{ToSqlLiteral(sessionEvent.TurnId)}}, {{ToSqlLiteral(sessionEvent.EventType)}}, {{ToSqlLiteral(sessionEvent.PayloadJson)}}, {{sessionEvent.SequenceNo.ToString(CultureInfo.InvariantCulture)}}, {{ToSqlLiteral(sessionEvent.CreatedAt)}});
""");
        }
    }

    private static void ExecuteNonQuery(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string ToSqlLiteral(string? value) =>
        value is null ? "NULL" : $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string ToSqlLiteral(DateTimeOffset? value) =>
        value is null ? "NULL" : ToSqlLiteral(value.Value.ToString("O", CultureInfo.InvariantCulture));
}

internal sealed class DuckDbSessionAnalyticsService(
    ClawSharp.Lib.Configuration.ClawOptions options,
    IDuckDbAnalyticsProjector projector,
    DuckDbConnectionFactory duckDbFactory) : ISessionAnalyticsService
{
    private static readonly SessionAnalyticsSnapshot EmptySnapshot = new(0, 0, [], [], [], [], [], []);

    public async Task<SessionAnalyticsSnapshot> GetSnapshotAsync(
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        CancellationToken cancellationToken = default)
    {
        if (!options.Databases.DuckDb.Enabled)
        {
            return EmptySnapshot;
        }

        await projector.RebuildAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = duckDbFactory.Open();
        var sessionWhere = BuildSessionRangeClause(start, end);
        var messageWhere = BuildTimeRangeClause("created_at", start, end);
        var eventWhere = BuildTimeRangeClause("created_at", start, end);
        var blockWhere = BuildTimeRangeClause("m.created_at", start, end);

        var totalSessions = ExecuteScalarInt(connection, $"SELECT COUNT(*) FROM sessions {sessionWhere};");
        var activeSessions = ExecuteScalarInt(connection, $"SELECT COUNT(*) FROM sessions {sessionWhere}{AppendAnd(sessionWhere)}status IN ({(int)SessionStatus.Created}, {(int)SessionStatus.Running}, {(int)SessionStatus.WaitingForApproval});");

        var sessionsByStatus = ReadList(connection, $"SELECT status, COUNT(*) FROM sessions {sessionWhere} GROUP BY status ORDER BY status;",
            reader => new SessionStatusCount((SessionStatus)reader.GetInt32(0), reader.GetInt32(1)));
        var messagesByRole = ReadList(connection, $"SELECT role, COUNT(*) FROM messages {messageWhere} GROUP BY role ORDER BY role;",
            reader => new PromptRoleCount((PromptMessageRole)reader.GetInt32(0), reader.GetInt32(1)));
        var eventsByType = ReadList(connection, $"SELECT event_type, COUNT(*) FROM session_events {eventWhere} GROUP BY event_type ORDER BY event_type;",
            reader => new EventTypeCount(reader.GetString(0), reader.GetInt32(1)));
        var messagesPerSession = ReadList(connection, $"SELECT session_id, COUNT(*) FROM messages {messageWhere} GROUP BY session_id ORDER BY session_id;",
            reader => new SessionMessageCount(new SessionId(reader.GetString(0)), reader.GetInt32(1)));
        var blocksByType = ReadList(connection, $"SELECT mb.block_type, COUNT(*) FROM message_blocks mb INNER JOIN messages m ON m.message_id = mb.message_id {blockWhere} GROUP BY mb.block_type ORDER BY mb.block_type;",
            reader => new PromptBlockTypeCount(reader.GetString(0), reader.GetInt32(1)));
        var blocksByRoleAndType = ReadList(connection, $"SELECT mb.role, mb.block_type, COUNT(*) FROM message_blocks mb INNER JOIN messages m ON m.message_id = mb.message_id {blockWhere} GROUP BY mb.role, mb.block_type ORDER BY mb.role, mb.block_type;",
            reader => new RoleBlockTypeCount((PromptMessageRole)reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));

        return new SessionAnalyticsSnapshot(totalSessions, activeSessions, sessionsByStatus, messagesByRole, eventsByType, messagesPerSession, blocksByType, blocksByRoleAndType);
    }

    public async Task<IReadOnlyList<TokenUsageMetric>> GetTokenUsageTrendAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        if (!options.Databases.DuckDb.Enabled)
        {
            return [];
        }

        await projector.RebuildAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = duckDbFactory.Open();
        return ReadList(connection, $$"""
SELECT
  created_at,
  COALESCE(
    TRY_CAST(json_extract(payload, '$.usage.InputTokens') AS INTEGER),
    TRY_CAST(json_extract(payload, '$.Usage.InputTokens') AS INTEGER),
    TRY_CAST(json_extract(payload, '$.Usage.PromptTokens') AS INTEGER),
    TRY_CAST(json_extract(payload, '$.usage.prompt_tokens') AS INTEGER),
    0
  ) AS input_tokens,
  COALESCE(
    TRY_CAST(json_extract(payload, '$.usage.OutputTokens') AS INTEGER),
    TRY_CAST(json_extract(payload, '$.Usage.OutputTokens') AS INTEGER),
    TRY_CAST(json_extract(payload, '$.Usage.CompletionTokens') AS INTEGER),
    TRY_CAST(json_extract(payload, '$.usage.completion_tokens') AS INTEGER),
    0
  ) AS output_tokens
FROM session_events
WHERE event_type = 'TurnCompleted'
  AND created_at >= {{ToSqlLiteral(start)}}
  AND created_at <= {{ToSqlLiteral(end)}}
ORDER BY created_at;
""", reader => new TokenUsageMetric(reader.GetInt32(1), reader.GetInt32(2), ReadDateTimeOffset(reader, 0)));
    }

    public async Task<IReadOnlyList<ToolUsageMetric>> GetToolUsageStatsAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        if (!options.Databases.DuckDb.Enabled)
        {
            return [];
        }

        await projector.RebuildAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = duckDbFactory.Open();
        return ReadList(connection, $$"""
WITH tool_calls AS (
  SELECT
    mb.tool_call_id,
    COALESCE(NULLIF(mb.name, ''), NULLIF(mb.tool_name, ''), 'unknown') AS tool_name
  FROM message_blocks mb
  INNER JOIN messages m ON m.message_id = mb.message_id
  WHERE mb.block_type = 'tool_use'
    AND mb.tool_call_id IS NOT NULL
    AND m.created_at >= {{ToSqlLiteral(start)}}
    AND m.created_at <= {{ToSqlLiteral(end)}}
),
tool_results AS (
  SELECT
    COALESCE(json_extract_string(payload, '$.toolCallId'), json_extract_string(payload, '$.tool_call_id')) AS tool_call_id,
    LOWER(COALESCE(json_extract_string(payload, '$.status'), 'failed')) AS result_status
  FROM session_events
  WHERE event_type = 'ToolCallCompleted'
    AND created_at >= {{ToSqlLiteral(start)}}
    AND created_at <= {{ToSqlLiteral(end)}}
)
SELECT
  c.tool_name,
  COUNT(*) AS call_count,
  SUM(CASE WHEN r.result_status = 'success' THEN 1 ELSE 0 END) AS success_count,
  COUNT(*) - SUM(CASE WHEN r.result_status = 'success' THEN 1 ELSE 0 END) AS failure_count
FROM tool_calls c
LEFT JOIN tool_results r ON r.tool_call_id = c.tool_call_id
GROUP BY c.tool_name
ORDER BY call_count DESC, c.tool_name ASC;
""", reader => new ToolUsageMetric(
            reader.GetString(0),
            ReadInt32(reader, 1),
            ReadInt32(reader, 2),
            ReadInt32(reader, 3)));
    }

    public async Task<IReadOnlyList<AgentPerformanceMetric>> GetAgentPerformanceAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        if (!options.Databases.DuckDb.Enabled)
        {
            return [];
        }

        await projector.RebuildAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = duckDbFactory.Open();
        return ReadList(connection, $$"""
WITH turn_metrics AS (
  SELECT
    s.agent_id,
    COALESCE(
      TRY_CAST(json_extract(e.payload, '$.latencyMs') AS DOUBLE),
      TRY_CAST(json_extract(e.payload, '$.LatencyMs') AS DOUBLE),
      CAST(date_diff('millisecond', m.created_at, e.created_at) AS DOUBLE),
      0
    ) AS latency_ms
  FROM session_events e
  INNER JOIN sessions s ON s.session_id = e.session_id
  LEFT JOIN messages m
    ON m.session_id = e.session_id
   AND m.turn_id = e.turn_id
   AND m.role = {{(int)PromptMessageRole.User}}
  WHERE e.event_type = 'TurnCompleted'
    AND e.created_at >= {{ToSqlLiteral(start)}}
    AND e.created_at <= {{ToSqlLiteral(end)}}
)
SELECT
  agent_id,
  AVG(latency_ms) AS avg_latency_ms,
  MIN(latency_ms) AS min_latency_ms,
  MAX(latency_ms) AS max_latency_ms,
  COUNT(*) AS request_count
FROM turn_metrics
GROUP BY agent_id
ORDER BY avg_latency_ms DESC, agent_id ASC;
""", reader => new AgentPerformanceMetric(
            reader.GetString(0),
            reader.GetDouble(1),
            reader.GetDouble(2),
            reader.GetDouble(3),
            ReadInt32(reader, 4)));
    }

    private static int ExecuteScalarInt(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<T> ReadList<T>(DuckDBConnection connection, string sql, Func<DbDataReader, T> projector)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var results = new List<T>();
        while (reader.Read())
        {
            results.Add(projector(reader));
        }

        return results;
    }

    private static string BuildTimeRangeClause(string columnName, DateTimeOffset? start, DateTimeOffset? end)
    {
        var clauses = new List<string>();
        if (start is not null)
        {
            clauses.Add($"{columnName} >= {ToSqlLiteral(start.Value)}");
        }

        if (end is not null)
        {
            clauses.Add($"{columnName} <= {ToSqlLiteral(end.Value)}");
        }

        return clauses.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", clauses)}";
    }

    private static string BuildSessionRangeClause(DateTimeOffset? start, DateTimeOffset? end)
    {
        var clauses = new List<string>();
        if (end is not null)
        {
            clauses.Add($"started_at <= {ToSqlLiteral(end.Value)}");
        }

        if (start is not null)
        {
            clauses.Add($"(ended_at IS NULL OR ended_at >= {ToSqlLiteral(start.Value)})");
        }

        return clauses.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", clauses)}";
    }

    private static string AppendAnd(string clause) => string.IsNullOrWhiteSpace(clause) ? " WHERE " : " AND ";

    private static string ToSqlLiteral(DateTimeOffset value) =>
        $"TIMESTAMPTZ '{value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}'";

    private static DateTimeOffset ReadDateTimeOffset(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            string raw => DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
            _ => new DateTimeOffset(Convert.ToDateTime(value, CultureInfo.InvariantCulture), TimeSpan.Zero)
        };
    }

    private static int ReadInt32(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value switch
        {
            int intValue => intValue,
            long longValue => checked((int)longValue),
            short shortValue => shortValue,
            byte byteValue => byteValue,
            System.Numerics.BigInteger bigInteger => (int)bigInteger,
            _ => Convert.ToInt32(value, CultureInfo.InvariantCulture)
        };
    }
}

internal sealed class NullSessionAnalyticsService : ISessionAnalyticsService
{
    public Task<SessionAnalyticsSnapshot> GetSnapshotAsync(
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        CancellationToken cancellationToken = default) => Task.FromResult(new SessionAnalyticsSnapshot(0, 0, [], [], [], [], [], []));

    public Task<IReadOnlyList<TokenUsageMetric>> GetTokenUsageTrendAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TokenUsageMetric>>([]);

    public Task<IReadOnlyList<ToolUsageMetric>> GetToolUsageStatsAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ToolUsageMetric>>([]);

    public Task<IReadOnlyList<AgentPerformanceMetric>> GetAgentPerformanceAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AgentPerformanceMetric>>([]);
}
