using System.Globalization;
using ClawSharp.Lib.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ClawSharp.Lib.Runtime;

/// <summary>
/// 会话分析服务抽象。
/// </summary>
public interface ISessionAnalyticsService
{
    /// <summary>
    /// 获取当前运行时数据的分析摘要。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>聚合后的分析结果。</returns>
    Task<SessionAnalyticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 运行时数据的聚合分析结果。
/// </summary>
public sealed record SessionAnalyticsSnapshot(
    int TotalSessions,
    int ActiveSessions,
    IReadOnlyList<SessionStatusCount> SessionsByStatus,
    IReadOnlyList<PromptRoleCount> MessagesByRole,
    IReadOnlyList<EventTypeCount> EventsByType,
    IReadOnlyList<SessionMessageCount> MessagesPerSession,
    IReadOnlyList<PromptBlockTypeCount> BlocksByType,
    IReadOnlyList<RoleBlockTypeCount> BlocksByRoleAndType);

/// <summary>
/// 按状态汇总的 session 数量。
/// </summary>
public sealed record SessionStatusCount(SessionStatus Status, int Count);

/// <summary>
/// 按角色汇总的消息数量。
/// </summary>
public sealed record PromptRoleCount(PromptMessageRole Role, int Count);

/// <summary>
/// 按事件类型汇总的事件数量。
/// </summary>
public sealed record EventTypeCount(string EventType, int Count);

/// <summary>
/// 每个 session 的消息数量。
/// </summary>
public sealed record SessionMessageCount(SessionId SessionId, int Count);

/// <summary>
/// 按 block 类型汇总的内容块数量。
/// </summary>
public sealed record PromptBlockTypeCount(string BlockType, int Count);

/// <summary>
/// 按消息角色和 block 类型汇总的数量。
/// </summary>
public sealed record RoleBlockTypeCount(PromptMessageRole Role, string BlockType, int Count);

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
        var sessions = await sqlite.Sessions.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var messageEntities = await sqlite.Messages.AsNoTracking().OrderBy(x => x.SequenceNo).ToListAsync(cancellationToken).ConfigureAwait(false);
        var messages = messageEntities.Select(RuntimeEntityMapper.ToRecord).ToList();
        var events = await sqlite.SessionEvents.AsNoTracking().OrderBy(x => x.SequenceNo).ToListAsync(cancellationToken).ConfigureAwait(false);
        sessions = sessions.OrderBy(x => x.StartedAt).ToList();

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

    private static void ExecuteNonQuery(DuckDB.NET.Data.DuckDBConnection connection, string sql)
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
    public async Task<SessionAnalyticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Databases.DuckDb.Enabled)
        {
            return new SessionAnalyticsSnapshot(0, 0, [], [], [], [], [], []);
        }

        await projector.RebuildAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = duckDbFactory.Open();
        var totalSessions = ExecuteScalarInt(connection, "SELECT COUNT(*) FROM sessions;");
        var activeSessions = ExecuteScalarInt(connection, $"SELECT COUNT(*) FROM sessions WHERE status IN ({(int)SessionStatus.Created}, {(int)SessionStatus.Running}, {(int)SessionStatus.WaitingForApproval});");

        var sessionsByStatus = ReadList(connection, "SELECT status, COUNT(*) FROM sessions GROUP BY status ORDER BY status;",
            reader => new SessionStatusCount((SessionStatus)reader.GetInt32(0), reader.GetInt32(1)));
        var messagesByRole = ReadList(connection, "SELECT role, COUNT(*) FROM messages GROUP BY role ORDER BY role;",
            reader => new PromptRoleCount((PromptMessageRole)reader.GetInt32(0), reader.GetInt32(1)));
        var eventsByType = ReadList(connection, "SELECT event_type, COUNT(*) FROM session_events GROUP BY event_type ORDER BY event_type;",
            reader => new EventTypeCount(reader.GetString(0), reader.GetInt32(1)));
        var messagesPerSession = ReadList(connection, "SELECT session_id, COUNT(*) FROM messages GROUP BY session_id ORDER BY session_id;",
            reader => new SessionMessageCount(new SessionId(reader.GetString(0)), reader.GetInt32(1)));
        var blocksByType = ReadList(connection, "SELECT block_type, COUNT(*) FROM message_blocks GROUP BY block_type ORDER BY block_type;",
            reader => new PromptBlockTypeCount(reader.GetString(0), reader.GetInt32(1)));
        var blocksByRoleAndType = ReadList(connection, "SELECT role, block_type, COUNT(*) FROM message_blocks GROUP BY role, block_type ORDER BY role, block_type;",
            reader => new RoleBlockTypeCount((PromptMessageRole)reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));

        return new SessionAnalyticsSnapshot(totalSessions, activeSessions, sessionsByStatus, messagesByRole, eventsByType, messagesPerSession, blocksByType, blocksByRoleAndType);
    }

    private static int ExecuteScalarInt(DuckDB.NET.Data.DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<T> ReadList<T>(DuckDB.NET.Data.DuckDBConnection connection, string sql, Func<System.Data.Common.DbDataReader, T> projector)
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
}

internal sealed class NullSessionAnalyticsService : ISessionAnalyticsService
{
    public Task<SessionAnalyticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SessionAnalyticsSnapshot(0, 0, [], [], [], [], [], []));
}
