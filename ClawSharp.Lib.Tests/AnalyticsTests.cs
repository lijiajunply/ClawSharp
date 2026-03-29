using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClawSharp.Lib.Tests;

public sealed class AnalyticsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claw-analytics-tests", Guid.NewGuid().ToString("N"));

    public AnalyticsTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task SessionAnalyticsService_RebuildsDuckDbAndReturnsAggregates()
    {
        var options = CreateOptions(enabled: true);
        var threadSpaceStore = new SqliteThreadSpaceStore(options);
        var sessionStore = new SqliteSessionStore(options);
        var historyStore = new SqlitePromptHistoryStore(options);
        var eventStore = new SqliteSessionEventStore(options);
        var dbContextFactory = SqlitePersistenceFactory.CreateDbContextFactory(options);
        var init = await threadSpaceStore.GetByNameAsync("init");
        var analytics = new DuckDbSessionAnalyticsService(
            options,
            new DuckDbAnalyticsProjector(options, dbContextFactory, SqlitePersistenceFactory.CreateInitializer(options, dbContextFactory), new DuckDbConnectionFactory(options)),
            new DuckDbConnectionFactory(options));

        var sessionA = new SessionRecord(new SessionId("session-a"), init!.ThreadSpaceId, "planner", _root, SessionStatus.Created, DateTimeOffset.UtcNow);
        var sessionB = new SessionRecord(new SessionId("session-b"), init.ThreadSpaceId, "planner", _root, SessionStatus.Completed, DateTimeOffset.UtcNow);
        await sessionStore.CreateAsync(sessionA);
        await sessionStore.CreateAsync(sessionB);

        await historyStore.AppendAsync(sessionA.SessionId, new TurnId("turn-1"), PromptMessageRole.User, "hello");
        await historyStore.AppendBlocksAsync(sessionA.SessionId, new TurnId("turn-1"), PromptMessageRole.Assistant,
            [new ModelTextBlock("world"), new ModelToolUseBlock("call_1", "system.info", "{}")]);
        await historyStore.AppendAsync(sessionB.SessionId, new TurnId("turn-2"), PromptMessageRole.User, "again");
        await historyStore.AppendBlocksAsync(sessionB.SessionId, new TurnId("turn-2"), PromptMessageRole.Tool,
            [new ModelToolResultBlock("call_1", """{"ok":true}""", "system.info")]);
        await eventStore.AppendAsync(sessionA.SessionId, new TurnId("turn-1"), "TurnCompleted", TestHelpers.Json(new { ok = true }));
        await eventStore.AppendAsync(sessionB.SessionId, new TurnId("turn-2"), "ToolCallCompleted", TestHelpers.Json(new { ok = true }));

        var snapshot = await analytics.GetSnapshotAsync();
        var snapshotAgain = await analytics.GetSnapshotAsync();

        Assert.Equal(2, snapshot.TotalSessions);
        Assert.Equal(1, snapshot.ActiveSessions);
        Assert.Contains(snapshot.SessionsByStatus, item => item.Status == SessionStatus.Created && item.Count == 1);
        Assert.Contains(snapshot.SessionsByStatus, item => item.Status == SessionStatus.Completed && item.Count == 1);
        Assert.Contains(snapshot.MessagesByRole, item => item.Role == PromptMessageRole.User && item.Count == 2);
        Assert.Contains(snapshot.MessagesByRole, item => item.Role == PromptMessageRole.Assistant && item.Count == 1);
        Assert.Contains(snapshot.EventsByType, item => item.EventType == "TurnCompleted" && item.Count == 1);
        Assert.Contains(snapshot.EventsByType, item => item.EventType == "ToolCallCompleted" && item.Count == 1);
        Assert.Contains(snapshot.MessagesPerSession, item => item.SessionId == sessionA.SessionId && item.Count == 2);
        Assert.Contains(snapshot.MessagesPerSession, item => item.SessionId == sessionB.SessionId && item.Count == 2);
        Assert.Contains(snapshot.BlocksByType, item => item.BlockType == "text" && item.Count == 3);
        Assert.Contains(snapshot.BlocksByType, item => item.BlockType == "tool_use" && item.Count == 1);
        Assert.Contains(snapshot.BlocksByType, item => item.BlockType == "tool_result" && item.Count == 1);
        Assert.Contains(snapshot.BlocksByRoleAndType, item => item.Role == PromptMessageRole.Assistant && item.BlockType == "tool_use" && item.Count == 1);
        Assert.Contains(snapshot.BlocksByRoleAndType, item => item.Role == PromptMessageRole.Tool && item.BlockType == "tool_result" && item.Count == 1);
        Assert.Equal(snapshot.TotalSessions, snapshotAgain.TotalSessions);
        Assert.Equal(snapshot.ActiveSessions, snapshotAgain.ActiveSessions);
        Assert.Equal(snapshot.SessionsByStatus, snapshotAgain.SessionsByStatus);
        Assert.Equal(snapshot.MessagesByRole, snapshotAgain.MessagesByRole);
        Assert.Equal(snapshot.EventsByType, snapshotAgain.EventsByType);
        Assert.Equal(snapshot.MessagesPerSession, snapshotAgain.MessagesPerSession);
        Assert.Equal(snapshot.BlocksByType, snapshotAgain.BlocksByType);
        Assert.Equal(snapshot.BlocksByRoleAndType, snapshotAgain.BlocksByRoleAndType);
    }

    [Fact]
    public void AddClawSharp_RegistersAnalyticsService_ForEnabledAndDisabledDuckDb()
    {
        using var enabledProvider = CreateServiceProvider(enabled: true);
        using var disabledProvider = CreateServiceProvider(enabled: false);

        var enabled = enabledProvider.GetRequiredService<ISessionAnalyticsService>();
        var disabled = disabledProvider.GetRequiredService<ISessionAnalyticsService>();

        Assert.IsType<DuckDbSessionAnalyticsService>(enabled);
        Assert.IsType<NullSessionAnalyticsService>(disabled);
    }

    private ServiceProvider CreateServiceProvider(bool enabled)
    {
        var services = new ServiceCollection();
        services.AddClawSharp(builder =>
        {
            builder.Configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Runtime:WorkspaceRoot"] = _root,
                    ["Databases:DuckDb:Enabled"] = enabled.ToString(),
                    ["Databases:Sqlite:DatabasePath"] = Path.Combine(_root, $"di-{enabled}.db"),
                    ["Databases:DuckDb:DatabasePath"] = Path.Combine(_root, $"di-{enabled}.duckdb")
                })
                .Build();
        });
        return services.BuildServiceProvider();
    }

    private ClawOptions CreateOptions(bool enabled) => new()
    {
        Runtime = new RuntimeOptions { WorkspaceRoot = _root },
        Sessions = new SessionOptions { DatabasePath = Path.Combine(_root, "legacy.db") },
        Databases = new DatabaseOptions
        {
            Sqlite = new SqliteDatabaseOptions { DatabasePath = Path.Combine(_root, "runtime.db") },
            DuckDb = new DuckDbDatabaseOptions
            {
                Enabled = enabled,
                DatabasePath = Path.Combine(_root, "analytics.duckdb")
            }
        }
    };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
