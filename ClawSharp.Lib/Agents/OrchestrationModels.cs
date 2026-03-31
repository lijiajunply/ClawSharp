namespace ClawSharp.Lib.Agents;

using System.Collections.Generic;
using ClawSharp.Lib.Runtime;

/// <summary>
/// Tracks the active multi-agent interaction.
/// </summary>
public record DelegationContext
{
    /// <summary>
    /// List of Agent IDs in the current delegation chain.
    /// </summary>
    public List<string> CallStack { get; init; } = new();
    
    /// <summary>
    /// Correlation ID for tracing across agents.
    /// </summary>
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Current permission scope for this delegation.
    /// </summary>
    public PermissionScope? Permissions { get; init; }
}

/// <summary>
/// Enforces Least Privilege (FR-007).
/// </summary>
public record PermissionScope
{
    /// <summary>
    /// Whitelist of tool names that are allowed to be invoked.
    /// </summary>
    public HashSet<string> AllowedToolNames { get; init; } = new();
    
    /// <summary>
    /// Resource constraints, e.g., allowed file system paths.
    /// </summary>
    public Dictionary<string, object> ResourceConstraints { get; init; } = new();
}

/// <summary>
/// Provides context for the current turn, including orchestration state.
/// </summary>
public record TurnContext(
    SessionId SessionId,
    TurnId TurnId,
    DelegationContext DelegationStack,
    PermissionScope Permissions,
    System.Threading.CancellationToken CancellationToken = default);

/// <summary>
/// Represents an orchestration plan step.
/// </summary>
public record PlanStep(string AgentName, string Task);

/// <summary>
/// Represents a multi-step orchestration plan.
/// </summary>
public record Plan(List<PlanStep> Steps);
