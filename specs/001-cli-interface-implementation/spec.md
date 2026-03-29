# Feature Specification: CLI Interface for ClawSharp.Lib

**Feature Branch**: `001-cli-interface-implementation`  
**Created**: 2026-03-29  
**Status**: Draft  
**Input**: User description: "我现在想加入 CLI 功能,围绕着 Lib 来实现"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Basic Interaction Loop (Priority: P1)

As a developer, I want to start a session and talk to an agent via the command line, so that I can quickly interact with the AI without a GUI.

**Why this priority**: This is the core MVP functionality of the CLI.

**Independent Test**: Running the CLI with a "talk" command and an agent ID should allow the user to send at least one message and receive a response.

**Acceptance Scenarios**:

1. **Given** a valid agent ID, **When** the user starts a session and sends "Hello", **Then** the CLI displays the AI response.
2. **Given** a missing or invalid agent ID, **When** the user attempts to start a session, **Then** the CLI shows a clear error message.

---

### User Story 2 - ThreadSpace Initialization (Priority: P2)

As a user, I want to initialize a directory as a ClawSharp ThreadSpace via the CLI, so that all subsequent interactions are bound to that project context.

**Why this priority**: Essential for the "local-first" and workspace-bound core concept of ClawSharp.

**Independent Test**: Running `init` in an empty folder should create the necessary ClawSharp metadata/directories.

**Acceptance Scenarios**:

1. **Given** a target directory, **When** the user runs `claw init`, **Then** the directory is marked as a ThreadSpace and persistent storage is initialized.

---

### User Story 3 - Session History Management (Priority: P3)

As a user, I want to list past sessions and view history for a specific session via the CLI, so that I can resume or review my work.

**Why this priority**: Important for session persistence and long-term usability.

**Independent Test**: Running a "history" command should display a list of previous sessions or messages from a specific session.

**Acceptance Scenarios**:

1. **Given** existing sessions, **When** the user runs `claw list`, **Then** a list of session IDs and summaries is displayed.
2. **Given** a specific session ID, **When** the user runs `claw history <id>`, **Then** all messages in that session are shown.

### Edge Cases

- Handling API errors (timeout, invalid keys) and presenting them cleanly to the CLI user.
- Attempting to use CLI in a non-ThreadSpace directory (unless it auto-inits).
- Interrupting a streaming response (Ctrl+C).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: CLI MUST provide an `init` command to initialize the current or specified directory as a ThreadSpace.
- **FR-002**: CLI MUST allow selecting an Agent by ID from the registry defined in `workspace/agents`.
- **FR-003**: CLI MUST support basic "chat" mode: taking user input and printing agent output.
- **FR-004**: CLI MUST persist all messages to the SQLite session store via `ClawSharp.Lib`.
- **FR-005**: CLI MUST support listing existing sessions within the current ThreadSpace.
- **FR-006**: CLI MUST operate as a persistent Interactive Shell (REPL), allowing users to maintain a continuous conversation context until explicitly exited.
- **FR-007**: CLI MUST support streaming output for AI responses to improve perceived performance.

### Key Entities

- **CLI Command**: Represents a user action (init, chat, list, history).
- **Session Record**: The persistence unit from `ClawSharp.Lib` mapped to the CLI session.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can initialize a new ThreadSpace in under 5 seconds.
- **SC-002**: Time from CLI command execution to first character of AI response (TTFT) is under 2 seconds (excluding network latency).
- **SC-003**: 100% of messages sent via CLI are successfully retrieved when checking history later.

## Assumptions

- **Target Users**: Developers comfortable with terminal environments.
- **Scope Boundaries**: This feature focuses on the `ClawSharp.CLI` project; `ClawSharp.Desktop` is out of scope.
- **Environment**: Users have `.NET 10` runtime installed.
- **Authentication**: Users have already configured their API keys in `.env` or `appsettings.Local.json` as per Lib requirements.
