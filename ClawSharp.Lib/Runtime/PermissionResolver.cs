using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClawSharp.Lib.Runtime;

/// <summary>
/// 默认的权限解析器实现。
/// </summary>
public sealed class PermissionResolver(ILogger<PermissionResolver>? logger = null) : IPermissionResolver
{
    private readonly ILogger _logger = logger ?? NullLogger<PermissionResolver>.Instance;

    /// <inheritdoc />
    public ToolPermissionSet Resolve(AgentDefinition agent, WorkspacePolicy policy)
    {
        var agentPerms = agent.Permissions;
        var policyPerms = policy.Permissions;

        // 计算能力位交集 (Intersection of capabilities)
        var effectiveCapabilities = agentPerms.Capabilities & policyPerms.Capabilities;

        var dropped = agentPerms.Capabilities & ~policyPerms.Capabilities;
        if (dropped != ToolCapability.None)
        {
            _logger.LogWarning("Agent '{AgentId}' requested capabilities [{Requested}] but Workspace Policy restricts them. Dropped: [{Dropped}]", 
                agent.Id, agentPerms.Capabilities, dropped);
        }

        // 路径求交集
        var allowedReadRoots = IntersectPaths(agentPerms.AllowedReadRoots, policyPerms.AllowedReadRoots);
        var allowedWriteRoots = IntersectPaths(agentPerms.AllowedWriteRoots, policyPerms.AllowedWriteRoots);
        
        // 合并命令白名单
        var allowedCommands = IntersectPaths(agentPerms.AllowedCommands, policyPerms.AllowedCommands);

        return new ToolPermissionSet(
            effectiveCapabilities,
            allowedReadRoots,
            allowedWriteRoots,
            allowedCommands,
            agentPerms.ApprovalRequired || policyPerms.ApprovalRequired,
            agentPerms.ReadOnlyFileSystem || policyPerms.ReadOnlyFileSystem,
            MinNullable(agentPerms.TimeoutSeconds, policyPerms.TimeoutSeconds),
            MinNullable(agentPerms.MaxOutputLength, policyPerms.MaxOutputLength));
    }

    private static IReadOnlyCollection<string> IntersectPaths(IReadOnlyCollection<string> agentPaths, IReadOnlyCollection<string> policyPaths)
    {
        if (agentPaths.Count == 0) return policyPaths.ToArray();
        if (policyPaths.Count == 0) return agentPaths.ToArray();

        // 简单的字符串交集，实际可能需要更复杂的路径包含关系判断，但目前按规格说明先实现基础交集
        return agentPaths.Intersect(policyPaths, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static int? MinNullable(int? left, int? right)
    {
        if (left == null) return right;
        if (right == null) return left;
        return Math.Min(left.Value, right.Value);
    }
}
