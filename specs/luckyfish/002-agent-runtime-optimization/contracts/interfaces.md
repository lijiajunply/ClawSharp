# Interface Contracts: Agent Runtime Strategy Optimization

## IPermissionResolver (ClawSharp.Lib)

Responsibility: Consolidate configuration and definition into a single source of truth for runtime permissions.

```csharp
namespace ClawSharp.Lib.Runtime;

public interface IPermissionResolver
{
    /// <summary>
    /// Calculates the effective permission set for a session based on Agent def and WorkspacePolicy.
    /// </summary>
    ToolPermissionSet Resolve(AgentDefinition agent, WorkspacePolicy policy);

    /// <summary>
    /// Intersects two path lists based on common root logic.
    /// </summary>
    IReadOnlyList<string> IntersectPaths(IReadOnlyList<string> agentPaths, IReadOnlyList<string> policyPaths);
}
```

## IPermissionUI (ClawSharp.Lib / Consumer Apps)

Responsibility: Provide the mechanism for JIT capability elevation via user-in-the-loop interaction.

```csharp
namespace ClawSharp.Lib.Runtime;

public interface IPermissionUI
{
    /// <summary>
    /// Prompts the user to grant a specific capability to an Agent.
    /// </summary>
    /// <returns>True if granted, False if denied.</returns>
    Task<bool> RequestCapabilityAsync(string agentId, ToolCapability capability, string toolName, CancellationToken ct);
}
```

## Mandatory Tool Registry (Configuration)

```json
{
  "WorkspacePolicy": {
    "Capabilities": "FileRead, FileWrite, NetworkAccess",
    "MandatoryTools": ["audit_logger", "telemetry_collector"],
    "AllowedReadRoots": ["./logs"],
    "AllowedWriteRoots": ["./logs"]
  }
}
```
