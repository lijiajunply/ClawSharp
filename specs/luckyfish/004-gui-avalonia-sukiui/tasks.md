# Tasks: Add Avalonia+SukiUI GUI version

**Input**: Design documents from `/specs/luckyfish/004-gui-avalonia-sukiui/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

**Tests**: Tests are NOT requested for this feature.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

## Path Conventions

- **Core Library**: `ClawSharp.Lib/`
- **Desktop App**: `ClawSharp.Desktop/`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Add NuGet dependencies (SukiUI, ReactiveUI, Markdown.Avalonia, Microsoft.Extensions.DependencyInjection) to `ClawSharp.Desktop/ClawSharp.Desktop.csproj`
- [x] T002 Configure `ClawSharp.Desktop/App.axaml` with SukiUI styles and theme configuration
- [x] T003 [P] Setup folder structure (Models, ViewModels, Views) in `ClawSharp.Desktop/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Implement Dependency Injection container in `ClawSharp.Desktop/Program.cs` or separate configurator
- [x] T005 Create `ViewModelBase.cs` in `ClawSharp.Desktop/ViewModels/`
- [x] T006 [P] Create `MainWindowViewModel.cs` in `ClawSharp.Desktop/ViewModels/` (Initial setup)
- [x] T007 Update `ClawSharp.Desktop/App.axaml.cs` to resolve `MainWindow` from DI container

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Desktop Chat Experience (Priority: P1) 🎯 MVP

**Goal**: Implement a basic functional chat interface powered by `ClawSharp.Lib`.

**Independent Test**: Launch application, send a message, and see a formatted AI response.

### Implementation for User Story 1

- [x] T008 [P] [US1] Create `MessageViewModel.cs` in `ClawSharp.Desktop/ViewModels/`
- [x] T009 [US1] Implement `ChatViewModel.cs` in `ClawSharp.Desktop/ViewModels/` with `Messages` collection and `SendMessageCommand`
- [x] T010 [US1] Create `ChatView.axaml` and `ChatView.axaml.cs` in `ClawSharp.Desktop/Views/`
- [x] T011 [US1] Integrate `Markdown.Avalonia` in `ChatView.axaml` for rich message rendering
- [x] T012 [US1] Integrate `IClawKernel` in `ChatViewModel.cs` to handle real-time message processing and streaming

**Checkpoint**: User Story 1 (MVP) is fully functional and testable independently.

---

## Phase 4: User Story 2 - Modern UI with SukiUI (Priority: P2)

**Goal**: Apply SukiUI design language and implement theme switching.

**Independent Test**: Toggle between light and dark themes; observe SukiUI window decorations and control styles.

### Implementation for User Story 2

- [x] T013 [US2] Update `ClawSharp.Desktop/Views/MainWindow.axaml` to use `SukiWindow` and sidebar layout
- [x] T014 [US2] Implement theme switching logic in `MainWindowViewModel.cs` using `SukiTheme`
- [x] T015 [US2] Add theme toggle UI button/control in `MainWindow.axaml`
- [x] T016 [P] [US2] Apply SukiUI specific controls (Buttons, TextFields, Cards) in `ChatView.axaml`

**Checkpoint**: User Story 2 is complete, providing a polished look and theme management.

---

## Phase 5: User Story 3 - Agent Selection and Management (Priority: P3)

**Goal**: Allow users to browse and select agents defined in the workspace.

**Independent Test**: Browse available agents in a sidebar and switch the current session to a new agent.

### Implementation for User Story 3

- [x] T017 [P] [US3] Create `AgentViewModel.cs` in `ClawSharp.Desktop/ViewModels/`
- [x] T018 [US3] Implement agent loading logic from `IAgentRegistry` in `ChatViewModel.cs`
- [x] T019 [US3] Create `AgentListView.axaml` and `AgentListView.axaml.cs` in `ClawSharp.Desktop/Views/`
- [x] T020 [US3] Implement selection logic to restart sessions with new agents in `ChatViewModel.cs`

**Checkpoint**: All user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [x] T021 [P] Add application icon and assets to `ClawSharp.Desktop/Assets/`
- [x] T022 Implement error handling with SukiUI Toast notifications for kernel errors
- [x] T023 Optimize message list virtualization in `ChatView.axaml` for large histories
- [x] T024 [P] Update `quickstart.md` and `README.md` with final GUI execution details

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Phase 1 completion.
- **User Stories (Phase 3+)**: All depend on Phase 2 completion.
  - US1 (P1) should be completed first as it is the MVP.
  - US2 and US3 can proceed in parallel once US1 is functional.
- **Polish (Final Phase)**: Depends on all user stories being complete.

### Parallel Opportunities

- T003 (Folder setup) can run in parallel with T001/T002.
- T006 (ViewModel setup) can run in parallel with T004/T005.
- T008 (Message model) can run in parallel with other US1 tasks.
- US2 and US3 can be worked on in parallel once the foundation and US1 basic structure exist.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 & 2 (Setup & Foundation).
2. Complete Phase 3 (User Story 1 - Basic Chat).
3. **STOP and VALIDATE**: Ensure the app can send and receive messages correctly.

### Incremental Delivery

1. Foundation ready.
2. Add US1 → Core Chat functionality (MVP).
3. Add US2 → Polished UI and Theme switching.
4. Add US3 → Advanced Agent management.
5. Final Polish.
