# Tasks: Introduce Multi-Agent Orchestrator (SupervisorAgent)

Feature Branch: `luckyfish/002-multi-agent-orchestrator`

## Phase 1: Setup
- [X] T001 Define core interfaces `IOrchestratorAgent`, `IAgentToolProvider`, and `IPermissionScopeManager` in `ClawSharp.Lib/Agents/OrchestrationContracts.cs`
- [X] T002 Create `DelegationContext` and `PermissionScope` data models in `ClawSharp.Lib/Agents/OrchestrationModels.cs`
- [X] T003 Add `OrchestrationOptions` to `ClawSharp.Lib/Configuration/ClawOptions.cs` to hold `MaxDelegationDepth` and default strategy

## Phase 2: Foundational
- [X] T004 [P] Implement `AgentTool` wrapping `IAgent` as `ITool` in `ClawSharp.Lib/Agents/AgentTool.cs`
- [X] T005 [P] Implement `PermissionScopeManager` to enforce Least Privilege in `ClawSharp.Lib/Runtime/PermissionScopeManager.cs`
- [X] T006 Integrate `PermissionScopeManager` into `ToolExecutor` to validate every tool call in `ClawSharp.Lib/Runtime/ToolExecutor.cs`
- [X] T007 Add `DelegationStack` tracking to `TurnContext` in `ClawSharp.Lib/Runtime/RuntimeContracts.cs`

## Phase 3: User Story 1 - Task Delegation and Orchestration [US1]
- [X] T008 [US1] Create `SupervisorAgent` class inheriting from `Agent` in `ClawSharp.Lib/Agents/SupervisorAgent.cs`
- [X] T009 [US1] Implement multi-step planning logic in `SupervisorAgent.GeneratePlanAsync`
- [X] T010 [US1] Implement step execution logic in `SupervisorAgent.ExecutePlanAsync` using `AgentTool`
- [X] T011 [P] [US1] Create integration test `MultiAgentOrchestrationTests.cs` using mock agents (echo/reverse) in `ClawSharp.Lib.Tests/`
- [X] T012 [US1] Implement state/context passing between sub-agent calls in `SupervisorAgent.cs`

## Phase 4: User Story 2 - Agent Discovery as Tools [US2]
- [X] T013 [US2] Implement `FileSystemAgentToolProvider` to discover agents from workspace in `ClawSharp.Lib/Agents/FileSystemAgentToolProvider.cs`
- [X] T014 [US2] Register `IAgentToolProvider` in DI container in `ClawSharp.Lib/Configuration/ServiceCollectionExtensions.cs`
- [X] T015 [US2] Update `SupervisorAgent` to automatically load discovered agents as tools during initialization
- [X] T016 [P] [US2] Add test case for automatic agent discovery in `ClawSharp.Lib.Tests/RegistryTests.cs`

## Phase 5: User Story 3 - Interactive Orchestration Feedback [US3]
- [X] T017 [US3] Define `DelegationStarted` and `DelegationCompleted` events in `ClawSharp.Lib/Runtime/RuntimeContracts.cs`
- [X] T018 [US3] Update `SupervisorAgent` to fire delegation events during execution
- [X] T019 [US3] Update `ClawSharp.CLI/Infrastructure/MarkdownRenderer.cs` to handle and display delegation events
- [X] T020 [US3] Enhance CLI output to show the "thought process" markers for the supervisor

## Final Phase: Polish & Cross-Cutting Concerns
- [X] T021 Implement circular delegation detection using `DelegationStack` (SC-004) in `ClawSharp.Lib/Runtime/ToolExecutor.cs`
- [X] T022 Enforce `MaxDelegationDepth` limit in `SupervisorAgent.cs` (FR-006)
- [X] T023 Add performance metrics collection for delegation overhead (SC-002) using DuckDB in `ClawSharp.Lib/Runtime/AnalyticsServices.cs`
- [X] T024 Perform final end-to-end integration test with real agents in the workspace

## Dependency Graph
- Phase 1 -> Phase 2 (Foundational infrastructure needed for all logic)
- Phase 2 -> Phase 3 (Supervisor needs AgentTool and PermissionScope)
- Phase 3 -> Phase 4 (Discovery enhances the supervisor)
- Phase 3 -> Phase 5 (Feedback depends on supervisor events)
- Phase 3 -> Final Phase (Loop detection needs supervisor execution logic)

## Parallel Execution Examples
- [US1]: T011 (Tests) can be developed in parallel with T008-T010 (Logic).
- [US2]: T013 (Discovery) and T016 (Registry Tests) can be done in parallel.
- [Foundational]: T004 (AgentTool) and T005 (Permission Manager) are independent.
