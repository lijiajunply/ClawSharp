# Tasks: ThreadSpace 重新设计与 CLI 体验优化

**Input**: Design documents from `specs/luckyfish/002-redesign-threadspace-cli/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 [P] Update `ThreadSpaceRecord` in `ClawSharp.Lib/Runtime/ThreadSpaceContracts.cs` (BoundFolderPath nullable, IsInit→IsGlobal)
- [ ] T002 [P] Update `CreateThreadSpaceRequest` in `ClawSharp.Lib/Runtime/ThreadSpaceContracts.cs` (BoundFolderPath nullable)
- [ ] T003 [P] Update `ThreadSpaceEntity` in `ClawSharp.Lib/Runtime/Persistence/Entities/ThreadSpaceEntity.cs` (BoundFolderPath nullable, IsInit→IsGlobal)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 Update `ThreadSpaceEntityConfiguration` in `ClawSharp.Lib/Runtime/Persistence/Configurations/ThreadSpaceEntityConfiguration.cs` (Mapping nullable and unique index filter)
- [ ] T005 Update `RuntimeEntityMapper` in `ClawSharp.Lib/Runtime/Persistence/Mapping/RuntimeEntityMapper.cs` (Mapping IsGlobal)
- [ ] T006 Create EF Core Migration `20260330010000_AddGlobalThreadSpace.cs` in `ClawSharp.Lib/Runtime/Persistence/Migrations/`
- [ ] T007 Update `ClawDbContextModelSnapshot.cs` in `ClawSharp.Lib/Runtime/Persistence/Migrations/` to reflect schema changes
- [ ] T008 Update `ThreadSpaceManager` in `ClawSharp.Lib/Runtime/SqliteStores.cs` (EnsureDefault, Create, GetInit using IsGlobal)
- [ ] T009 [P] Add `GetGlobalAsync` to `IThreadSpaceRepository` and implement in `EfThreadSpaceRepository.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - 直接启动即可对话 (Priority: P1) 🎯 MVP

**Goal**: 消除了"先初始化工作区才能对话"的摩擦，与 Gemini CLI / Claude Code 的零配置启动体验对齐。

**Independent Test**: 在全新目录运行 `claw`，应显示欢迎头部 [global] 提示符，直接输入消息应收到 Agent 回复。

### Tests for User Story 1 (OPTIONAL)

- [ ] T010 [P] [US1] Create integration test for Global ThreadSpace auto-creation in `ClawSharp.Lib.Tests/ThreadSpaceManagerTests.cs`

### Implementation for User Story 1

- [ ] T011 [US1] Update `Program.cs` to set `rootCommand` handler to `ChatCommand.RunAsync`
- [ ] T012 [US1] Basic REPL rewrite in `ClawSharp.CLI/Commands/ChatCommand.cs` to initialize Global TS on start
- [ ] T013 [US1] Implement welcome header, `[global] > ` prompt (blue/white), and truncation logic for prompts in `ClawSharp.CLI/Commands/ChatCommand.cs`
- [ ] T014 [US1] Implement message loop with stream rendering in `ClawSharp.CLI/Commands/ChatCommand.cs`

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - 全局 ThreadSpace 的会话延续 (Priority: P1)

**Goal**: 用户再次启动 CLI 时，可以选择继续上一次的全局对话。

**Independent Test**: 启动后输入 `/resume`，应加载上一次会话的历史摘要并继续对话。

### Implementation for User Story 2

- [ ] T015 [US2] Implement `/resume` logic in `ClawSharp.CLI/Commands/ChatCommand.cs` (ListByThreadSpace and load last session)
- [ ] T016 [US2] Add `/resume` tip to welcome header when a previous session exists in `ClawSharp.CLI/Commands/ChatCommand.cs`
- [ ] T017 [US2] Implement `/new` command to start a fresh session in `ClawSharp.CLI/Commands/ChatCommand.cs`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - 切换到目录绑定的 ThreadSpace (Priority: P2)

**Goal**: 支持针对特定项目工作，通过 `/cd` 切换到绑定了目录的 ThreadSpace。

