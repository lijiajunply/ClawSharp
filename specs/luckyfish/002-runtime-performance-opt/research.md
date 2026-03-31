# Research: Runtime Performance Optimization

## Findings

### 1. Agent Launch Plan Caching
- **Current State**: `PrepareAgentAsync` in `ClawRuntime` is called at the beginning of every `RunTurnStreamingAsync`. It re-resolves permissions, sessions, history, and tools/skills/MCP servers. This is redundant as agent definitions only change on disk.
- **Optimization Strategy**: Implement a `ConcurrentDictionary<string, AgentLaunchPlan>` (keyed by `AgentId` or `SessionId`) in `ClawRuntime`.
- **Cache Invalidation**: Subscribe to `DefinitionWatcher.OnChanged` events (via `IAgentRegistry` / `ISkillRegistry` reload notifications) to clear the corresponding cache entries.
- **History Separation**: Since history is truly dynamic, it should be loaded after retrieving a cached `AgentLaunchPlan`, or the plan should only cache static components (Definition, Tools, Skills, MCP Servers).

### 2. MCP Connection Pooling
- **Current State**: `kernel.Mcp.ConnectAsync` is called before each turn. If using `McpStdioTransport`, this might restart child processes or perform heavy handshakes unnecessarily.
- **Optimization Strategy**: Refactor `McpService` to maintain a pool of `McpClient` instances.
- **Lifecycle Management**: Introduce an idle timeout (default 10 minutes) for pooled connections. Use a heartbeat or health check to prune dead connections.
- **Handshake Avoidance**: Once initialized, reuse the `McpClient` for subsequent tool calls within the same session or across sessions if the server configuration is identical.

### 3. Manual Reload Capability
- **Current State**: System relies entirely on `DefinitionWatcher` (FileSystemWatcher).
- **Optimization Strategy**: Add a new command/method to `IClawRuntime` or `IClawKernel` to trigger a global or specific reload. This will be exposed to the CLI as `/reload`.

## Decisions
- **Decision**: Cache `AgentLaunchPlan` in `ClawRuntime`.
- **Rationale**: `ClawRuntime` is the orchestrator and has the context of sessions and registries.
- **Alternatives Considered**: Caching in `AgentRegistry`. Rejected because `AgentLaunchPlan` includes resolved tools/skills which cross multiple registries.

- **Decision**: Implement a fixed-timeout connection pool in `McpService`.
- **Rationale**: Centralizes resource management and simplifies `ClawRuntime` logic.

## Remaining Unknowns
- None. (All technical details for initial implementation are clear).
