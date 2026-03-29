using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ClawSharp.Lib.Tests;

public sealed class CliIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "claw-cli-tests", Guid.NewGuid().ToString("N"));

    public CliIntegrationTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddClawSharp(builder =>
                {
                    builder.BasePath = _root;
                    builder.Override("Runtime:WorkspaceRoot", _root);
                    builder.Override("Sessions:DatabasePath", Path.Combine(_root, "cli.db"));
                    builder.Override("Providers:DefaultProvider", "stub");
                });
                
                // Ensure a stub agent exists for testing
                services.AddSingleton<IAgentRegistry>(sp => {
                    var agent = new AgentDefinition(
                        "planner", "Planner", "desc", "", "stub-model", "sys", [], [], "workspace", [], Tools.ToolPermissionSet.Empty, "v1", "");
                    return new FakeAgentRegistry(agent);
                });
            })
            .Build();
    }

    [Fact]
    public async Task Cli_Init_CreatesDirectoryAndDatabase()
    {
        using var host = CreateHost();
        var runtime = host.Services.GetRequiredService<IClawRuntime>();

        await runtime.InitializeAsync();

        Assert.True(Directory.Exists(_root));
    }

    [Fact]
    public async Task Cli_Chat_CanStartSession()
    {
        using var host = CreateHost();
        var runtime = host.Services.GetRequiredService<IClawRuntime>();
        await runtime.InitializeAsync();

        var session = await runtime.StartSessionAsync("planner");
        Assert.NotNull(session);
        Assert.Equal("planner", session.Record.AgentId);
    }
}

internal class FakeAgentRegistry : IAgentRegistry
{
    private readonly AgentDefinition _agent;
    public FakeAgentRegistry(AgentDefinition agent) => _agent = agent;

    public IAgentDefinitionStore Store => new FakeStore(_agent);

    public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IReadOnlyCollection<AgentDefinition> GetAll() => [_agent];

    public AgentDefinition Get(string id) => id == _agent.Id ? _agent : throw new KeyNotFoundException();

    private class FakeStore : IAgentDefinitionStore
    {
        private readonly AgentDefinition _agent;
        public FakeStore(AgentDefinition agent) => _agent = agent;
        public Task<AgentDefinition?> GetAsync(string id) => Task.FromResult<AgentDefinition?>(id == _agent.Id ? _agent : null);
        public Task<IReadOnlyList<AgentDefinition>> ListAsync() => Task.FromResult<IReadOnlyList<AgentDefinition>>([_agent]);
        public Task<IReadOnlyList<AgentDefinition>> LoadAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AgentDefinition>>([_agent]);
    }
}
