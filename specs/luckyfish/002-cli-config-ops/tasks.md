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

- [x] T001 Create directory for configuration management in `ClawSharp.Lib/Configuration/`
- [x] T002 [P] Initialize test file for configuration in `ClawSharp.Lib.Tests/ConfigurationTests.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 Define `IConfigManager` interface in `ClawSharp.Lib/Configuration/IConfigManager.cs` per refined contract (including async methods)
- [x] T004 Implement `ConfigManager` skeleton in `ClawSharp.Lib/Configuration/ConfigManager.cs` with reflection-based key discovery logic
- [x] T005 Register `IConfigManager` as singleton in `ClawSharp.Lib/Configuration/ServiceCollectionExtensions.cs`
- [x] T006 [P] Create `ConfigCommands` skeleton in `ClawSharp.CLI/Commands/ConfigCommands.cs` using `System.CommandLine`
- [x] T007 Register `config` command in `ClawSharp.CLI/Program.cs` command registry

**Checkpoint**: Foundation ready - `claw config` commands can be registered and the manager service is injectable.

---

## Phase 3: User Story 1 - View and Modify Configuration (Priority: P1) 🎯 MVP

**Goal**: Implement basic viewing, getting, and setting of configuration values with persistence to `appsettings.Local.json`.

**Independent Test**: Run `claw config list` to see settings, `claw config set key value` to change one, and `claw config get key` to verify.

### Implementation for User Story 1

- [x] T008 [US1] Implement `GetAllAsync(reload)` and `Get(key)` in `ClawSharp.Lib/Configuration/ConfigManager.cs`
- [x] T009 [US1] Implement `SetAsync(key, value)` with persistence to `appsettings.Local.json` in `ClawSharp.Lib/Configuration/ConfigManager.cs`
- [x] T010 [US1] Implement `GetSupportedKeysAsync()` via reflection on `ClawOptions` in `ClawSharp.Lib/Configuration/ConfigManager.cs`
- [x] T011 [US1] Implement `config list` command using `Spectre.Console` table in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [x] T012 [US1] Implement `config get <key>` command in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [x] T013 [US1] Implement `config set <key> [value]` command with key validation in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [x] T014 [US1] Add unit tests for `Get`, `SetAsync`, and persistence in `ClawSharp.Lib.Tests/ConfigurationTests.cs`

**Checkpoint**: User Story 1 is functional. Basic config management (CRUD) works.

---

## Phase 4: User Story 3 - Secure Secret Management (Priority: P2)

**Goal**: Protect sensitive configuration values like API keys from exposure via masking and secure input.

**Independent Test**: Verify `claw config list` masks keys and `claw config set key` (without value) prompts for masked input.

### Implementation for User Story 3

- [x] T015 [US3] Implement `IsSecret(key)` logic using patterns (`*.ApiKey`, `*.Token`, etc.) in `ClawSharp.Lib/Configuration/ConfigManager.cs`
- [x] T016 [US3] Update `config list` to use `********` mask for secret values in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [x] T017 [US3] Update `config set` to trigger interactive masked prompt via `AnsiConsole.Prompt` when value is missing for secret keys in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [x] T018 [US3] Add unit tests for secret masking and `IsSecret` pattern matching in `ClawSharp.Lib.Tests/ConfigurationTests.cs`

**Checkpoint**: User Story 3 is functional. Secrets are handled securely in the CLI.

---

## Phase 5: User Story 2 - Reset Configuration (Priority: P2)

**Goal**: Allow users to reset settings to default values with optional interactive confirmation.

**Independent Test**: Run `claw config reset --key some.key` and confirm (or use `--force`), then verify it returns to default.

### Implementation for User Story 2

- [x] T019 [US2] Implement `ResetAsync(all, key, force)` in `ClawSharp.Lib/Configuration/ConfigManager.cs`
- [x] T020 [US2] Implement `config reset` command with `--all`, `--key`, and `--force` options in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [x] T021 [US2] Add interactive confirmation using `AnsiConsole.Confirm` in `config reset` if `--force` is missing in `ClawSharp.CLI/Commands/ConfigCommands.cs`
- [x] T022 [US2] Add unit tests for reset logic and confirmation bypassing in `ClawSharp.Lib.Tests/ConfigurationTests.cs`

**Checkpoint**: User Story 2 is functional. Configuration can be reset safely.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and final verification.

- [x] T023 [P] Update `GEMINI.md` "Active Technologies" section with configuration management details
- [x] T024 [P] Ensure all configuration methods have XML documentation in `ClawSharp.Lib/Configuration/`
- [x] T025 Run all tests in `ClawSharp.Lib.Tests/` and verify SC-001 (performance) and SC-002 (real-time updates) manually via `quickstart.md` scenarios

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on T001. Blocks all User Stories.
- **User Stories (Phase 3+)**: Depend on Phase 2 completion.
  - US1 (Phase 3) is the MVP and should be completed first.
  - US3 (Phase 4) depends on US1 implementation (table and set command).
  - US2 (Phase 5) is independent but recommended after US1.
- **Polish (Final Phase)**: Depends on all desired user stories.

### User Story Dependencies

- **User Story 1 (P1)**: Foundation for other stories.
- **User Story 3 (P2)**: Enhances US1 with security.
- **User Story 2 (P2)**: Independent management feature.

### Parallel Opportunities

- T002 (Test file) can be created in parallel with T001.
- T006 (CLI Skeleton) can be created in parallel with T004 (Lib implementation).
- T023 and T024 (Docs/Polish) can run in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 & 2 (Foundation).
2. Complete Phase 3 (US1: View/Modify).
3. **STOP and VALIDATE**: Verify CRUD via CLI independently.

### Incremental Delivery

1. Foundation -> Config architecture ready.
2. US1 -> Config management functional (MVP).
3. US3 -> Security layer added (masking/secure input).
4. US2 -> Safety layer added (reset/confirmation).
5. Polish -> Final docs and verification.
