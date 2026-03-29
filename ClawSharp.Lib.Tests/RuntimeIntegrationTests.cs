using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Memory;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Skills;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Tests;

public sealed class RuntimeIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claw-runtime-tests", Guid.NewGuid().ToString("N"));

    public RuntimeIntegrationTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task Runtime_RunTurn_CompletesAndPersistsAssistantMessage()
    {
        var runtime = CreateRuntime(out var historyStore, out var eventStore);
        var session = await runtime.StartSessionAsync("planner");
        await runtime.AppendUserMessageAsync(session.Record.SessionId, "hello runtime");

        var result = await runtime.RunTurnAsync(session.Record.SessionId);
        var history = await runtime.GetHistoryAsync(session.Record.SessionId);
        var messages = await historyStore.ListAsync(session.Record.SessionId);
        var events = await eventStore.ListAsync(session.Record.SessionId);

        Assert.Equal(SessionStatus.Completed, result.Status);
        Assert.Contains(messages, message => message.Role == PromptMessageRole.Assistant && message.Content == "hello runtime");
        Assert.Contains(events, @event => @event.EventType == "TurnCompleted");
        Assert.Equal(history.Count, history.OrderBy(x => x.SequenceNo).Count());
    }

    [Fact]
    public async Task Runtime_RunTurn_HandlesToolRequestAndRecordsEvents()
    {
        var runtime = CreateRuntime(out var historyStore, out var eventStore);
        var session = await runtime.StartSessionAsync("planner");
        await runtime.AppendUserMessageAsync(session.Record.SessionId, "tool:system.info:{}");

        var result = await runtime.RunTurnAsync(session.Record.SessionId);
        var messages = await historyStore.ListAsync(session.Record.SessionId);
        var events = await eventStore.ListAsync(session.Record.SessionId);

        Assert.Equal(1, result.ToolCallCount);
        Assert.Contains(messages, message => message.Role == PromptMessageRole.Tool && message.Name == "system.info");
        Assert.Contains(events, @event => @event.EventType == "ToolCallRequested");
        Assert.Contains(events, @event => @event.EventType == "ToolCallCompleted");
    }

    [Fact]
    public async Task Runtime_CancelSession_UpdatesStatus()
    {
        var runtime = CreateRuntime(out _, out _);
        var session = await runtime.StartSessionAsync("planner");
        await runtime.CancelSessionAsync(session.Record.SessionId);

        var history = await runtime.GetHistoryAsync(session.Record.SessionId);
        Assert.Empty(history);
    }

    private ClawRuntime CreateRuntime(out IPromptHistoryStore historyStore, out ISessionEventStore eventStore)
    {
        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = _root },
            Sessions = new SessionOptions { DatabasePath = Path.Combine(_root, "runtime.db") },
            Providers = new ProviderOptions { DefaultProvider = "stub", DefaultModel = "stub-model" },
            WorkspacePolicy = new WorkspacePolicy
            {
                Permissions = new ToolPermissionSet(
                    ToolCapability.FileRead | ToolCapability.SystemInspect | ToolCapability.ShellExecute,
                    [],
                    [],
                    [],
                    false,
                    false,
                    10,
                    2048)
            }
        };

        var sessionStore = new SqliteSessionStore(options);
        historyStore = new SqlitePromptHistoryStore(options);
        eventStore = new SqliteSessionEventStore(options);
        var sessionManager = new SessionManager(sessionStore);

        var agent = new AgentDefinition(
            "planner",
            "Planner",
            "desc",
            "stub-model",
            "You are helpful",
            ["system.info", "shell.run"],
            [],
            "workspace",
            [],
            new ToolPermissionSet(ToolCapability.SystemInspect | ToolCapability.ShellExecute, [], [], [], false, false, 10, 1024),
            "v1",
            "");

        var provider = new StubModelProvider();
        var registry = new ModelProviderRegistry([provider]);
        var resolver = new ModelProviderResolver(options, registry);
        var kernel = new ClawKernel(
            options,
            new FakeAgentRegistry(agent),
            new FakeSkillRegistry(),
            new ToolRegistry([new SystemInfoTool(), new ShellRunTool()]),
            new MemoryIndex(options, new SimpleEmbeddingProvider(options), new InMemoryVectorStore()),
            new McpClientManager(new McpServerCatalog(options)),
            sessionManager,
            historyStore,
            eventStore,
            resolver);

        return new ClawRuntime(
            kernel,
            new McpServerCatalog(options),
            sessionStore,
            new DefaultAgentWorkerLauncher(options, new StdioJsonRpcAgentWorkerClient(), resolver),
            new JsonSessionSerializer());
    }

    private sealed class FakeAgentRegistry(AgentDefinition agent) : IAgentRegistry
    {
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IReadOnlyCollection<AgentDefinition> GetAll() => [agent];
        public AgentDefinition Get(string id) => agent;
    }

    private sealed class FakeSkillRegistry : ISkillRegistry
    {
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IReadOnlyCollection<SkillDefinition> GetAll() => [];
        public SkillDefinition Get(string id) => throw new KeyNotFoundException(id);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
