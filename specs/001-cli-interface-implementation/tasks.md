# Tasks: CLI Interface for ClawSharp.Lib

**Input**: Design documents from `/specs/001-cli-interface-implementation/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/cli-commands.md

**Tests**: Integration tests are included as per the project's "Testing Discipline" principle in the Constitution.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Core Library**: `ClawSharp.Lib/`
- **CLI App**: `ClawSharp.CLI/`
- **Desktop App**: `ClawSharp.Desktop/`
- **Tests**: `ClawSharp.Lib.Tests/`
- Paths shown below assume `ClawSharp.CLI/` - adjust based on plan.md structure

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Initialize `ClawSharp.CLI` project and add project reference to `ClawSharp.Lib` in `ClawSharp.slnx`
- [x] T002 Add NuGet packages `Spectre.Console` and `System.CommandLine` to `ClawSharp.CLI/ClawSharp.CLI.csproj`
- [x] T003 [P] Configure Dependency Injection for `IClawRuntime` and `IClawKernel` in `ClawSharp.CLI/Infrastructure/ServiceConfigurator.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Implement base `RootCommand` and routing logic using `System.CommandLine` in `ClawSharp.CLI/Program.cs`
- [x] T005 [P] Setup basic logging and `Spectre.Console` error formatting in `ClawSharp.CLI/Infrastructure/CliErrorHandler.cs`

**Checkpoint**: CLI skeleton ready - command implementation can now begin

---

## Phase 3: User Story 1 - Basic Interaction Loop (Priority: P1) 🎯 MVP

**Goal**: Start a session and talk to an agent via a persistent REPL environment.

**Independent Test**: Running `dotnet run -- chat planner` starts a REPL, allows typing a message, and shows a streamed response.

### Implementation for User Story 1

- [x] T006 [P] [US1] Create `ChatCommand` class with `agent-id` argument in `ClawSharp.CLI/Commands/ChatCommand.cs`
- [x] T007 [US1] Implement REPL loop using `AnsiConsole.Ask` in `ClawSharp.CLI/Commands/ChatCommand.cs`
- [x] T008 [US1] Integrate `IClawRuntime.RunTurnAsync` with `AnsiConsole.Live` for character-by-character streaming in `ClawSharp.CLI/Commands/ChatCommand.cs`
- [x] T009 [US1] Add integration test for the chat interaction loop in `ClawSharp.Lib.Tests/CliIntegrationTests.cs`

**Checkpoint**: Basic chat functionality is fully operational and testable.

---

## Phase 4: User Story 2 - ThreadSpace Initialization (Priority: P2)

**Goal**: Initialize a directory as a ClawSharp ThreadSpace via the CLI.

**Independent Test**: Running `dotnet run -- init` creates a `.clawsharp` folder and initializes the local database in the current directory.

### Implementation for User Story 2

- [x] T010 [P] [US2] Create `InitCommand` with optional `--path` option in `ClawSharp.CLI/Commands/InitCommand.cs`
- [x] T011 [US2] Implement initialization logic calling `IClawKernel.InitializeAsync` in `ClawSharp.CLI/Commands/InitCommand.cs`
- [x] T012 [US2] Add integration test verifying directory initialization in `ClawSharp.Lib.Tests/CliIntegrationTests.cs`

**Checkpoint**: ThreadSpace management is now supported via CLI.

---

## Phase 5: User Story 3 - Session History Management (Priority: P3)

**Goal**: List past sessions and view history for a specific session.

**Independent Test**: Running `dotnet run -- list` shows a table of sessions; `dotnet run -- history <id>` prints past messages.

### Implementation for User Story 3

- [x] T013 [P] [US3] Create `ListCommand` using `Spectre.Console.Table` to display session records in `ClawSharp.CLI/Commands/ListCommand.cs`
- [x] T014 [P] [US3] Create `HistoryCommand` to fetch and print session history in `ClawSharp.CLI/Commands/HistoryCommand.cs`
- [x] T015 [US3] Implement rich text formatting for history blocks (User vs Agent colors) in `ClawSharp.CLI/Commands/HistoryCommand.cs`
- [x] T016 [US3] Add integration tests for list and history commands in `ClawSharp.Lib.Tests/CliIntegrationTests.cs`

**Checkpoint**: Session history management is fully functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and usability

- [x] T017 [P] Implement `AgentsCommand` and `SkillsCommand` to list registered definitions in `ClawSharp.CLI/Commands/RegistryCommands.cs`
- [x] T018 Define a consistent CLI theme (colors, icons) in `ClawSharp.CLI/Infrastructure/ThemeConfig.cs`
- [x] T019 Update `specs/001-cli-interface-implementation/quickstart.md` with final command syntax and usage examples

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Depends on Phase 1 - BLOCKS all command implementations.
- **User Stories (Phase 3-5)**: All depend on Phase 2. US1 is P1 (MVP).
- **Polish (Phase 6)**: Depends on completion of core stories.

### Parallel Opportunities

- T003 (DI Config) can run in parallel with project setup.
- T005 (Error Handling) can run in parallel with routing logic.
- Command creation (T006, T010, T013, T014, T017) can be done in parallel once the foundation (Phase 2) is ready.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 & 2.
2. Complete Phase 3 (User Story 1 - Chat).
3. **VALIDATE**: Run `dotnet run -- chat planner` to confirm core value.

### Incremental Delivery

1. Foundation ready.
2. Add `chat` (MVP).
3. Add `init` (Project context).
4. Add `list`/`history` (Management).
5. Add Polish (Registry listing, styling).
