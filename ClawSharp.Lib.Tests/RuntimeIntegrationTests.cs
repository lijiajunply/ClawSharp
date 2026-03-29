using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Memory;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Skills;
using ClawSharp.Lib.Tools;
using System.Net;
using System.Text;

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

    [Fact]
    public async Task Runtime_WithResponsesProvider_CompletesTextTurn()
    {
        var runtime = CreateOpenAiRuntime(
            [
                """
data: {"type":"response.output_text.delta","delta":"Hello "}

data: {"type":"response.output_text.delta","delta":"from provider"}

data: {"type":"response.completed","response":{"usage":{"input_tokens":3,"output_tokens":2,"total_tokens":5}}}

"""
            ],
            out var historyStore,
            out _);

        var session = await runtime.StartSessionAsync("planner");
        await runtime.AppendUserMessageAsync(session.Record.SessionId, "hello");

        var result = await runtime.RunTurnAsync(session.Record.SessionId);
        var messages = await historyStore.ListAsync(session.Record.SessionId);

        Assert.Equal(SessionStatus.Completed, result.Status);
        Assert.Contains(messages, message => message.Role == PromptMessageRole.Assistant && message.Content == "Hello from provider");
    }

    [Fact]
    public async Task Runtime_WithResponsesProvider_HandlesToolTurn()
    {
        var runtime = CreateOpenAiRuntime(
            [
                """
data: {"type":"response.output_item.done","item":{"type":"function_call","call_id":"call_1","name":"system.info","arguments":"{}"}}

data: {"type":"response.completed","response":{"usage":{"input_tokens":3,"output_tokens":1,"total_tokens":4}}}

""",
                """
data: {"type":"response.output_text.delta","delta":"Tool handled"}

data: {"type":"response.completed","response":{"usage":{"input_tokens":4,"output_tokens":2,"total_tokens":6}}}

"""
            ],
            out var historyStore,
            out var eventStore);

        var session = await runtime.StartSessionAsync("planner");
        await runtime.AppendUserMessageAsync(session.Record.SessionId, "run tool");

        var result = await runtime.RunTurnAsync(session.Record.SessionId);
        var messages = await historyStore.ListAsync(session.Record.SessionId);
        var events = await eventStore.ListAsync(session.Record.SessionId);

        Assert.Equal(1, result.ToolCallCount);
        Assert.Contains(messages, message => message.Role == PromptMessageRole.Tool && message.Name == "system.info");
        Assert.Contains(events, @event => @event.EventType == "ToolCallCompleted");
    }

    [Fact]
    public async Task Runtime_WithAnthropicProvider_HandlesNativeToolTurn()
    {
        var runtime = CreateAnthropicRuntime(
            [
                """
data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"system.info","input":{}}}

data: {"type":"content_block_stop","index":0}

data: {"type":"message_delta","delta":{"stop_reason":"tool_use"},"usage":{"input_tokens":3,"output_tokens":1}}

data: {"type":"message_stop"}

""",
                """
data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"Claude handled tool"}}

data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"input_tokens":4,"output_tokens":2}}

data: {"type":"message_stop"}

"""
            ],
            out var historyStore,
            out var eventStore);

        var session = await runtime.StartSessionAsync("planner");
        await runtime.AppendUserMessageAsync(session.Record.SessionId, "run tool");

        var result = await runtime.RunTurnAsync(session.Record.SessionId);
        var messages = await historyStore.ListAsync(session.Record.SessionId);
        var events = await eventStore.ListAsync(session.Record.SessionId);

        Assert.Equal(1, result.ToolCallCount);
        Assert.Contains(messages, message => message.Role == PromptMessageRole.Tool && message.Name == "system.info");
        Assert.Contains(messages, message => message.Role == PromptMessageRole.Assistant && message.Content == "Claude handled tool");
        Assert.Contains(events, @event => @event.EventType == "ToolCallCompleted");
    }

    [Fact]
    public async Task Runtime_GetHistory_ReturnsBlocksAwareReplayEntries()
    {
        var runtime = CreateAnthropicRuntime(
            [
                """
data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"system.info","input":{}}}

data: {"type":"content_block_stop","index":0}

data: {"type":"message_delta","delta":{"stop_reason":"tool_use"},"usage":{"input_tokens":3,"output_tokens":1}}

data: {"type":"message_stop"}

""",
                """
data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"Claude handled tool"}}

data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"input_tokens":4,"output_tokens":2}}

data: {"type":"message_stop"}

"""
            ],
            out _,
            out _);

        var session = await runtime.StartSessionAsync("planner");
        await runtime.AppendUserMessageAsync(session.Record.SessionId, "run tool");
        await runtime.RunTurnAsync(session.Record.SessionId);

        var history = await runtime.GetHistoryAsync(session.Record.SessionId);

        Assert.Contains(history, entry =>
            entry.Message?.Role == PromptMessageRole.Tool &&
            entry.ReplayBlocks.OfType<ModelToolResultBlock>().Any(block => block.ToolName == "system.info"));

        Assert.Contains(history, entry =>
            entry.Event?.EventType == "ToolCallRequested" &&
            entry.ReplayBlocks.OfType<ModelToolUseBlock>().Any(block => block.Name == "system.info"));

        Assert.Contains(history, entry =>
            entry.Event?.EventType == "ToolCallCompleted" &&
            entry.ReplayBlocks.OfType<ModelToolResultBlock>().Any(block => block.ToolName == "system.info"));

        Assert.Contains(history, entry =>
            entry.Event?.EventType == "TurnCompleted" &&
            entry.ReplayBlocks.OfType<ModelTextBlock>().Any(block => block.Text == "Claude handled tool"));
    }

    private ClawRuntime CreateRuntime(out IPromptHistoryStore historyStore, out ISessionEventStore eventStore)
    {
        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = _root },
            Sessions = new SessionOptions { DatabasePath = Path.Combine(_root, "runtime.db") },
            Providers = new ProviderOptions
            {
                DefaultProvider = "stub",
                DefaultModel = "stub-model",
                Models =
                [
                    new ModelProviderOptions
                    {
                        Name = "stub",
                        Type = "stub",
                        BaseUrl = "http://localhost/stub",
                        DefaultModel = "stub-model",
                        SupportsResponses = true
                    }
                ]
            },
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
            "",
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
            new DefaultAgentWorkerLauncher(options, new StdioJsonRpcAgentWorkerClient(), registry),
            new JsonSessionSerializer());
    }

    private ClawRuntime CreateOpenAiRuntime(IReadOnlyList<string> ssePayloads, out IPromptHistoryStore historyStore, out ISessionEventStore eventStore)
    {
        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = _root },
            Sessions = new SessionOptions { DatabasePath = Path.Combine(_root, $"runtime-{Guid.NewGuid():N}.db") },
            Providers = new ProviderOptions
            {
                DefaultProvider = "openai",
                Models =
                [
                    new ModelProviderOptions
                    {
                        Name = "openai",
                        Type = "openai-responses",
                        BaseUrl = "https://api.openai.com",
                        ApiKey = "key",
                        DefaultModel = "gpt-test",
                        SupportsResponses = true
                    }
                ]
            },
            WorkspacePolicy = new WorkspacePolicy
            {
                Permissions = new ToolPermissionSet(
                    ToolCapability.SystemInspect,
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
            "",
            "gpt-runtime",
            "You are helpful",
            ["system.info"],
            [],
            "workspace",
            [],
            new ToolPermissionSet(ToolCapability.SystemInspect, [], [], [], false, false, 10, 1024),
            "v1",
            "");

        var payloadQueue = new Queue<string>(ssePayloads);
        var handler = new ProviderTestsProxyHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payloadQueue.Count > 0 ? payloadQueue.Dequeue() : ssePayloads[^1], Encoding.UTF8, "text/event-stream")
        });
        var provider = new OpenAiResponsesModelProvider(options, new ProviderTestsProxyFactory(handler));
        var registry = new ModelProviderRegistry([provider, new StubModelProvider()]);
        var resolver = new ModelProviderResolver(options, registry);
        var kernel = new ClawKernel(
            options,
            new FakeAgentRegistry(agent),
            new FakeSkillRegistry(),
            new ToolRegistry([new SystemInfoTool()]),
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
            new DefaultAgentWorkerLauncher(options, new StdioJsonRpcAgentWorkerClient(), registry),
            new JsonSessionSerializer());
    }

    private ClawRuntime CreateAnthropicRuntime(IReadOnlyList<string> ssePayloads, out IPromptHistoryStore historyStore, out ISessionEventStore eventStore)
    {
        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = _root },
            Sessions = new SessionOptions { DatabasePath = Path.Combine(_root, $"runtime-anthropic-{Guid.NewGuid():N}.db") },
            Providers = new ProviderOptions
            {
                DefaultProvider = "claude",
                Models =
                [
                    new ModelProviderOptions
                    {
                        Name = "claude",
                        Type = "anthropic-messages",
                        BaseUrl = "https://api.anthropic.com",
                        ApiKey = "key",
                        DefaultModel = "claude-sonnet"
                    }
                ]
            },
            WorkspacePolicy = new WorkspacePolicy
            {
                Permissions = new ToolPermissionSet(
                    ToolCapability.SystemInspect,
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
            "",
            "claude-sonnet",
            "You are helpful",
            ["system.info"],
            [],
            "workspace",
            [],
            new ToolPermissionSet(ToolCapability.SystemInspect, [], [], [], false, false, 10, 1024),
            "v1",
            "");

        var payloadQueue = new Queue<string>(ssePayloads);
        var handler = new ProviderTestsProxyHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payloadQueue.Count > 0 ? payloadQueue.Dequeue() : ssePayloads[^1], Encoding.UTF8, "text/event-stream")
        });
        var provider = new AnthropicMessagesModelProvider(options, new ProviderTestsProxyFactory(handler));
        var registry = new ModelProviderRegistry([provider, new StubModelProvider()]);
        var resolver = new ModelProviderResolver(options, registry);
        var kernel = new ClawKernel(
            options,
            new FakeAgentRegistry(agent),
            new FakeSkillRegistry(),
            new ToolRegistry([new SystemInfoTool()]),
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
            new DefaultAgentWorkerLauncher(options, new StdioJsonRpcAgentWorkerClient(), registry),
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

    private sealed class ProviderTestsProxyFactory(HttpMessageHandler handler) : IProviderHttpClientFactory
    {
        public HttpClient CreateClient(ResolvedModelTarget target) =>
            new(handler) { BaseAddress = new Uri(target.BaseUrl.EndsWith('/') ? target.BaseUrl : $"{target.BaseUrl}/", UriKind.Absolute) };
    }

    private sealed class ProviderTestsProxyHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
