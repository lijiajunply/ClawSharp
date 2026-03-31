# Feature Specification: Introduce Multi-Agent Orchestrator (SupervisorAgent)

**Feature Branch**: `luckyfish/002-multi-agent-orchestrator`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "迈向多 Agent (Multi-Agent) * 引入 Orchestrator 模式: 增加一个特殊的 SupervisorAgent，它可以将其他 Agent 视为“工具”进行调用。这是对齐 OpenClaw “编排平台”定义的关键一步。"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Task Delegation and Orchestration (Priority: P1)

As a user, I want to provide a complex, multi-step task to the system (e.g., "Research a topic and write a summary"), so that a SupervisorAgent can automatically break it down and delegate sub-tasks to specialized agents (e.g., a "Researcher" and a "Writer") without my manual intervention for each step.

**Why this priority**: This is the core value of the orchestrator pattern, transforming the system from a single-agent chat to a collaborative platform.

**Independent Test**: Can be tested by giving the SupervisorAgent access to two simple mock agents (one for "echo" and one for "reverse") and verifying it calls them in sequence to process a string.

**Acceptance Scenarios**:

1. **Given** a SupervisorAgent and two specialized agents (A and B), **When** a task requires both A and B's capabilities, **Then** the SupervisorAgent correctly identifies and calls A, receives output, and passes it to B.
2. **Given** a multi-step task, **When** the first sub-task fails, **Then** the SupervisorAgent attempts to retry or informs the user of the specific failure point.

---

### User Story 2 - Agent Discovery as Tools (Priority: P2)

As a system administrator or developer, I want my existing agents to be automatically registered as "tools" for the SupervisorAgent, so that I don't have to manually wrap every agent in a tool definition.

**Why this priority**: Enhances usability and reduces friction when scaling the number of specialized agents in the workspace.

**Independent Test**: Verify that a new agent definition added to the `workspace/agents` folder is immediately visible as an available tool in the SupervisorAgent's capability list.

**Acceptance Scenarios**:

1. **Given** a collection of agents in the registry, **When** the SupervisorAgent initializes, **Then** all compatible agents are listed as available tools with descriptions derived from their metadata.

---

### User Story 3 - Interactive Orchestration Feedback (Priority: P3)

As a user, I want to see the "thought process" and delegation steps of the SupervisorAgent in real-time, so that I can understand how my task is being handled and which agent is doing what.

**Why this priority**: Essential for transparency and debugging complex multi-agent flows.

**Independent Test**: Check the session history/output stream for specific "delegation" events or markers indicating which sub-agent was invoked.

**Acceptance Scenarios**:

1. **Given** a delegated task, **When** a sub-agent is working, **Then** the UI/CLI displays a clear indicator (e.g., "Supervisor delegated to [Agent Name]").

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST implement a `SupervisorAgent` that inherits from or extends the base `Agent` definition.
- **FR-002**: The `SupervisorAgent` MUST be capable of discovering other registered agents and treating them as tool calls.
- **FR-003**: The system MUST automatically generate tool schemas (parameters, descriptions) for agents based on their `AgentDefinition` metadata.
- **FR-004**: The `SupervisorAgent` MUST support iterative reasoning (e.g., ReAct pattern or similar) to handle complex, non-linear tasks.
- **FR-005**: The `SupervisorAgent` MUST be able to pass state/context between different sub-agent calls.
- **FR-006**: The system MUST provide a way to limit the "depth" or "budget" of delegation to prevent infinite loops between agents.
- **FR-007**: The system MUST enforce a **Least Privilege** model for delegation. When the SupervisorAgent invokes a sub-agent, that sub-agent is only granted the minimum set of tool permissions required for its specific task, as defined by the Supervisor's delegation context.

### Key Entities *(include if feature involves data)*

- **SupervisorAgent**: A specialized agent capable of planning and tool invocation.
- **AgentTool**: A wrapper that exposes an `IAgent` as an `ITool`, mapping agent prompts to tool execution.
- **DelegationRecord**: An entry in the session history tracking the caller (Supervisor) and the callee (Sub-Agent).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can execute a task requiring at least two different specialized agents with a single prompt.
- **SC-002**: The overhead of delegating a task (excluding LLM inference time) should be less than 500ms.
- **SC-003**: 100% of registered agents in the `workspace/agents` folder are accessible as tools by the SupervisorAgent if they have valid descriptions.
- **SC-004**: System successfully detects and breaks a "circular delegation" loop (Agent A calls Agent B calls Agent A) within 3 iterations.

## Assumptions

- **Existing Tool System**: We assume the current `ClawSharp.Lib` has a tool calling mechanism that can be extended to support agent-based tools.
- **Metadata Quality**: We assume that sub-agents have descriptive `About` or `Description` fields in their YAML frontmatter to allow the Supervisor to understand their purpose.
- **LLM Capability**: We assume the underlying LLM (e.g., GPT-4o, Claude 3.5 Sonnet) used by the Supervisor is capable of reliable tool use and planning.
- **Synchronous Execution**: For the initial implementation, sub-agent calls are assumed to be synchronous (sequential) within a single supervisor's planning step.
