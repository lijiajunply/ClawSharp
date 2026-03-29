using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Memory;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Skills;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Tests;

public sealed class RuntimeTests
{
    [Fact]
    public async Task Runtime_PreparesAuthorizedArtifactsForAgent()
    {
        var options = new ClawOptions
        {
            Runtime = new RuntimeOptions { WorkspaceRoot = Directory.GetCurrentDirectory() },
            WorkspacePolicy = new WorkspacePolicy
            {
                Permissions = new ToolPermissionSet(
                    ToolCapability.FileRead | ToolCapability.SystemInspect,
                    [],
                    [],
                    [],
                    false,
                    false,
                    30,
                    2048)
            },
            Mcp = new McpOptions
            {
                Servers =
                [
                    new McpServerDefinition { Name = "filesystem", Command = "/bin/echo", Arguments = "ok" }
                ]
            }
        };

        var agent = new AgentDefinition(
            "planner",
            "Planner",
            "desc",
            "gpt",
            "prompt",
            ["file.read", "system.info", "shell.run"],
            ["summarize"],
            "workspace",
            ["filesystem"],
            new ToolPermissionSet(ToolCapability.FileRead | ToolCapability.ShellExecute, [], [], [], false, false, 10, 1024),
            "v1",
            "");
        var skill = new SkillDefinition("summarize", "Summarize", "desc", [], [], [], [], [], "scripts/run.sh", "v1", "");

        var kernel = new ClawKernel(
            options,
            new FakeAgentRegistry(agent),
            new FakeSkillRegistry(skill),
            new ToolRegistry([new FileReadTool(), new ShellRunTool(), new SystemInfoTool()]),
            new MemoryIndex(options, new SimpleEmbeddingProvider(options), new InMemoryVectorStore()),
            new McpClientManager(new McpServerCatalog(options)));

        var runtime = new ClawRuntime(kernel, new McpServerCatalog(options), new LocalAgentProcessLauncher(options));
        var plan = await runtime.PrepareAgentAsync(new AgentLaunchRequest("planner", "session-1"));

        Assert.Equal("planner", plan.Agent.Id);
        Assert.Single(plan.Skills);
        Assert.Single(plan.McpServers);
        Assert.DoesNotContain(plan.Tools, tool => tool.Name == "shell.run");
        Assert.Contains(plan.Tools, tool => tool.Name == "file.read");
    }

    private sealed class FakeAgentRegistry(AgentDefinition agent) : IAgentRegistry
    {
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IReadOnlyCollection<AgentDefinition> GetAll() => [agent];
        public AgentDefinition Get(string id) => agent;
    }

    private sealed class FakeSkillRegistry(SkillDefinition skill) : ISkillRegistry
    {
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IReadOnlyCollection<SkillDefinition> GetAll() => [skill];
        public SkillDefinition Get(string id) => skill;
    }
}
