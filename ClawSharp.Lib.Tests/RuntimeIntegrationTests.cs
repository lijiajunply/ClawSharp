using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Core;
using ClawSharp.Lib.Memory;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Projects;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Skills;
using ClawSharp.Lib.Tools;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

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
        await runtime.AppendUserMessageAsync(session.Record.SessionId, "tool:system_info:{}");

        var result = await runtime.RunTurnAsync(session.Record.SessionId);
        var messages = await historyStore.ListAsync(session.Record.SessionId);
        var events = await eventStore.ListAsync(session.Record.SessionId);

        Assert.Equal(1, result.ToolCallCount);
        Assert.Contains(messages, message => message.Role == PromptMessageRole.Tool && message.Name == "system_info");
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
data: {"type":"response.output_item.done","item":{"type":"function_call","call_id":"call_1","name":"system_info","arguments":"{}"}}

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
        Assert.Contains(messages, message => message.Role == PromptMessageRole.Tool && message.Name == "system_info");
        Assert.Contains(events, @event => @event.EventType == "ToolCallCompleted");
    }

    [Fact]
    public async Task Runtime_WithAnthropicProvider_HandlesNativeToolTurn()
    {
        var runtime = CreateAnthropicRuntime(
            [
                """
data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"system_info","input":{}}}

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
        Assert.Contains(messages, message => message.Role == PromptMessageRole.Tool && message.Name == "system_info");
        Assert.Contains(messages, message => message.Role == PromptMessageRole.Assistant && message.Content == "Claude handled tool");
        Assert.Contains(events, @event => @event.EventType == "ToolCallCompleted");
    }

    [Fact]
    public async Task Runtime_GetHistory_ReturnsBlocksAwareReplayEntries()
    {
        var runtime = CreateAnthropicRuntime(
            [
                """
data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_1","name":"system_info","input":{}}}

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
            entry.ReplayBlocks.OfType<ModelToolResultBlock>().Any(block => block.ToolName == "system_info"));

        Assert.Contains(history, entry =>
            entry.Event?.EventType == "ToolCallRequested" &&
            entry.ReplayBlocks.OfType<ModelToolUseBlock>().Any(block => block.Name == "system_info"));

        Assert.Contains(history, entry =>
            entry.Event?.EventType == "ToolCallCompleted" &&
            entry.ReplayBlocks.OfType<ModelToolResultBlock>().Any(block => block.ToolName == "system_info"));

        Assert.Contains(history, entry =>
            entry.Event?.EventType == "TurnCompleted" &&
            entry.ReplayBlocks.OfType<ModelTextBlock>().Any(block => block.Text == "Claude handled tool"));
    }

    [Fact]
    public async Task Runtime_StartSessionAsync_DefaultsToGlobalThreadSpace()
    {
        var runtime = CreateRuntime(out _, out _, out var threadSpaces);
        var global = await threadSpaces.GetGlobalAsync();

        var session = await runtime.StartSessionAsync("planner");

        Assert.Equal(global.ThreadSpaceId, session.Record.ThreadSpaceId);
        Assert.Equal(_root, session.Record.WorkspaceRoot);
    }

    [Fact]
    public async Task Runtime_StartSessionAsync_CanTargetExplicitThreadSpace()
    {
        var runtime = CreateRuntime(out _, out _, out var threadSpaces);
        var docsPath = Path.Combine(_root, "docs");
        var docs = await threadSpaces.CreateAsync(new CreateThreadSpaceRequest("docs", docsPath));

        var session = await runtime.StartSessionAsync(new StartSessionRequest("planner", docs.ThreadSpaceId));

        Assert.Equal(docs.ThreadSpaceId, session.Record.ThreadSpaceId);
        Assert.Equal(docs.BoundFolderPath, session.Record.WorkspaceRoot);
    }

    [Fact]
    public async Task Runtime_PrepareAgentAsync_UsesLaunchPlanCacheOnSecondCall()
    {
        var runtime = CreateRuntime(out _, out _);
        await runtime.InitializeAsync();

        var session = await runtime.StartSessionAsync("planner");
        var first = await runtime.PrepareAgentAsync(new AgentLaunchRequest("planner", session.Record.SessionId));
        var second = await runtime.PrepareAgentAsync(new AgentLaunchRequest("planner", session.Record.SessionId));

        Assert.False(first.CacheHit);
        Assert.True(second.CacheHit);
    }

    [Fact]
    public async Task Runtime_ReloadAsync_ClearsLaunchPlanCache()
    {
        var runtime = CreateRuntime(out _, out _);
        await runtime.InitializeAsync();

        var session = await runtime.StartSessionAsync("planner");
        _ = await runtime.PrepareAgentAsync(new AgentLaunchRequest("planner", session.Record.SessionId));
        var cached = await runtime.PrepareAgentAsync(new AgentLaunchRequest("planner", session.Record.SessionId));

        Assert.True(cached.CacheHit);

        await runtime.ReloadAsync();

        var afterReload = await runtime.PrepareAgentAsync(new AgentLaunchRequest("planner", session.Record.SessionId));
        Assert.False(afterReload.CacheHit);
    }

    [Fact]
    public async Task Runtime_PrepareAgentAsync_IsolatesCacheByAgentId()
    {
        var planner = CreateAgent("planner", ["system_info", "shell_run"]);
        var reviewer = CreateAgent("reviewer", ["system_info"]);
        var runtime = CreateRuntime([planner, reviewer], out _, out _);
        await runtime.InitializeAsync();

        var plannerSession = await runtime.StartSessionAsync("planner");
        var reviewerSession = await runtime.StartSessionAsync("reviewer");

        _ = await runtime.PrepareAgentAsync(new AgentLaunchRequest("planner", plannerSession.Record.SessionId));
        var plannerCached = await runtime.PrepareAgentAsync(new AgentLaunchRequest("planner", plannerSession.Record.SessionId));
        var reviewerFirst = await runtime.PrepareAgentAsync(new AgentLaunchRequest("reviewer", reviewerSession.Record.SessionId));
        var reviewerCached = await runtime.PrepareAgentAsync(new AgentLaunchRequest("reviewer", reviewerSession.Record.SessionId));

        Assert.True(plannerCached.CacheHit);
        Assert.False(reviewerFirst.CacheHit);
        Assert.True(reviewerCached.CacheHit);
    }

    [Fact]
    public async Task Runtime_GetEnvironmentDiscoveryAsync_ReturnsUnavailableLocalModels_WhenNothingIsRunning()
    {
        Environment.SetEnvironmentVariable("OLLAMA_HOST", "http://127.0.0.1:9");
        Environment.SetEnvironmentVariable("LLAMAEDGE_HOST", "http://127.0.0.1:9");

        try
        {
            var runtime = CreateRuntime(out _, out _);
            await runtime.InitializeAsync();

            var discovery = await runtime.GetEnvironmentDiscoveryAsync();

            Assert.False(discovery.Ollama.Available);
            Assert.False(discovery.LlamaEdge.Available);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OLLAMA_HOST", null);
            Environment.SetEnvironmentVariable("LLAMAEDGE_HOST", null);
        }
    }

    [Fact]
    public async Task EnvironmentDiscoveryInspector_DetectsOllamaAndLlamaEdge_FromConfiguredHosts()
    {
        using var ollamaServer = await LocalHttpServer.StartAsync("""
{"models":[{"name":"qwen3:latest"}]}
""");
        using var llamaEdgeServer = await LocalHttpServer.StartAsync("""
{"data":[{"id":"llama-edge"}]}
""");

        Environment.SetEnvironmentVariable("OLLAMA_HOST", ollamaServer.BaseUrl);
        Environment.SetEnvironmentVariable("LLAMAEDGE_HOST", llamaEdgeServer.BaseUrl);

        try
        {
            var discovery = await EnvironmentDiscoveryInspector.DiscoverAsync();

            Assert.True(discovery.Ollama.Available);
            Assert.Equal("qwen3:latest", discovery.Ollama.Models[0]);
            Assert.True(discovery.LlamaEdge.Available);
            Assert.Equal("llama-edge", discovery.LlamaEdge.Models[0]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OLLAMA_HOST", null);
            Environment.SetEnvironmentVariable("LLAMAEDGE_HOST", null);
        }
    }

    [Fact]
    public async Task ThreadSpace_ListSessions_CanSelectExistingSessionAndReplayHistory()
    {
        var runtime = CreateRuntime(out _, out _, out var threadSpaces);
        var global = await threadSpaces.GetGlobalAsync();
        var firstSession = await runtime.StartSessionAsync(new StartSessionRequest("planner", global.ThreadSpaceId));
        var secondSession = await runtime.StartSessionAsync(new StartSessionRequest("planner", global.ThreadSpaceId));

        await runtime.AppendUserMessageAsync(firstSession.Record.SessionId, "first question");
        await runtime.RunTurnAsync(firstSession.Record.SessionId);
        await runtime.AppendUserMessageAsync(secondSession.Record.SessionId, "second question");

        var listedSessions = await threadSpaces.ListSessionsAsync(global.ThreadSpaceId);
        var selected = listedSessions.Single(session => session.SessionId == firstSession.Record.SessionId);
        var replayHistory = await runtime.GetHistoryAsync(selected.SessionId);

        Assert.Contains(listedSessions, session => session.SessionId == firstSession.Record.SessionId);
        Assert.Contains(replayHistory, entry => entry.Message?.Content == "first question");
        Assert.Contains(replayHistory, entry => entry.Message?.Role == PromptMessageRole.Assistant && entry.Message.Content == "first question");
    }

    [Fact]
    public async Task ThreadSpace_ReplayLargeSessionHistory_CompletesWithinFiveSeconds()
    {
        var runtime = CreateRuntime(out _, out _, out var threadSpaces);
        var global = await threadSpaces.GetGlobalAsync();
        var session = await runtime.StartSessionAsync(new StartSessionRequest("planner", global.ThreadSpaceId));

        for (var index = 0; index < 200; index++)
        {
            await runtime.AppendUserMessageAsync(session.Record.SessionId, $"question {index}");
            await runtime.RunTurnAsync(session.Record.SessionId);
        }

        var stopwatch = Stopwatch.StartNew();
        var sessions = await threadSpaces.ListSessionsAsync(global.ThreadSpaceId);
        var selected = sessions.Single(record => record.SessionId == session.Record.SessionId);
        var history = await runtime.GetHistoryAsync(selected.SessionId);
        stopwatch.Stop();

        Assert.NotEmpty(history);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Replay exceeded budget: {stopwatch.Elapsed}.");
    }

    private ClawRuntime CreateRuntime(out IPromptHistoryStore historyStore, out ISessionEventStore eventStore) =>
        CreateRuntime(out historyStore, out eventStore, out _);

    private ClawRuntime CreateRuntime(out IPromptHistoryStore historyStore, out ISessionEventStore eventStore, out IThreadSpaceManager threadSpaceManager)
    {
        return CreateRuntime([CreateAgent("planner", ["system_info", "shell_run"])], out historyStore, out eventStore, out threadSpaceManager);
    }

    private ClawRuntime CreateRuntime(
        IReadOnlyCollection<AgentDefinition> agents,
        out IPromptHistoryStore historyStore,
        out ISessionEventStore eventStore)
    {
        return CreateRuntime(agents, out historyStore, out eventStore, out _);
    }

    private ClawRuntime CreateRuntime(
        IReadOnlyCollection<AgentDefinition> agents,
        out IPromptHistoryStore historyStore,
        out ISessionEventStore eventStore,
        out IThreadSpaceManager threadSpaceManager)
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

        var threadSpaceStore = new SqliteThreadSpaceStore(options);
        var sessionStore = new SqliteSessionStore(options);
        historyStore = new SqlitePromptHistoryStore(options);
        eventStore = new SqliteSessionEventStore(options);
        var sessionManager = new SessionManager(sessionStore);
        threadSpaceManager = new ThreadSpaceManager(threadSpaceStore, sessionStore, options);

        var provider = new StubModelProvider();
        var registry = new ModelProviderRegistry([provider]);
        var resolver = new ModelProviderResolver(options, registry);
        var mcpCatalog = new McpServerCatalog(options);
        var mcpManager = new McpClientManager(mcpCatalog, options);
        var kernel = new ClawKernel(
            options,
            new FakeAgentRegistry(agents),
            new FakeSkillRegistry(),
            new ToolRegistry([new SystemInfoTool(), new ShellRunTool()], Array.Empty<IAgentToolProvider>(), new PermissionScopeManager()),
            new MemoryIndex(options, new SimpleEmbeddingProvider(options), new InMemoryVectorStore()),
            mcpManager,
            sessionManager,
            threadSpaceManager,
            historyStore,
            eventStore,
            resolver,
            new FakeProjectScaffolder());

        return new ClawRuntime(
            kernel,
            new McpService(options, mcpManager),
            mcpCatalog,
            sessionStore,
            new DefaultAgentWorkerLauncher(options, new StdioJsonRpcAgentWorkerClient(), registry),
            new JsonSessionSerializer(),
            new PermissionResolver(),
            new PermissionScopeManager(),
            new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider());
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

        var threadSpaceStore = new SqliteThreadSpaceStore(options);
        var sessionStore = new SqliteSessionStore(options);
        historyStore = new SqlitePromptHistoryStore(options);
        eventStore = new SqliteSessionEventStore(options);
        var sessionManager = new SessionManager(sessionStore);
        var threadSpaceManager = new ThreadSpaceManager(threadSpaceStore, sessionStore, options);

        var agent = new AgentDefinition(
            "planner",
            "Planner",
            "desc",
            "",
            "gpt-runtime",
            "You are helpful",
            ["system_info"],
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
        var mcpCatalog = new McpServerCatalog(options);
        var mcpManager = new McpClientManager(mcpCatalog, options);
        var kernel = new ClawKernel(
            options,
            new FakeAgentRegistry([agent]),
            new FakeSkillRegistry(),
            new ToolRegistry([new SystemInfoTool()], Array.Empty<IAgentToolProvider>(), new PermissionScopeManager()),
            new MemoryIndex(options, new SimpleEmbeddingProvider(options), new InMemoryVectorStore()),
            mcpManager,
            sessionManager,
            threadSpaceManager,
            historyStore,
            eventStore,
            resolver,
            new FakeProjectScaffolder());

        return new ClawRuntime(
            kernel,
            new McpService(options, mcpManager),
            mcpCatalog,
            sessionStore,
            new DefaultAgentWorkerLauncher(options, new StdioJsonRpcAgentWorkerClient(), registry),
            new JsonSessionSerializer(),
            new PermissionResolver(),
            new PermissionScopeManager(),
            new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider());
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

        var threadSpaceStore = new SqliteThreadSpaceStore(options);
        var sessionStore = new SqliteSessionStore(options);
        historyStore = new SqlitePromptHistoryStore(options);
        eventStore = new SqliteSessionEventStore(options);
        var sessionManager = new SessionManager(sessionStore);
        var threadSpaceManager = new ThreadSpaceManager(threadSpaceStore, sessionStore, options);

        var agent = new AgentDefinition(
            "planner",
            "Planner",
            "desc",
            "",
            "claude-sonnet",
            "You are helpful",
            ["system_info"],
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
        var mcpCatalog = new McpServerCatalog(options);
        var mcpManager = new McpClientManager(mcpCatalog, options);
        var kernel = new ClawKernel(
            options,
            new FakeAgentRegistry([agent]),
            new FakeSkillRegistry(),
            new ToolRegistry([new SystemInfoTool()], Array.Empty<IAgentToolProvider>(), new PermissionScopeManager()),
            new MemoryIndex(options, new SimpleEmbeddingProvider(options), new InMemoryVectorStore()),
            mcpManager,
            sessionManager,
            threadSpaceManager,
            historyStore,
            eventStore,
            resolver,
            new FakeProjectScaffolder());

        return new ClawRuntime(
            kernel,
            new McpService(options, mcpManager),
            mcpCatalog,
            sessionStore,
            new DefaultAgentWorkerLauncher(options, new StdioJsonRpcAgentWorkerClient(), registry),
            new JsonSessionSerializer(),
            new PermissionResolver(),
            new PermissionScopeManager(),
            new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider());
    }

    private static AgentDefinition CreateAgent(string id, IReadOnlyList<string> tools) =>
        new(
            id,
            char.ToUpperInvariant(id[0]) + id[1..],
            "desc",
            "",
            "stub-model",
            "You are helpful",
            tools,
            [],
            "workspace",
            [],
            new ToolPermissionSet(ToolCapability.SystemInspect | ToolCapability.ShellExecute, [], [], [], false, false, 10, 1024),
            "v1",
            "");

    private sealed class FakeAgentRegistry(IReadOnlyCollection<AgentDefinition> agents) : IAgentRegistry
    {
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IReadOnlyCollection<AgentDefinition> GetAll() => agents;
        public AgentDefinition Get(string id) => agents.Single(agent => agent.Id == id);
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

    private sealed class FakeProjectScaffolder : IProjectScaffolder
    {
        public Task<IReadOnlyList<ProjectTemplateDefinition>> ListTemplatesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectTemplateDefinition>>(Array.Empty<ProjectTemplateDefinition>());

        public Task<OperationResult<CreateProjectResult>> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<CreateProjectResult>.Failure("Fake scaffolder always fails."));

        public Task<OperationResult<ApplySpecKitResult>> ApplySpecKitAsync(string projectRoot, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<ApplySpecKitResult>.Success(
                new ApplySpecKitResult(projectRoot, Array.Empty<string>(), Array.Empty<string>())));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

internal sealed class LocalHttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts;
    private readonly Task _serverTask;

    private LocalHttpServer(HttpListener listener, CancellationTokenSource cts, Task serverTask, string baseUrl)
    {
        _listener = listener;
        _cts = cts;
        _serverTask = serverTask;
        BaseUrl = baseUrl.TrimEnd('/');
    }

    public string BaseUrl { get; }

    public static async Task<LocalHttpServer> StartAsync(string body)
    {
        var port = GetFreePort();
        var prefix = $"http://127.0.0.1:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        var cts = new CancellationTokenSource();
        var serverTask = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await using var writer = new StreamWriter(context.Response.OutputStream);
                await writer.WriteAsync(body);
                await writer.FlushAsync();
                context.Response.Close();
            }
        }, cts.Token);

        var server = new LocalHttpServer(listener, cts, serverTask, prefix);
        await Task.Delay(50);
        return server;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Close();
        try
        {
            _serverTask.GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore listener shutdown races during tests.
        }
        _cts.Dispose();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
