using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClawSharp.Lib.Configuration;
using DuckDB.NET.Data;
using Microsoft.EntityFrameworkCore;

namespace ClawSharp.Lib.Runtime;

internal static class DatabasePathResolver
{
    public static string ResolveSqlitePath(ClawOptions options)
    {
        var configuredPath = string.IsNullOrWhiteSpace(options.Databases.Sqlite.DatabasePath)
            ? options.Sessions.DatabasePath
            : options.Databases.Sqlite.DatabasePath;
        return ResolvePath(options, configuredPath);
    }

    public static string ResolveDuckDbPath(ClawOptions options) => ResolvePath(options, options.Databases.DuckDb.DatabasePath);

    private static string ResolvePath(ClawOptions options, string configuredPath)
    {
        var path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(options.Runtime.WorkspaceRoot, configuredPath);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return fullPath;
    }
}

internal sealed class ClawSqliteDbContextFactory
{
    private readonly DbContextOptions<ClawDbContext> _options;

    public ClawSqliteDbContextFactory(ClawOptions options)
    {
        DatabasePath = DatabasePathResolver.ResolveSqlitePath(options);
        var builder = new DbContextOptionsBuilder<ClawDbContext>();
        builder.UseSqlite($"Data Source={DatabasePath}");
        _options = builder.Options;

        using var context = CreateDbContext();
        context.Database.EnsureCreated();
    }

    public string DatabasePath { get; }

    public ClawDbContext CreateDbContext() => new(_options);
}

internal sealed class DuckDbConnectionFactory
{
    public DuckDbConnectionFactory(ClawOptions options)
    {
        DatabasePath = DatabasePathResolver.ResolveDuckDbPath(options);
    }

    public string DatabasePath { get; }

    public DuckDBConnection Open()
    {
        var connection = new DuckDBConnection($"DataSource={DatabasePath}");
        connection.Open();
        return connection;
    }
}

internal sealed class ClawDbContext(DbContextOptions<ClawDbContext> options) : DbContext(options)
{
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();

    public DbSet<MessageEntity> Messages => Set<MessageEntity>();

    public DbSet<SessionEventEntity> SessionEvents => Set<SessionEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var messages = modelBuilder.Entity<MessageEntity>();
        messages.HasIndex(x => new { x.SessionId, x.SequenceNo });

        var events = modelBuilder.Entity<SessionEventEntity>();
        events.HasIndex(x => new { x.SessionId, x.SequenceNo });
    }
}

[Table("sessions")]
internal sealed class SessionEntity
{
    [Key]
    [Column("session_id")]
    [Required]
    [MaxLength(128)]
    public required string SessionId { get; init; }

    [Column("agent_id")]
    [Required]
    [MaxLength(128)]
    public required string AgentId { get; init; }

    [Column("workspace_root")]
    [Required]
    [MaxLength(1024)]
    public required string WorkspaceRoot { get; init; }

    [Column("status")]
    public SessionStatus Status { get; set; }

    [Column("started_at")]
    public DateTimeOffset StartedAt { get; set; }

    [Column("ended_at")]
    public DateTimeOffset? EndedAt { get; set; }
}

[Table("messages")]
internal sealed class MessageEntity
{
    [Key]
    [Column("message_id")]
    [Required]
    [MaxLength(128)]
    public required string MessageId { get; init; }

    [Column("session_id")]
    [Required]
    [MaxLength(128)]
    public required string SessionId { get; init; }

    [Column("turn_id")]
    [Required]
    [MaxLength(128)]
    public required string TurnId { get; init; }
    
    [Column("role")]
    public PromptMessageRole Role { get; init; }

    [Column("content")]
    [Required]
    [MaxLength(1000_0000)]
    public required string Content { get; init; }

    [Column("name")]
    [MaxLength(128)]
    public string? Name { get; init; }

    [Column("tool_call_id")]
    [MaxLength(128)]
    public string? ToolCallId { get; init; }
    
