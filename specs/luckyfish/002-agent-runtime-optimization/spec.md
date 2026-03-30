# Feature Specification: Agent Runtime Strategy Optimization

**Feature Branch**: `luckyfish/002-agent-runtime-optimization`  
**Created**: 2026-03-30  
**Status**: Completed
  
**Input**: User description: "核心功能增强：Agent 运行时策略优化"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Secure Least-Privilege Execution (Priority: P1)

As a system administrator, I want the runtime to enforce the intersection of Agent permissions and Workspace policies so that Agents cannot exceed the safety boundaries defined for the entire project.

**Why this priority**: This is the core security requirement to prevent Agent "jailbreaking" or accidental high-privilege tool usage.

**Independent Test**: Can be fully tested by creating an Agent with "Shell.Execute" capability in a Workspace where the policy explicitly disables it. The test delivers value by proving that the policy override works.

**Acceptance Scenarios**:

1. **Given** a Workspace policy that disables `shell.execute`, **When** an Agent with `shell.execute` declared in its definition is launched, **Then** the effective permission set MUST NOT contain `shell.execute`.
2. **Given** an Agent that declares no specific permissions, **When** it is launched, **Then** it MUST inherit only the permissions allowed by the Workspace policy.

---

### User Story 2 - Dynamic Tool Discovery (Priority: P2)

As a developer, I want my Agent to be able to discover and utilize newly added tools or MCP servers that match its authorized capabilities without manual re-registration of every single tool in the agent definition.

**Why this priority**: Enhances extensibility and reduces the maintenance burden of keeping `agent.md` files updated with every new tool.

**Independent Test**: Can be tested by adding a new tool to the registry that matches an existing Agent's capability bitmask and verifying the Agent can immediately see/use it in the next turn.

**Acceptance Scenarios**:

1. **Given** an Agent authorized for `network.access`, **When** a new MCP tool with `network.access` is registered, **Then** the Agent SHOULD be able to list and call this tool in its next execution turn.

---

### User Story 3 - Mandatory System Tools (Priority: P3)

As a platform owner, I want to ensure certain "Mandatory" tools (like telemetry or specialized logging) are always available to every Agent, even if the Agent author did not explicitly include them in the `agent.md`.

**Why this priority**: Critical for system-wide observability and compliance.

**Independent Test**: Can be tested by defining a tool as "Global Mandatory" in `appsettings.json` and verifying it appears in the tool list of an Agent that has an empty `tools` list.

**Acceptance Scenarios**:

1. **Given** a tool marked as `Mandatory` in the workspace policy, **When** any Agent is launched, **Then** that tool MUST be included in the `AgentLaunchPlan` regardless of the Agent's local `tools` configuration.

---

### User Story 4 - User-in-the-Loop Authorization (Priority: P1)

As a user, I want the system to prompt me for authorization whenever an Agent or mandatory tool requires a capability that hasn't been explicitly granted, so that I maintain full control over my environment's security.

**Why this priority**: Ensures transparency and prevents "silent" privilege escalation, aligning with the "Apple-style" permission model.

**Independent Test**: Can be tested by triggering a mandatory tool that requires a missing capability and verifying that a blocking UI/CLI prompt appears asking for user consent.

**Acceptance Scenarios**:

1. **Given** a tool that requires a capability not in the Agent's current mask, **When** the tool is invoked, **Then** the system MUST pause and request explicit user authorization.
2. **Given** a user prompt for authorization, **When** the user approves, **Then** the capability MUST be granted for the current session/turn and the tool should proceed.
3. **Given** a user prompt for authorization, **When** the user denies, **Then** the tool invocation MUST fail gracefully with a "Permission Denied" error.

---

### Edge Cases

- **Conflicting Overrides**: What happens when a Workspace policy mandates a tool that requires a capability the Agent specifically lacks? (Solution: Trigger User-in-the-Loop authorization to grant the capability for that specific mandatory tool execution).
- **Runtime Policy Update**: How does the system handle a policy change while a turn is in progress? (Assumption: The current turn completes with old permissions; the next turn uses the new policy).
- **Empty Intersections**: If the intersection of Agent permissions and Workspace policy results in zero tools, how is the Agent notified?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST calculate the effective permission set as the intersection (`AND`) of Agent-declared capabilities and Workspace-enforced capabilities.
- **FR-002**: System MUST support "Mandatory Tools" defined at the Workspace/Global level that bypass the Agent's explicit tool list.
- **FR-003**: System MUST provide a centralized `PermissionResolver` that consolidates `ClawOptions.WorkspacePolicy`, `AgentDefinition.Permissions`, and any active `Session` context.
- **FR-004**: System MUST log a `PermissionAudit` event whenever a tool is filtered out from an Agent's plan due to policy restrictions or when a JIT permission is requested.
- **FR-005**: System MUST allow dynamic discovery of MCP tools based on the effective capability bitmask of the active session.
- **FR-006**: System MUST implement a "Just-In-Time Permission" (JIT) prompt. If a mandatory tool or agent tool requires a capability not currently authorized, the system MUST prompt the user for manual approval.

### Key Entities *(include if feature involves data)*

- **PermissionResolver**: The logic component responsible for merging policies and definitions into an effective permission set.
- **EffectivePermissionSet**: The resulting runtime object containing authorized capabilities, path roots, and tool whitelists.
- **WorkspacePolicy**: The high-level configuration (from `ClawOptions`) that defines the "safety ceiling" for all Agents in a specific workspace.
- **PermissionRequest**: An ephemeral object representing a request for JIT capability elevation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of tool invocations in the `ClawRuntime` are validated against the `EffectivePermissionSet` before execution.
- **SC-002**: Initialization overhead for calculating effective permissions MUST be less than 5ms per session start.
- **SC-003**: All "Mandatory" workspace tools are successfully injected into 100% of Agent sessions.
- **SC-004**: 100% of capability elevations (JIT) must be explicitly approved by a user interaction.

## Assumptions

- **Policy Precedence**: Workspace-level safety policies always take precedence over Agent-level declarations in case of conflicts (Security-First).
- **Capability Bitmasks**: The existing `ToolCapability` flags are sufficient for the first iteration of this optimization.
- **Turn-based Consistency**: Permissions remain static for the duration of a single "Turn" (User input -> Assistant response) unless elevated via JIT prompt.
- **User-in-the-Loop**: Users are available to provide JIT approvals during interactive sessions. For non-interactive sessions, JIT requests fail by default.
