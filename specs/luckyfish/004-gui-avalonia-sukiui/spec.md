# Feature Specification: Add Avalonia+SukiUI GUI version

**Feature Branch**: `luckyfish/004-gui-avalonia-sukiui`  
**Created**: 2026-04-02  
**Status**: Draft  
**Input**: User description: "我现在想使用Avalonia+SukiUI 进行 GUI 版本的开发,项目和基本依赖已经安装在 @ClawSharp.Desktop/ 上了"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Desktop Chat Experience (Priority: P1)

As a user, I want a modern desktop interface to interact with ClawSharp agents so that I can have a more productive and visually appealing chat experience than the CLI.

**Why this priority**: This is the core value proposition of a GUI version. Without a functional chat interface, the GUI serves no purpose.

**Independent Test**: Can be fully tested by launching the application, typing a message, and receiving a response from the default agent.

**Acceptance Scenarios**:

1. **Given** the application is launched, **When** the user types a message in the input box and presses Enter, **Then** the message should appear in the chat history.
2. **Given** a message has been sent, **When** the AI kernel generates a response, **Then** the response should be rendered in the chat window with proper formatting.

---

### User Story 2 - Modern UI with SukiUI (Priority: P2)

As a user, I want the application to use SukiUI's modern design language so that I have a polished, "pro" feel with easy theme switching.

**Why this priority**: SukiUI is a key requirement for the "modern" feel and branding of the desktop app.

**Independent Test**: Can be tested by toggling themes (light/dark) and observing SukiUI components (buttons, sidebars, cards) in the UI.

**Acceptance Scenarios**:

1. **Given** the app is running, **When** the user selects a different theme (e.g., Dark mode), **Then** all SukiUI components should update their appearance instantly.
2. **Given** various UI elements are present, **When** interacting with them, **Then** they should show SukiUI-specific animations and hover effects.

---

### User Story 3 - Agent Selection and Management (Priority: P3)

As a user, I want to see and select from available agents and skills within the GUI so that I can easily switch context without editing files.

**Why this priority**: Leverages the existing `ClawSharp.Lib` agent registry in a visual way.

**Independent Test**: Can be tested by opening an "Agents" panel and selecting a different agent to start a new session.

**Acceptance Scenarios**:

1. **Given** multiple agents are defined in the workspace, **When** the user opens the agent list, **Then** all available agents should be displayed with their names and descriptions.
2. **Given** an agent is selected, **When** a new chat is started, **Then** the session should use that agent's specific instructions and tools.

---

### Edge Cases

- **Large Chat History**: How does the UI handle thousands of messages? (Should implement virtualization or pagination).
- **Network/API Failures**: How does the GUI report errors from `ClawSharp.Lib`? (Should use SukiUI toast notifications or error states).
- **Window Resizing**: Does the layout break on small or very large screens?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a main window using Avalonia and SukiUI.
- **FR-002**: System MUST integrate with `IClawKernel` to process chat messages.
- **FR-003**: System MUST display messages in a scrollable list with distinct styles for User and AI.
- **FR-004**: System MUST allow users to switch between SukiUI themes.
- **FR-005**: System MUST display a list of available agents from the workspace.

### Key Entities *(include if feature involves data)*

- **ChatSession**: Represents a sequence of messages between the user and an agent.
- **AgentCard**: A visual representation of an `AgentDefinition` in the UI.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Application main window appears within 3 seconds of execution.
- **SC-002**: Message rendering (from input to UI display) happens in under 100ms (excluding AI generation time).
- **SC-003**: Theme switching completes in under 200ms without UI flickering.
- **SC-004**: Users can successfully identify and select an agent in under 3 clicks.

## Assumptions

- **Assumptions**: `ClawSharp.Lib` is fully compatible with the Avalonia UI thread (async/await usage).
- **Assumptions**: SukiUI version is compatible with the target .NET 10 runtime.
- **Assumptions**: Basic project structure in `ClawSharp.Desktop/` already contains necessary boilerplate (App.axaml, etc.).
