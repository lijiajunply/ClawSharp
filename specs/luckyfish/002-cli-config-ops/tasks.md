# Tasks: CLI Configuration Operations

**Input**: Design documents from `/specs/luckyfish/002-cli-config-ops/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: xUnit integration tests included as requested in the implementation plan.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create directory for configuration management in `ClawSharp.Lib/Configuration/`
- [ ] T002 Initialize test file for configuration in `ClawSharp.Lib.Tests/ConfigurationTests.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T003 Define `IConfigManager` interface in `ClawSharp.Lib/Configuration/IConfigManager.cs` per contract
- [ ] T004 Implement `ConfigManager` skeleton in `ClawSharp.Lib/Configuration/ConfigManager.cs`
- [ ] T005 Register `IConfigManager` as singleton in `ClawSharp.Lib/Configuration/ServiceCollectionExtensions.cs`
- [ ] T006 [P] Create `ConfigCommands` skeleton in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [ ] T007 Register `config` command in `ClawSharp.CLI/Program.cs` (or appropriate command registry)

**Checkpoint**: Foundation ready - `claw config` commands can be registered and the service is injectable.

---

## Phase 3: User Story 1 - View and Modify Configuration (Priority: P1) 🎯 MVP

**Goal**: Implement basic viewing, getting, and setting of configuration values with persistence.

**Independent Test**: Run `claw config list` to see settings, `claw config set key value` to change one, and `claw config get key` to verify.

### Implementation for User Story 1

- [ ] T008 [US1] Implement `GetAll()` and `Get(key)` in `ClawSharp.Lib/Configuration/ConfigManager.cs`
- [ ] T009 [US1] Implement `SetAsync(key, value)` with persistence to `appsettings.Local.json` in `ClawSharp.Lib/Configuration/ConfigManager.cs`
- [ ] T010 [US1] Create unit tests for `GetAll`, `Get`, and `SetAsync` in `ClawSharp.Lib.Tests/ConfigurationTests.cs`
- [ ] T011 [US1] Implement `config list` command using `Spectre.Console` table in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [ ] T012 [US1] Implement `config get <key>` command in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [ ] T013 [US1] Implement `config set <key> <value>` command in `ClawSharp.CLI/Commands/ConfigCommands.cs`

**Checkpoint**: User Story 1 is functional. Basic config management works.

---

## Phase 4: User Story 3 - Secure Secret Management (Priority: P2)

**Goal**: Protect sensitive configuration values like API keys from exposure.

**Independent Test**: Verify `claw config list` masks keys and `claw config set key` (without value) prompts for masked input.

### Implementation for User Story 3

- [ ] T014 [US3] Implement `IsSecret(key)` logic in `ClawSharp.Lib/Configuration/ConfigManager.cs` using patterns from `data-model.md`
- [ ] T015 [US3] Update `config list` to use `********` for secret values in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [ ] T016 [US3] Update `config set` to support interactive masked prompt via `AnsiConsole.Prompt` when value is missing in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [ ] T017 [US3] Add test cases for secret masking and pattern matching in `ClawSharp.Lib.Tests/ConfigurationTests.cs`

**Checkpoint**: User Story 3 is functional. Secrets are handled securely in the CLI.

---

## Phase 5: User Story 2 - Reset Configuration (Priority: P2)

**Goal**: Allow users to reset settings to default values.

**Independent Test**: Modify a setting, run `claw config reset --key key`, and verify it returns to default.

### Implementation for User Story 2

- [ ] T018 [US2] Implement `ResetAsync(all, key)` in `ClawSharp.Lib/Configuration/ConfigManager.cs`
- [ ] T019 [US2] Implement `config reset` command with `--all` and `--key` options in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [ ] T020 [US2] Add unit tests for reset logic in `ClawSharp.Lib.Tests/ConfigurationTests.cs`

**Checkpoint**: User Story 2 is functional. Configuration can be reset.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T021 [P] Update `GEMINI.md` "Active Technologies" section with configuration details
- [ ] T022 Code cleanup and ensuring all methods have XML documentation
- [ ] T023 Run all tests in `ClawSharp.Lib.Tests/` to ensure no regressions
- [ ] T024 Verify all scenarios in `quickstart.md` manually

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Depends on T001. Blocks all User Stories.
- **User Stories (Phase 3-5)**: Depend on Phase 2 completion.
  - US1 (Phase 3) is the priority (MVP).
  - US3 (Phase 4) depends on US1 implementation (masking in list/set).
  - US2 (Phase 5) is independent but recommended after US1.
- **Polish (Phase 6)**: Depends on all user stories.

### Parallel Opportunities

- T006 (ConfigCommands skeleton) can run in parallel with T003-T005 (Lib logic).
- T021 (Documentation) can run in parallel with final implementation tasks.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 & 2 (Foundation).
2. Complete Phase 3 (US1: list, get, set).
3. Validate with manual tests and unit tests.

### Incremental Delivery

1. Foundation -> Config architecture ready.
2. US1 -> Config management functional.
3. US3 -> Security layer added.
4. US2 -> Recovery feature added.
