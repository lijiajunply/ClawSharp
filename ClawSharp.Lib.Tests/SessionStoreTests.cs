using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClawSharp.Lib.Tests;

public sealed class SessionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claw-session-tests", Guid.NewGuid().ToString("N"));

    public SessionStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task SqliteStores_PersistSessionsMessagesAndEventsInOrder()
    {
        var options = CreateOptions();
        var threadSpaceStore = new SqliteThreadSpaceStore(options);
        var sessionStore = new SqliteSessionStore(options);
        var historyStore = new SqlitePromptHistoryStore(options);
        var eventStore = new SqliteSessionEventStore(options);
        var init = await threadSpaceStore.GetByNameAsync("init");

        var session = new SessionRecord(new SessionId("session-a"), init!.ThreadSpaceId, "planner", _root, SessionStatus.Created, DateTimeOffset.UtcNow);
        await sessionStore.CreateAsync(session);

        var turnId = new TurnId("turn-a");
        await historyStore.AppendAsync(session.SessionId, turnId, PromptMessageRole.User, "hello");
        await eventStore.AppendAsync(session.SessionId, turnId, "MessageAppended", TestHelpers.Json(new { role = "user" }));
        await historyStore.AppendAsync(session.SessionId, turnId, PromptMessageRole.Assistant, "world");

        var loaded = await sessionStore.GetAsync(session.SessionId);
        var messages = await historyStore.ListAsync(session.SessionId);
        var events = await eventStore.ListAsync(session.SessionId);

        Assert.NotNull(loaded);
        Assert.Equal(2, messages.Count);
        Assert.Single(events);
        Assert.True(messages[0].SequenceNo < messages[1].SequenceNo);
    }

    [Fact]
    public async Task SqliteStores_HonorLegacySessionDatabasePathConfiguration()
    {
        var legacyPath = Path.Combine(_root, "legacy-state.db");
        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = _root },
            Sessions = new SessionOptions { DatabasePath = legacyPath }
        };

        var threadSpaceStore = new SqliteThreadSpaceStore(options);
        var sessionStore = new SqliteSessionStore(options);
        var init = await threadSpaceStore.GetByNameAsync("init");
        await sessionStore.CreateAsync(new SessionRecord(new SessionId("legacy-session"), init!.ThreadSpaceId, "planner", _root, SessionStatus.Created, DateTimeOffset.UtcNow));

        Assert.True(File.Exists(legacyPath));
    }

    [Fact]
    public async Task SqliteStores_PersistStructuredBlocksWithoutFlattening()
    {
        var options = CreateOptions();
        var threadSpaceStore = new SqliteThreadSpaceStore(options);
        var historyStore = new SqlitePromptHistoryStore(options);
        var sessionStore = new SqliteSessionStore(options);
        var init = await threadSpaceStore.GetByNameAsync("init");
        var session = new SessionRecord(new SessionId("session-blocks"), init!.ThreadSpaceId, "planner", _root, SessionStatus.Created, DateTimeOffset.UtcNow);
        await sessionStore.CreateAsync(session);

        var turnId = new TurnId("turn-blocks");
        await historyStore.AppendBlocksAsync(
            session.SessionId,
            turnId,
            PromptMessageRole.Assistant,
            [new ModelToolUseBlock("call_1", "system.info", """{"verbose":true}""")]);

        var messages = await historyStore.ListAsync(session.SessionId);
        var message = Assert.Single(messages);
        var toolUse = Assert.IsType<ModelToolUseBlock>(Assert.Single(message.Blocks));

        Assert.Equal("call_1", toolUse.Id);
        Assert.Equal("system.info", toolUse.Name);
        Assert.Equal("""{"verbose":true}""", toolUse.ArgumentsJson);
        Assert.Equal("system.info", message.Name);
        Assert.Equal("call_1", message.ToolCallId);
    }

    [Fact]
    public async Task AddClawSharp_ResolvesStoresThroughDbContextFactory()
    {
        var services = new ServiceCollection();
        services.AddClawSharp(builder =>
        {
            builder.Configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Runtime:WorkspaceRoot"] = _root,
                    ["Databases:Sqlite:DatabasePath"] = Path.Combine(_root, "di-runtime.db"),
                    ["Databases:DuckDb:Enabled"] = false.ToString()
                })
                .Build();
        });

        using var provider = services.BuildServiceProvider();
        var sessionStore = provider.GetRequiredService<ISessionStore>();
        var historyStore = provider.GetRequiredService<IPromptHistoryStore>();
        var eventStore = provider.GetRequiredService<ISessionEventStore>();
        var threadSpaces = provider.GetRequiredService<IThreadSpaceStore>();

        var init = await threadSpaces.GetByNameAsync("init");
        var session = new SessionRecord(new SessionId("di-session"), init!.ThreadSpaceId, "planner", _root, SessionStatus.Created, DateTimeOffset.UtcNow);
        await sessionStore.CreateAsync(session);
        await historyStore.AppendAsync(session.SessionId, new TurnId("di-turn"), PromptMessageRole.User, "hello");
        await eventStore.AppendAsync(session.SessionId, new TurnId("di-turn"), "TurnCompleted", TestHelpers.Json(new { ok = true }));

        Assert.NotNull(await sessionStore.GetAsync(session.SessionId));
        Assert.Single(await historyStore.ListAsync(session.SessionId));
        Assert.Single(await eventStore.ListAsync(session.SessionId));
    }

    private ClawOptions CreateOptions() => new()
    {
        Runtime = new RuntimeOptions { WorkspaceRoot = _root },
        Sessions = new SessionOptions { DatabasePath = Path.Combine(_root, "state.db") }
    };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
