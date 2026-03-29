using System.Diagnostics;
using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Memory;
using ClawSharp.Lib.Mcp;
using ClawSharp.Lib.Skills;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Runtime;

public sealed record RuntimeSession(string SessionId, string AgentId, string WorkspaceRoot, ToolPermissionSet EffectivePermissions);

public sealed record AgentLaunchRequest(string AgentId, string SessionId);

public sealed record AgentLaunchPlan(
    RuntimeSession Session,
    AgentDefinition Agent,
    IReadOnlyList<SkillDefinition> Skills,
    IReadOnlyList<ToolDefinition> Tools,
    IReadOnlyList<McpServerDefinition> McpServers);

public sealed record AgentProcessHandle(int? ProcessId, string? CommandLine);

public interface IClawKernel
{
    ClawOptions Options { get; }

    IAgentRegistry Agents { get; }

    ISkillRegistry Skills { get; }

    IToolRegistry Tools { get; }

    IMemoryIndex Memory { get; }

    IMcpClientManager Mcp { get; }
}

public interface IClawRuntime
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<AgentLaunchPlan> PrepareAgentAsync(AgentLaunchRequest request, CancellationToken cancellationToken = default);

    Task<AgentProcessHandle> LaunchAgentProcessAsync(AgentLaunchPlan plan, CancellationToken cancellationToken = default);
}

public interface IAgentProcessLauncher
{
    Task<AgentProcessHandle> LaunchAsync(AgentLaunchPlan plan, CancellationToken cancellationToken = default);
}

public sealed class ClawKernel(
    ClawOptions options,
    IAgentRegistry agents,
    ISkillRegistry skills,
    IToolRegistry tools,
    IMemoryIndex memory,
    IMcpClientManager mcp) : IClawKernel
{
    public ClawOptions Options => options;

    public IAgentRegistry Agents => agents;

    public ISkillRegistry Skills => skills;

    public IToolRegistry Tools => tools;

    public IMemoryIndex Memory => memory;

    public IMcpClientManager Mcp => mcp;
}

public sealed class ClawRuntime(
    IClawKernel kernel,
    IMcpServerCatalog serverCatalog,
    IAgentProcessLauncher processLauncher) : IClawRuntime
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await kernel.Agents.ReloadAsync(cancellationToken).ConfigureAwait(false);
        await kernel.Skills.ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<AgentLaunchPlan> PrepareAgentAsync(AgentLaunchRequest request, CancellationToken cancellationToken = default)
    {
        var agent = kernel.Agents.Get(request.AgentId);
        var permissions = agent.Permissions.Merge(kernel.Options.WorkspacePolicy.Permissions);
        var session = new RuntimeSession(request.SessionId, request.AgentId, kernel.Options.Runtime.WorkspaceRoot, permissions);

        var skills = agent.Skills.Select(kernel.Skills.Get).ToArray();
        var tools = kernel.Tools.GetAuthorizedTools(permissions)
            .Where(x => agent.Tools.Count == 0 || agent.Tools.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var mcpServers = agent.McpServers.Select(serverCatalog.Get).ToArray();

        return Task.FromResult(new AgentLaunchPlan(session, agent, skills, tools, mcpServers));
    }

    public Task<AgentProcessHandle> LaunchAgentProcessAsync(AgentLaunchPlan plan, CancellationToken cancellationToken = default)
    {
        return processLauncher.LaunchAsync(plan, cancellationToken);
    }
}

public sealed class LocalAgentProcessLauncher(ClawOptions options) : IAgentProcessLauncher
{
    public Task<AgentProcessHandle> LaunchAsync(AgentLaunchPlan plan, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Runtime.AgentWorkerCommand))
        {
            return Task.FromResult(new AgentProcessHandle(null, null));
        }

        var arguments = $"{options.Runtime.AgentWorkerArguments} --agent {plan.Agent.Id} --session {plan.Session.SessionId}".Trim();
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(options.Runtime.AgentWorkerCommand, arguments)
            {
                WorkingDirectory = options.Runtime.WorkspaceRoot,
                UseShellExecute = false
            }
        };

        process.Start();
        return Task.FromResult(new AgentProcessHandle(process.Id, $"{options.Runtime.AgentWorkerCommand} {arguments}".Trim()));
    }
}
