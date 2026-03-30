# Tasks: Configuration Bootstrap Wizard

**Input**: Design documents from `/specs/luckyfish/002-config-bootstrap/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Unit tests for the bootstrapper logic are included.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 [P] Create `ProviderTemplate.cs` in `ClawSharp.Lib/Configuration/` per data model
- [x] T002 [P] Create `BootstrapConfig.cs` in `ClawSharp.Lib/Configuration/` per data model

---

## Phase 2: Foundational (Library Logic)

**Purpose**: Core logic for generating and saving the configuration file

**⚠️ CRITICAL**: Must be completed before UI implementation

- [x] T003 Define `IConfigBootstrapper` interface in `ClawSharp.Lib/Configuration/IConfigBootstrapper.cs` per contract
- [x] T004 Implement `ConfigBootstrapper` in `ClawSharp.Lib/Configuration/ConfigBootstrapper.cs` (handles JSON generation and file saving)
- [x] T005 [P] Add unit tests for `ConfigBootstrapper` in `ClawSharp.Lib.Tests/ConfigurationTests.cs`

**Checkpoint**: Foundation ready - the system can programmatically generate `appsettings.json`.

---

## Phase 3: User Story 1 - Initial Setup Wizard (Priority: P1) 🎯 MVP

**Goal**: Implement the basic interactive setup flow when `appsettings.json` is missing.

**Independent Test**: Delete `appsettings.json`, run `claw chat`, and verify the wizard starts.

### Implementation for User Story 1

- [x] T006 [US1] Create `BootstrapWizard.cs` skeleton in `ClawSharp.CLI/Infrastructure/` using `Spectre.Console`
- [x] T007 [US1] Implement interactive prompts for WorkspaceRoot and DataPath in `BootstrapWizard.cs`
- [x] T008 [US1] Implement interactive prompts for DefaultProvider and ApiKey (masked) in `BootstrapWizard.cs`
- [x] T009 [US1] Integrate `BootstrapWizard.RunAsync()` in `ClawSharp.CLI/Program.cs` before `BuildHost`
- [x] T010 [US1] Update `Program.cs` to ensure the original command continues after bootstrap (FR-008)

**Checkpoint**: User Story 1 is functional. New users are guided through setup.

---

## Phase 4: User Story 3 - Interactive Provider Selection (Priority: P2)

**Goal**: Use a selection list for choosing the AI provider.

**Independent Test**: Verify the provider prompt shows a list (OpenAI, Anthropic, Gemini) that can be navigated with arrow keys.

### Implementation for User Story 3

- [x] T011 [US3] Update `BootstrapWizard.cs` to use `SelectionPrompt` for provider selection based on templates from `IConfigBootstrapper`

**Checkpoint**: User Story 3 is functional. Provider selection is more user-friendly.

---

## Phase 5: User Story 2 - Skip or Default Wizard (Priority: P2)

**Goal**: Handle default values and existing Local configuration.

**Independent Test**: Verify that pressing Enter accepts defaults and that existing `appsettings.Local.json` triggers a warning.

### Implementation for User Story 2

- [x] T012 [US2] Update `BootstrapWizard` prompts to show and support default values (FR-003, FR-004)
- [x] T013 [US2] Implement the check for `appsettings.Local.json` and the corresponding "Use existing?" prompt in `BootstrapWizard.cs` (FR-009)

**Checkpoint**: User Story 2 is functional. Power users can skip prompts, and Local config is handled gracefully.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup and documentation

- [x] T014 [P] Ensure all new members in `ClawSharp.Lib` have XML documentation
- [x] T015 Verify all scenarios in `quickstart.md` manually by running the CLI from a clean state

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Phase 1.
- **User Stories (Phase 3+)**: Depend on Phase 2.
  - US1 (Phase 3) is the MVP.
  - US3 (Phase 4) enhances US1.
  - US2 (Phase 5) adds edge case handling to US1.
- **Polish (Phase 6)**: Depends on all user stories.

### Parallel Opportunities

- T001 and T002 can run in parallel.
- T005 (Tests) can run in parallel with UI tasks once the interface is defined.
- T014 (Docs) can run in parallel with final testing.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 & 2 (Foundation).
2. Complete Phase 3 (US1: Core Wizard).
3. **STOP and VALIDATE**: Confirm a functional `appsettings.json` is generated and the app proceeds.

### Incremental Delivery

1. Foundation -> Config generation logic ready.
2. US1 -> Basic interactive wizard working.
3. US3 -> Selection list added.
4. US2 -> Defaults and Local config handling added.
5. Polish -> Final docs and verification.
