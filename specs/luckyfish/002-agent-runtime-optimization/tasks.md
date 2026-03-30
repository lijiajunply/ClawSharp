# Tasks: Agent Runtime Strategy Optimization

**Input**: Design documents from `/specs/luckyfish/002-agent-runtime-optimization/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: ⚠️ Integration tests are requested in `spec.md` for each story. We will use TDD approach for core logic.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

## Path Conventions

- **Core Library**: `ClawSharp.Lib/`
- **CLI App**: `ClawSharp.CLI/`
- **Tests**: `ClawSharp.Lib.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and base structure for the new permission system.

- [ ] T001 [P] Create `PermissionTests.cs` in `ClawSharp.Lib.Tests/` for TDD
- [ ] T002 Update `ClawOptions.cs` in `ClawSharp.Lib/Configuration/` to include `WorkspacePolicy` class and `MandatoryTools` list
- [ ] T003 Update `ToolCapability.cs` (or equivalent) to ensure all required capability bits are defined

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core logic for permission resolution that blocks all user stories.

- [ ] T004 Define `IPermissionResolver` interface in `ClawSharp.Lib/Runtime/RuntimeContracts.cs`
- [ ] T005 Define `IPermissionUI` interface in `ClawSharp.Lib/Runtime/RuntimeContracts.cs`
- [ ] T006 Implement `PermissionResolver` in `ClawSharp.Lib/Runtime/PermissionResolver.cs` (Core logic: Bitmask intersection)
- [ ] T007 [P] Implement `IntersectPaths` logic in `PermissionResolver.cs` for file roots
- [ ] T008 [P] Register `IPermissionResolver` in `ClawSharp.Lib/Configuration/ServiceCollectionExtensions.cs`

**Checkpoint**: Foundation ready - Logic for intersection can be unit tested.

---

## Phase 3: User Story 1 & 4 - Secure Execution & JIT Prompt (Priority: P1) 🎯 MVP

**Goal**: Enforce least-privilege and provide Apple-style JIT prompts when permissions are missing.

**Independent Test**: Run a session where the agent lacks a capability; verify a CLI prompt appears and granting it allows the turn to proceed.

### Tests for US1 & US4

- [ ] T009 [US1/4] Write integration test in `PermissionTests.cs` for permission intersection (Agent vs Workspace)
- [ ] T010 [US1/4] Write unit test for JIT flow triggering when capability is missing

### Implementation for US1 & US4

- [ ] T011 [US1/4] Update `ClawRuntime.PrepareAgentAsync` in `ClawSharp.Lib/Runtime/RuntimeContracts.cs` to use `IPermissionResolver`
- [ ] T012 [US1/4] Implement `CliPermissionUI` in `ClawSharp.CLI/Infrastructure/CliPermissionUI.cs` using `Spectre.Console`
- [ ] T013 [US1/4] Update `ClawRuntime.HandleToolRequestAsync` to detect missing capabilities and invoke `IPermissionUI.RequestCapabilityAsync`
- [ ] T014 [US1/4] Add `PermissionAudit` events (Requested, Granted, Denied) to `ClawRuntime.cs` using `ISessionEventStore`
- [ ] T015 [US1/4] Register `CliPermissionUI` in `ClawSharp.CLI/Infrastructure/ServiceConfigurator.cs` (or DI setup)

**Checkpoint**: MVP Complete - Basic secure execution with interactive JIT prompt is functional.

---

## Phase 4: User Story 2 - Dynamic Tool Discovery (Priority: P2)

**Goal**: Agents automatically see new tools matching their authorized capabilities.

**Independent Test**: Register a new tool at runtime and verify it's available to an active session without restarting.

### Implementation for US2

- [ ] T016 [US2] Update `ClawRuntime.PrepareAgentAsync` to dynamically filter tools based on the *effective* capability bitmask
- [ ] T017 [US2] Ensure MCP tools are also filtered by the effective permission set
- [ ] T018 [US2] Add unit test verifying a newly added tool is visible to an agent with matching capabilities

---

## Phase 5: User Story 3 - Mandatory System Tools (Priority: P3)

**Goal**: Inject workspace-level mandatory tools into all sessions.

**Independent Test**: Configure a mandatory tool in `appsettings.json` and verify it's present in the `AgentLaunchPlan` for an agent that doesn't define it.

### Implementation for US3

- [ ] T019 [US3] Update `ClawRuntime.PrepareAgentAsync` to inject tools from `ClawOptions.WorkspacePolicy.MandatoryTools`
- [ ] T020 [US3] Add unit test for mandatory tool injection
- [ ] T021 [US3] Ensure mandatory tools trigger JIT prompts if they require missing capabilities (integrated with US4 logic)

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Cleanup and final validation.

- [ ] T022 [P] Update `README.md` or `DOCS` with information on how to configure `WorkspacePolicy`
- [ ] T023 [P] Add detailed logging for permission denials in `PermissionResolver`
- [ ] T024 Perform final run of all scenarios in `quickstart.md`
- [ ] T025 [P] Create a performance benchmark test to verify SC-002 (<5ms permission resolution overhead)
- [ ] T026 Code cleanup and ensuring all `TODO`s in the new files are addressed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 & 2**: Prerequisites for everything.
- **Phase 3**: The core value - should be implemented first.
- **Phase 4 & 5**: Built upon the core logic from Phase 3.

### Parallel Opportunities

- T001, T002, T003 can be done in parallel.
- T012 (CLI implementation) can be done in parallel with T006 (Library logic) as long as the interface is defined.
- Phase 4 and Phase 5 can be worked on in parallel once Phase 3 is stable.

---

## Implementation Strategy

### MVP First (User Story 1 & 4)

We prioritize the security enforcement and the JIT mechanism. This provides the most immediate "Apple-style" experience and fulfills the core requirement.

### Incremental Delivery

1. **Foundational**: Get the intersection logic working and tested.
2. **Interactive**: Connect the CLI prompt to the runtime.
3. **Extensions**: Add dynamic discovery and mandatory tools once the base system is proven.
