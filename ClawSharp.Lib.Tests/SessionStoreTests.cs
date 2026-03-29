using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;

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
        var sessionStore = new SqliteSessionStore(options);
        var historyStore = new SqlitePromptHistoryStore(options);
        var eventStore = new SqliteSessionEventStore(options);

        var session = new SessionRecord(new SessionId("session-a"), "planner", _root, SessionStatus.Created, DateTimeOffset.UtcNow);
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

        var sessionStore = new SqliteSessionStore(options);
        await sessionStore.CreateAsync(new SessionRecord(new SessionId("legacy-session"), "planner", _root, SessionStatus.Created, DateTimeOffset.UtcNow));

        Assert.True(File.Exists(legacyPath));
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
