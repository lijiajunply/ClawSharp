# Research: Multi-Agent Orchestrator (SupervisorAgent)

## R-001: Orchestrator Pattern Implementation
- **Decision**: Implement `SupervisorAgent` as a specialized class inheriting from a new base `OrchestratorAgent` (or directly from `Agent`).
- **Rationale**: While `PlannerAgent` exists, a `SupervisorAgent` needs specific logic for managing a pool of sub-agents and maintaining a global task graph.
- **Alternatives considered**: Modifying `PlannerAgent`. Rejected because it would overcomplicate the existing planner which is designed for tool-use rather than agent-delegation.

## R-002: Agent-to-Tool Conversion (AgentTool)
- **Decision**: Create an `AgentTool` class that implements `ITool`. This class will wrap an `IAgent` instance.
- **Rationale**: This allows the existing tool invocation logic in `ClawSharp.Lib` to work with agents without modification.
- **Mapping**: 
    - `ToolName` = `AgentDefinition.Name`
    - `Description` = `AgentDefinition.About`
    - `Parameters` = A single string field (e.g., "query" or "task") or a schema derived from the agent's expected input if defined.
- **Execution**: `AgentTool.ExecuteAsync` will trigger a new sub-session or a nested turn within the current runtime.

## R-003: Circular Delegation Prevention
- **Decision**: Use a `DelegationStack` (List of Agent IDs) passed in the `TurnContext`.
- **Rationale**: Before an agent is invoked as a tool, the system checks if its ID is already in the stack.
- **Detection**: If ID exists, block invocation and return a "Circular delegation detected" error to the supervisor (SC-004).

## R-004: Least Privilege Permission Model
- **Decision**: Introduce a `PermissionScope` in the `ToolInvocationContext`.
- **Rationale**: When the `SupervisorAgent` calls an `AgentTool`, it can optionally provide a whitelist of tool names that the sub-agent is allowed to use.
- **Enforcement**: The `ToolExecutor` will validate every tool call against the current `PermissionScope`.
