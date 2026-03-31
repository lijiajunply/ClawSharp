# Contract: IClawRuntime & IMcpClientManager Updates

## IClawRuntime (Extended)
A new method to manually invalidate and reload all active configurations.
- **Method**: `ReloadAsync(CancellationToken ct = default)`
- **Responsibility**: 
    - Clear the `AgentLaunchPlanCache`.
    - Trigger `ReloadAsync` on `IAgentRegistry` and `ISkillRegistry`.
    - Invalidate the `McpClientPool` (optional or based on flags).

## IMcpClientManager (Refactored)
Updates to support pooled connections.
- **ConnectAsync**: 
    - Now looks up a client in the `McpClientPool` by `serverId`.
    - If found and healthy, returns it and updates `LastActivityAt`.
    - If not found, initializes a new one and adds it to the pool.
- **ReleaseAsync(string serverId)**:
    - Explicitly marks a client as ready for reuse or closes it if faulted.

## DefinitionWatcher Integration
- **Event**: `OnDefinitionChanged`
- **Hook**: Subscribed to by `ClawRuntime`.
- **Payload**: `AgentId` or `SkillId` of the changed file.
- **Action**: Evict the specific cached `AgentLaunchPlan`.