**Independent Test**: 输入 `/cd /path/to/project`，提示符应更新为项目名，工具调用以该目录为根。

### Implementation for User Story 3

- [ ] T018 [US3] Implement `/cd <path>` logic in `ClawSharp.CLI/Commands/ChatCommand.cs` (Create/Reuse Directory TS)
- [ ] T019 [US3] Implement `/home` (and `/cd` no args) logic in `ClawSharp.CLI/Commands/ChatCommand.cs` (Switch to Global TS)
- [ ] T020 [US3] Update prompt dynamically to reflect current ThreadSpace name in `ClawSharp.CLI/Commands/ChatCommand.cs`

**Checkpoint**: All user stories should now be independently functional

---

## Phase 6: User Story 4 - CLI 内联帮助与命令发现 (Priority: P3)

**Goal**: 提升可发现性，通过 `/help` 查看所有可用的斜杠命令。

**Independent Test**: 输入 `/help` 应显示格式化的命令列表；输入未知命令应有友好提示。

### Implementation for User Story 4

- [ ] T021 [US4] Implement `/help` command with structured table in `ClawSharp.CLI/Commands/ChatCommand.cs`
- [ ] T022 [US4] Implement unknown slash command handling in `ClawSharp.CLI/Commands/ChatCommand.cs`
- [ ] T023 [US4] Implement auxiliary commands: `/clear`, `/quit`, `/exit` in `ClawSharp.CLI/Commands/ChatCommand.cs`

**Checkpoint**: UX features are complete

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T024 [P] Update `README_en.md` and `README.md` to reflect new CLI behavior
- [ ] T025 Run `quickstart.md` validation scenarios for final sign-off
- [ ] T026 Code cleanup and refactor shared REPL logic in `ChatCommand.cs`
- [ ] T027 [SC-002] Performance Verification: Measure CLI startup time (Target: < 3s)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Core model updates - can start immediately
- **Foundational (Phase 2)**: Database migrations and manager updates - BLOCKS all user stories
- **User Stories (Phase 3+)**: Depend on Foundational phase completion
  - US1 and US2 are P1 and should be completed first
  - US3 and US4 add extra features but US1/US2 are the MVP

### User Story Dependencies

- **User Story 1 (P1)**: Foundational logic for the new REPL - No dependencies on other stories
- **User Story 2 (P1)**: Depends on US1's REPL structure
- **User Story 3 (P2)**: Depends on US1's REPL structure
- **User Story 4 (P3)**: Depends on US1's REPL structure

### Parallel Opportunities

- Phase 1 tasks (T001, T002, T003) can run in parallel
- T009 (Repository) can run in parallel with other Foundational tasks
- Once T012 is ready, different slash commands (US2, US3, US4) can be implemented in parallel if needed

---

## Parallel Example: User Story 1 & 2 Setup

```bash
# Update models (Phase 1)
Task: "Update ThreadSpaceRecord in ClawSharp.Lib/Runtime/ThreadSpaceContracts.cs"
Task: "Update ThreadSpaceEntity in ClawSharp.Lib/Runtime/Persistence/Entities/ThreadSpaceEntity.cs"

# Once Foundational is done, implement basic commands
Task: "Implement /new command in ClawSharp.CLI/Commands/ChatCommand.cs"
Task: "Implement /help command in ClawSharp.CLI/Commands/ChatCommand.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 & 2)

1. Complete Phase 1 & 2 (Blocking models and DB migrations)
2. Complete Phase 3 (Basic Global REPL)
3. Complete Phase 4 (Resume/New Session)
4. **STOP and VALIDATE**: Verify zero-config start and session resume

### Incremental Delivery

1. Foundation + US1 + US2 → Core experience (MVP)
2. Add US3 → Project-specific workflow
3. Add US4 → UX Polishing and Help system

---

## Notes

- [P] tasks = different files or decoupled logic
- [Story] labels map to spec.md priorities
- Migration T006 is critical as it transforms `is_init` to `is_global` and allows null folder paths
- Ensure `rootCommand.SetHandler` correctly passes through to `ChatCommand` logic
