# Feature Specification: Configuration Bootstrap Wizard

**Feature Branch**: `luckyfish/002-config-bootstrap`  
**Created**: 2026-03-30  
**Status**: Draft  
**Input**: User description: "当没有 appsettings.json 时，进入 ClawSharp 的初始化环节，通过引导生成全新的配置文件"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Initial Setup Wizard (Priority: P1)

As a new user who just installed ClawSharp, I want to be guided through the initial configuration so that I don't have to manually create a JSON file.

**Why this priority**: Essential for the first-run experience and user onboarding.

**Independent Test**: Can be tested by deleting `appsettings.json` and running any `claw` command, then following the prompts to generate a new file.

**Acceptance Scenarios**:

1. **Given** `appsettings.json` does not exist, **When** I run `claw chat`, **Then** the system should display a welcome message and start an interactive configuration wizard.
2. **Given** the wizard is active, **When** I provide basic settings (workspace, provider, api key), **Then** the system should generate a valid `appsettings.json` and proceed with the original command.

---

### User Story 2 - Skip or Default Wizard (Priority: P2)

As an experienced user, I want to quickly bypass the wizard using default values so that I can get started as fast as possible.

**Why this priority**: Improves UX for power users.

**Independent Test**: Can be tested by pressing Enter at all prompts in the wizard.

**Acceptance Scenarios**:

1. **Given** the wizard is active, **When** I press Enter at all prompts, **Then** the system should generate a configuration with sensible default values.

---

### User Story 3 - Interactive Provider Selection (Priority: P2)

As a user, I want to choose my preferred AI provider from a list during setup so that I don't have to remember the exact provider type names.

**Why this priority**: Reduces friction and prevents configuration errors.

**Independent Test**: Can be tested by selecting a provider from the list in the wizard.

**Acceptance Scenarios**:

1. **Given** the wizard is at the "Provider" step, **When** I see a selection list of providers (OpenAI, Anthropic, Gemini, etc.), **Then** I should be able to select one using arrow keys.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST detect the absence of `appsettings.json` in the current working directory.
- **FR-002**: System MUST launch an interactive wizard if `appsettings.json` is missing.
- **FR-003**: Wizard MUST prompt for `Runtime:WorkspaceRoot` (default: current directory).
- **FR-004**: Wizard MUST prompt for `Runtime:DataPath` (default: `.clawsharp`).
- **FR-005**: Wizard MUST provide a selection list for `Providers:DefaultProvider`.
- **FR-006**: Wizard MUST allow entering an API key for the selected provider (using masked input).
- **FR-007**: System MUST write the gathered configuration to `appsettings.json` in a pretty-printed format.
- **FR-008**: System MUST automatically proceed with the execution of the original command after successful configuration generation.
- **FR-009**: If `appsettings.json` is missing but `appsettings.Local.json` exists, system MUST alert the user and ask if they want to proceed with the wizard or use the existing local configuration.

### Key Entities

- **BootstrapWizard**: The CLI component responsible for user interaction.
- **ConfigurationTemplate**: A template for the default configuration structure.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new user can generate a functional configuration file in under 60 seconds.
- **SC-002**: Generated `appsettings.json` is a valid JSON that can be loaded by the existing `ClawConfigurationLoader`.
- **SC-003**: No "file not found" errors are shown to the user when starting from scratch.

## Assumptions

- The CLI has write permissions in the current working directory.
- `Spectre.Console` is available for building the wizard UI.
- Users have at least one AI provider API key or intend to use a stub provider for testing.
