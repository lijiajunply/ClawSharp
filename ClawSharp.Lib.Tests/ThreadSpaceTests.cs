using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;

namespace ClawSharp.Lib.Tests;

public sealed class ThreadSpaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claw-threadspace-tests", Guid.NewGuid().ToString("N"));

    public ThreadSpaceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task ThreadSpaceManager_EnsuresSingleGlobalThreadSpace()
    {
        var options = CreateOptions("threadspaces.db");
        var store = new SqliteThreadSpaceStore(options);
        var sessionStore = new SqliteSessionStore(options);
        var manager = new ThreadSpaceManager(store, sessionStore, options);

        var first = await manager.EnsureDefaultAsync();
        var second = await manager.EnsureDefaultAsync();
        var all = await manager.ListAsync();

        Assert.Equal("global", first.Name);
        Assert.True(first.IsGlobal);
        Assert.Null(first.BoundFolderPath);
        Assert.Equal(first.ThreadSpaceId, second.ThreadSpaceId);
        Assert.Single(all);
    }

    [Fact]
    public async Task ThreadSpaceManager_RejectsPathOutsideWorkspace()
    {
        var options = CreateOptions("outside.db");
        var store = new SqliteThreadSpaceStore(options);
        var sessionStore = new SqliteSessionStore(options);
        var manager = new ThreadSpaceManager(store, sessionStore, options);
        var outside = Path.Combine(Path.GetTempPath(), "outside-threadspace", Guid.NewGuid().ToString("N"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.CreateAsync(new CreateThreadSpaceRequest("outside", outside)));

        Assert.Contains("must be within workspace root", error.Message);
    }

    [Fact]
    public async Task ThreadSpaceManager_RejectsDuplicateNameAndPath()
    {
        var options = CreateOptions("duplicates.db");
        var store = new SqliteThreadSpaceStore(options);
        var sessionStore = new SqliteSessionStore(options);
        var manager = new ThreadSpaceManager(store, sessionStore, options);
        var docs = Path.Combine(_root, "docs");

        Directory.CreateDirectory(docs);
        var created = await manager.CreateAsync(new CreateThreadSpaceRequest("docs", docs));

        var sameName = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.CreateAsync(new CreateThreadSpaceRequest("docs", Path.Combine(_root, "another"))));
        var samePath = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.CreateAsync(new CreateThreadSpaceRequest("another", docs)));

        Assert.Contains("already exists", sameName.Message);
        Assert.Contains("already exists", samePath.Message);
        Assert.Equal(created.ThreadSpaceId, (await manager.GetByNameAsync("docs"))!.ThreadSpaceId);
    }

    [Fact]
    public async Task ThreadSpaceManager_CanListSessionsWithinThreadSpace()
    {
        var options = CreateOptions("sessions.db");
        var threadSpaceStore = new SqliteThreadSpaceStore(options);
        var sessionStore = new SqliteSessionStore(options);
        var manager = new ThreadSpaceManager(threadSpaceStore, sessionStore, options);
        var sessionManager = new SessionManager(sessionStore);
        var docs = await manager.CreateAsync(new CreateThreadSpaceRequest("docs", Path.Combine(_root, "docs")));
        var notes = await manager.CreateAsync(new CreateThreadSpaceRequest("notes", Path.Combine(_root, "notes")));

        var docsSession = await sessionManager.StartAsync("planner", docs.ThreadSpaceId, docs.BoundFolderPath ?? _root);
        await sessionManager.StartAsync("planner", notes.ThreadSpaceId, notes.BoundFolderPath ?? _root);

        var sessions = await manager.ListSessionsAsync(docs.ThreadSpaceId);

        var only = Assert.Single(sessions);
        Assert.Equal(docsSession.Record.SessionId, only.SessionId);
        Assert.Equal(docs.ThreadSpaceId, only.ThreadSpaceId);
    }

    [Fact]
    public async Task SqliteStores_CompatibilityConstructorsUseInitializedSchema()
    {
        var options = CreateOptions("compatibility.db");
        var threadSpaceStore = new SqliteThreadSpaceStore(options);
        var sessionStore = new SqliteSessionStore(options);

        var global = await threadSpaceStore.GetByNameAsync("global");
        Assert.NotNull(global);

        await sessionStore.CreateAsync(new SessionRecord(new SessionId("compat-session"), global!.ThreadSpaceId, "planner", _root, SessionStatus.Created, DateTimeOffset.UtcNow));

        var loaded = await sessionStore.GetAsync(new SessionId("compat-session"));
        Assert.NotNull(loaded);
        Assert.Equal(global.ThreadSpaceId, loaded!.ThreadSpaceId);
    }

    private ClawOptions CreateOptions(string databaseFile) => new()
    {
        Runtime = new RuntimeOptions { WorkspaceRoot = _root },
        Databases = new DatabaseOptions
        {
            Sqlite = new SqliteDatabaseOptions { DatabasePath = Path.Combine(_root, databaseFile) }
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