    [Column("sequence_no")]
    public int SequenceNo { get; init; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}

[Table("session_events")]
internal sealed class SessionEventEntity
{
    [Key]
    [Column("event_id")]
    [Required]
    [MaxLength(128)]
    public required string EventId { get; init; }

    [Column("session_id")]
    [Required]
    [MaxLength(128)]
    public required string SessionId { get; init; }

    [Column("turn_id")]
    [Required]
    [MaxLength(128)]
    public required string TurnId { get; init; }

    [Column("event_type")]
    [Required]
    [MaxLength(128)]
    public required string EventType { get; init; }

    [Column("payload")]
    [Required]
    [MaxLength(1000_0000)]
    public required string PayloadJson { get; init; }

    [Column("sequence_no")]
    public int SequenceNo { get; init; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}

internal static class RuntimeEntityMapper
{
    public static SessionEntity ToEntity(SessionRecord record) => new()
    {
        SessionId = record.SessionId.Value,
        AgentId = record.AgentId,
        WorkspaceRoot = record.WorkspaceRoot,
        Status = record.Status,
        StartedAt = record.StartedAt,
        EndedAt = record.EndedAt
    };

    public static SessionRecord ToRecord(SessionEntity entity) =>
        new(new SessionId(entity.SessionId), entity.AgentId, entity.WorkspaceRoot, entity.Status, entity.StartedAt, entity.EndedAt);

    public static MessageEntity ToEntity(PromptMessage message) => new()
    {
        MessageId = message.MessageId.Value,
        SessionId = message.SessionId.Value,
        TurnId = message.TurnId.Value,
        Role = message.Role,
        Content = message.Content,
        Name = message.Name,
        ToolCallId = message.ToolCallId,
        SequenceNo = message.SequenceNo,
        CreatedAt = message.CreatedAt
    };

    public static PromptMessage ToRecord(MessageEntity entity) =>
        new(
            new MessageId(entity.MessageId),
            new SessionId(entity.SessionId),
            new TurnId(entity.TurnId),
            entity.Role,
            entity.Content,
            entity.SequenceNo,
            entity.CreatedAt,
            entity.Name,
            entity.ToolCallId);

    public static SessionEventEntity ToEntity(SessionEvent sessionEvent) => new()
    {
        EventId = sessionEvent.EventId.Value,
        SessionId = sessionEvent.SessionId.Value,
        TurnId = sessionEvent.TurnId.Value,
        EventType = sessionEvent.EventType,
        PayloadJson = sessionEvent.Payload.GetRawText(),
        SequenceNo = sessionEvent.SequenceNo,
        CreatedAt = sessionEvent.CreatedAt
    };

    public static SessionEvent ToRecord(SessionEventEntity entity) =>
        new(
            new EventId(entity.EventId),
            new SessionId(entity.SessionId),
            new TurnId(entity.TurnId),
            entity.EventType,
            JsonSessionSerializerHelper.ParseElement(entity.PayloadJson),
            entity.SequenceNo,
            entity.CreatedAt);
}

internal static class JsonSessionSerializerHelper
{
    public static System.Text.Json.JsonElement ParseElement(string json) =>
        System.Text.Json.JsonSerializer.SerializeToElement(System.Text.Json.JsonDocument.Parse(json).RootElement);
}

internal interface ISessionRecordRepository
{
    Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default);

