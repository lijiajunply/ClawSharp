using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Tools;
using System.Text.Json;

namespace ClawSharp.Lib.Tests;

public sealed class PermissionTests
{
    [Fact]
    public void PermissionResolver_ShouldIntersectCapabilities()
    {
        var resolver = new PermissionResolver();
        
        var agent = new AgentDefinition(
            "test", "Test", "Desc", "", "", "Prompt", [], [], "workspace", [],
            new ToolPermissionSet(ToolCapability.ShellExecute | ToolCapability.FileRead, [], [], []),
            "v1", "Body");

        var policy = new WorkspacePolicy
        {
            Permissions = new ToolPermissionSet(ToolCapability.FileRead | ToolCapability.NetworkAccess, [], [], [])
        };

        var effective = resolver.Resolve(agent, policy);

        // Intersection should be only FileRead
        Assert.Equal(ToolCapability.FileRead, effective.Capabilities);
        Assert.False(effective.Capabilities.HasFlag(ToolCapability.ShellExecute));
    }

    [Fact]
    public void PermissionResolver_ShouldIntersectPaths()
    {
        var resolver = new PermissionResolver();

        var agent = new AgentDefinition(
            "test", "Test", "Desc", "", "", "Prompt", [], [], "workspace", [],
            new ToolPermissionSet(ToolCapability.FileRead, ["/a", "/b"], [], []),
            "v1", "Body");

        var policy = new WorkspacePolicy
        {
            Permissions = new ToolPermissionSet(ToolCapability.FileRead, ["/b", "/c"], [], [])
        };

        var effective = resolver.Resolve(agent, policy);

        Assert.Single(effective.AllowedReadRoots);
        Assert.Contains("/b", effective.AllowedReadRoots);
    }

    [Fact]
    public void ToolRegistry_ShouldFilterByCapabilities()
    {
        var executors = new List<IToolExecutor>
        {
            new StubTool("t1", ToolCapability.FileRead),
            new StubTool("t2", ToolCapability.ShellExecute)
        };
        var registry = new ToolRegistry(executors, Array.Empty<IAgentToolProvider>(), new PermissionScopeManager());

        var permissions = new ToolPermissionSet(ToolCapability.FileRead, [], [], []);
        var authorized = registry.GetAuthorizedTools(permissions);

        Assert.Single(authorized);
        Assert.Equal("t1", authorized.First().Name);
    }

    [Fact]
    public void ToolRegistry_ShouldSupportMandatoryMerge()
    {
        var executors = new List<IToolExecutor>
        {
            new StubTool("t1", ToolCapability.None),
            new StubTool("mandatory_logger", ToolCapability.None)
        };
        var registry = new ToolRegistry(executors, Array.Empty<IAgentToolProvider>(), new PermissionScopeManager());

        var agentTools = new[] { "t1" };
        var mandatoryTools = new[] { "mandatory_logger" };
        
        var toolNames = new HashSet<string>(agentTools, StringComparer.OrdinalIgnoreCase);
        foreach(var t in mandatoryTools) toolNames.Add(t);

        var authorized = registry.GetAuthorizedTools(new ToolPermissionSet(ToolCapability.None, [], [], []))
            .Where(x => toolNames.Contains(x.Name))
            .ToArray();

        Assert.Equal(2, authorized.Length);
        Assert.Contains(authorized, x => x.Name == "t1");
        Assert.Contains(authorized, x => x.Name == "mandatory_logger");
    }

    [Fact]
    public void PermissionResolver_ShouldBeFast()
    {
        var resolver = new PermissionResolver();
        var agent = new AgentDefinition("t", "T", "D", "", "", "P", [], [], "w", [], new ToolPermissionSet(ToolCapability.FileRead, [], [], []), "v", "B");
        var policy = new WorkspacePolicy { Permissions = new ToolPermissionSet(ToolCapability.FileRead, [], [], []) };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            resolver.Resolve(agent, policy);
        }
        sw.Stop();

        // SC-002: <5ms per resolution. 1000 resolutions should be < 5000ms.
        Assert.True(sw.ElapsedMilliseconds < 5000);
    }

    private sealed class StubTool(string name, ToolCapability cap) : IToolExecutor
    {
        public ToolDefinition Definition { get; } = new(name, "desc", null, null, cap);
        public Task<ToolInvocationResult> ExecuteAsync(ToolExecutionContext context, JsonElement arguments) => throw new NotImplementedException();
    }
}
