using System.Text.Json;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Providers;
using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ClawSharp.Lib.Tests;

public sealed class MultiAgentOrchestrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claw-orchestration-tests", Guid.NewGuid().ToString("N"));

    public MultiAgentOrchestrationTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task AgentTool_CanDelegateToSubAgent()
    {
        // 1. Setup
        var subAgentDef = CreateAgent("echo-agent", "Echoes input.");
        var services = new ServiceCollection();
        
        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = _root },
            Sessions = new SessionOptions { DatabasePath = Path.Combine(_root, "test.db") },
            Providers = new ProviderOptions
            {
                DefaultProvider = "stub",
                DefaultModel = "stub-model",
                Models = [new ModelProviderOptions { Name = "stub", Type = "stub", DefaultModel = "stub-model", SupportsResponses = true }]
            },
            Memory = new MemoryOptions { VectorStoreType = "simple" }
        };

        services.AddClawSharp(builder =>
        {
            builder.BasePath = _root;
            builder.Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:DefaultProvider"] = "stub",
                ["Providers:DefaultModel"] = "stub-model",
                ["Memory:VectorStoreType"] = "simple"
            }).Build();
        });
        
        services.AddSingleton(options);
        services.AddSingleton<IAgentRegistry>(new FakeAgentRegistry([subAgentDef, CreateAgent("caller-agent", "Caller")]));

        var sp = services.BuildServiceProvider();
        var runtime = sp.GetRequiredService<IClawRuntime>();
        var kernel = sp.GetRequiredService<IClawKernel>();
        await runtime.InitializeAsync();

        var subAgent = new SimpleAgent(subAgentDef);
        var agentTool = new AgentTool(subAgent, sp);

        // Start a session for the caller to satisfy database constraints
        var callerSession = await runtime.StartSessionAsync(new StartSessionRequest("caller-agent", (await kernel.ThreadSpaces.GetGlobalAsync()).ThreadSpaceId));
        var userMsg = await runtime.AppendUserMessageAsync(callerSession.Record.SessionId, "start");

        var context = new ToolExecutionContext(
            _root,
            "caller-agent",
            callerSession.Record.SessionId.Value,
            userMsg.TurnId.Value,
            userMsg.MessageId.Value,
            new ToolPermissionSet(ToolCapability.None, [], [], []),
            "trace-1",
            CancellationToken.None);

        // 2. Execute
        var result = await agentTool.ExecuteAsync(context, ToolSecurity.Json(new { query = "hello multi-agent" }));

        // 3. Verify
        Assert.True(result.Status == ToolInvocationStatus.Success, $"Tool failed: {result.Error}");
        var response = result.Payload.GetProperty("response").GetString();
        Assert.Contains("hello multi-agent", response);
    }

    [Fact]
    public async Task SupervisorAgent_CanInitiateOrchestrationTurn()
    {
        // 1. Setup
        var supervisorDef = CreateAgent("supervisor", "Main orchestrator.");
        var services = new ServiceCollection();
        
        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = _root },
            Sessions = new SessionOptions { DatabasePath = Path.Combine(_root, "test-supervisor.db") },
            Providers = new ProviderOptions
            {
                DefaultProvider = "stub",
                DefaultModel = "stub-model",
                Models = [new ModelProviderOptions { Name = "stub", Type = "stub", DefaultModel = "stub-model", SupportsResponses = true }]
            },
            Memory = new MemoryOptions { VectorStoreType = "simple" }
        };

        services.AddClawSharp(builder =>
        {
            builder.BasePath = _root;
            builder.Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:DefaultProvider"] = "stub",
                ["Providers:DefaultModel"] = "stub-model",
                ["Memory:VectorStoreType"] = "simple"
            }).Build();
        });
        
        services.AddSingleton(options);
        services.AddSingleton<IAgentRegistry>(new FakeAgentRegistry([supervisorDef]));

        var sp = services.BuildServiceProvider();
        var runtime = sp.GetRequiredService<IClawRuntime>();
        var kernel = sp.GetRequiredService<IClawKernel>();
        await runtime.InitializeAsync();

        var supervisor = new SupervisorAgent(supervisorDef, runtime);

        var session = await runtime.StartSessionAsync(supervisorDef.Id);
        await runtime.AppendUserMessageAsync(session.Record.SessionId, "Do something complex");

        var turnContext = new TurnContext(
            session.Record.SessionId,
            TurnId.New(),
            new DelegationContext(),
            new PermissionScope(),
            CancellationToken.None);

        // 2. Execute
        var result = await supervisor.ExecutePlanAsync(new Plan(new List<PlanStep>()), turnContext);

        // 3. Verify
        Assert.Equal(SessionStatus.Completed, result.Status);
        Assert.NotNull(result.AssistantMessage);
    }

    [Fact]
    public void FileSystemAgentToolProvider_DiscoversAgentsAsTools()
    {
        // 1. Setup
        var agent1 = CreateAgent("agent1", "Desc 1");
        var agent2 = CreateAgent("agent2", "Desc 2");
        var registry = new FakeAgentRegistry([agent1, agent2]);
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        
        var provider = new FileSystemAgentToolProvider(registry, sp);

        // 2. Execute
        var tools = provider.DiscoverAgentTools().ToList();

        // 3. Verify
        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => t.Definition.Name == "agent1");
        Assert.Contains(tools, t => t.Definition.Name == "agent2");
    }

    private sealed class FakeAgentRegistry(IReadOnlyCollection<AgentDefinition> agents) : IAgentRegistry
    {
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IReadOnlyCollection<AgentDefinition> GetAll() => agents;
        public AgentDefinition Get(string id) => agents.First(a => a.Id == id);
    }

    private AgentDefinition CreateAgent(string id, string desc) =>
        new(id, id, desc, "stub", "stub-model", "You are a helpful assistant.", [], [], "workspace", [], 
            new ToolPermissionSet(ToolCapability.None, [], [], []), "v1", "");

    private class SimpleAgent(AgentDefinition definition) : IAgent
    {
        public AgentDefinition Definition => definition;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }
    }
}