    Task<SessionRecord?> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionRecord>> ListActiveAsync(CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(SessionId sessionId, SessionStatus status, DateTimeOffset? endedAt, CancellationToken cancellationToken = default);
}

internal interface IPromptMessageRepository
{
    Task<PromptMessage> AppendAsync(SessionId sessionId, TurnId turnId, PromptMessageRole role, string content, string? name, string? toolCallId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromptMessage>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default);
}

internal interface ISessionEventRepository
{
    Task<SessionEvent> AppendAsync(SessionId sessionId, TurnId turnId, string eventType, System.Text.Json.JsonElement payload, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionEvent>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default);
}

internal sealed class EfSessionRecordRepository(ClawSqliteDbContextFactory factory) : ISessionRecordRepository
{
    public async Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default)
    {
        await using var context = factory.CreateDbContext();
        context.Sessions.Add(RuntimeEntityMapper.ToEntity(session));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SessionRecord?> GetAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        await using var context = factory.CreateDbContext();
        var entity = await context.Sessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SessionId == sessionId.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : RuntimeEntityMapper.ToRecord(entity);
    }

    public async Task<IReadOnlyList<SessionRecord>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var context = factory.CreateDbContext();
        var results = await context.Sessions.AsNoTracking()
            .Where(x => x.Status == SessionStatus.Created || x.Status == SessionStatus.Running || x.Status == SessionStatus.WaitingForApproval)
            .Select(x => RuntimeEntityMapper.ToRecord(x))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return results.OrderByDescending(x => x.StartedAt).ToList();
    }

    public async Task UpdateStatusAsync(SessionId sessionId, SessionStatus status, DateTimeOffset? endedAt, CancellationToken cancellationToken = default)
    {
        await using var context = factory.CreateDbContext();
        var entity = await context.Sessions.SingleAsync(x => x.SessionId == sessionId.Value, cancellationToken).ConfigureAwait(false);
        context.Entry(entity).Property(x => x.Status).CurrentValue = status;
        context.Entry(entity).Property(x => x.EndedAt).CurrentValue = endedAt;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class EfPromptMessageRepository(ClawSqliteDbContextFactory factory) : IPromptMessageRepository
{
    public async Task<PromptMessage> AppendAsync(SessionId sessionId, TurnId turnId, PromptMessageRole role, string content, string? name, string? toolCallId, CancellationToken cancellationToken = default)
    {
        await using var context = factory.CreateDbContext();
        var sequenceNo = await context.Messages
            .Where(x => x.SessionId == sessionId.Value)
            .Select(x => (int?)x.SequenceNo)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false) ?? 0;

        var message = new PromptMessage(MessageId.New(), sessionId, turnId, role, content, sequenceNo + 1, DateTimeOffset.UtcNow, name, toolCallId);
        context.Messages.Add(RuntimeEntityMapper.ToEntity(message));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return message;
    }

    public async Task<IReadOnlyList<PromptMessage>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        await using var context = factory.CreateDbContext();
        return await context.Messages.AsNoTracking()
            .Where(x => x.SessionId == sessionId.Value)
            .OrderBy(x => x.SequenceNo)
            .Select(x => RuntimeEntityMapper.ToRecord(x))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class EfSessionEventRepository(ClawSqliteDbContextFactory factory) : ISessionEventRepository
{
    public async Task<SessionEvent> AppendAsync(SessionId sessionId, TurnId turnId, string eventType, System.Text.Json.JsonElement payload, CancellationToken cancellationToken = default)
    {
        await using var context = factory.CreateDbContext();
        var sequenceNo = await context.SessionEvents
            .Where(x => x.SessionId == sessionId.Value)
            .Select(x => (int?)x.SequenceNo)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false) ?? 0;

        var sessionEvent = new SessionEvent(EventId.New(), sessionId, turnId, eventType, payload, sequenceNo + 1, DateTimeOffset.UtcNow);
        context.SessionEvents.Add(RuntimeEntityMapper.ToEntity(sessionEvent));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return sessionEvent;
    }

    public async Task<IReadOnlyList<SessionEvent>> ListAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        await using var context = factory.CreateDbContext();
        return await context.SessionEvents.AsNoTracking()
            .Where(x => x.SessionId == sessionId.Value)
            .OrderBy(x => x.SequenceNo)
            .Select(x => RuntimeEntityMapper.ToRecord(x))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
