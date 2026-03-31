# Data Model: Multi-Agent Orchestrator

## Entities

### SupervisorAgent
- Represents the primary task orchestrator.
- **Properties**:
    - `OrchestrationStrategy`: Strategy (e.g., Sequential, Parallel, DAG-based).
    - `MaxDelegationDepth`: Integer (Constraint FR-006).
- **Behavior**:
    - `PlanAsync(TaskInput input)`: Generates a list of steps.
    - `ExecuteStepAsync(Step step)`: Invocates an `AgentTool`.

### AgentTool (implements ITool)
- Wraps an `IAgent` to be consumed by the Supervisor.
- **Properties**:
    - `AgentRef`: Instance of `IAgent`.
    - `Metadata`: Schema derived from `AgentDefinition`.
- **Logic**:
    - Mapping parameters from `ITool.Execute` to the Agent's internal prompt.

### DelegationContext
- Tracks the active multi-agent interaction.
- **Fields**:
    - `CallStack`: List<string> (Agent IDs).
    - `CorrelationId`: Guid (for tracing).

### PermissionScope
- Enforces Least Privilege (FR-007).
- **Fields**:
    - `AllowedToolNames`: HashSet<string>.
    - `ResourceConstraints`: JSON/Map (e.g., file system paths allowed).

## Relationships
- `SupervisorAgent` (1) -> Use -> `AgentTool` (N)
- `AgentTool` (1) -> Wraps -> `IAgent` (1)
- `TurnContext` (1) -> Holds -> `DelegationContext` (1)
- `TurnContext` (1) -> Holds -> `PermissionScope` (1)
