# Data Model: Runtime Performance Optimization

## Entities

### AgentLaunchPlanCacheEntry
Stores a pre-parsed launch plan for a specific agent definition version.
- **AgentId** (`string`): Unique identifier of the agent.
- **DefinitionHash** (`string`): Hash of the Markdown definition to detect changes.
- **LaunchPlan** (`AgentLaunchPlan`): The resolved object (excluding history).
- **LastUsedAt** (`DateTime`): For potential LRU eviction.

### McpClientPoolEntry
Manages a single long-lived connection to an MCP server.
- **ServerId** (`string`): Key representing the MCP server configuration.
- **Client** (`IMcpClient`): The active, initialized client.
- **Status** (`McpConnectionStatus`): Connected, Faulted, etc.
- **LastActivityAt** (`DateTime`): Used for idle timeout cleanup.

### PerformanceMetrics (DuckDB)
Existing analytics will be extended to track cache hit/miss and MCP handshake avoidance.
- **SessionId** (`Guid`): Reference to the session.
- **CacheHit** (`bool`): Whether `AgentLaunchPlan` was loaded from cache.
- **McpHandshakeAvoided** (`bool`): Whether a pooled connection was used.
- **TurnDurationMs** (`long`): Total time taken for the turn (to measure improvement).

## Relationships
- `ClawRuntime` OWNS `AgentLaunchPlanCache`.
- `McpService` OWNS `McpClientPool`.
- `DefinitionWatcher` TRIGGERS invalidation in `AgentLaunchPlanCache`.
- `Session` REUSES connections from `McpClientPool`.
