# Feature Specification: CLI Configuration Operations

**Feature Branch**: `luckyfish/002-cli-config-ops`  
**Created**: 2026-03-30  
**Status**: Draft  
**Input**: User description: "我现在想在 CLI 中加入配置相关操作"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View and Modify Configuration (Priority: P1)

As a CLI user, I want to view my current settings and update specific values (like API keys) so that I can configure the AI providers I use.

**Why this priority**: Essential for the CLI to be functional and usable with different AI models.

**Independent Test**: Can be tested by running `config list` to see defaults, then `config set` to change a value, and verifying the change with `config get`.

**Acceptance Scenarios**:

1. **Given** the CLI is installed, **When** I run `config list`, **Then** I should see a table of all configuration keys and their current values (with secrets masked).
2. **Given** I want to set my OpenAI API key, **When** I run `config set provider.openai.key sk-...`, **Then** the system should validate the key format and save it securely.

---

### User Story 2 - Reset Configuration (Priority: P2)

As a CLI user, I want to reset my configuration to default values so that I can easily recover from misconfiguration.

**Why this priority**: High value for troubleshooting and "getting back to a known good state".

**Independent Test**: Can be tested by modifying several values and then running a reset command to see them return to defaults.

**Acceptance Scenarios**:

1. **Given** I have modified several configuration settings, **When** I run `config reset --all`, **Then** the system should prompt for confirmation and then restore all settings to their original factory defaults.

---

### User Story 3 - Secure Secret Management (Priority: P2)

As a security-conscious user, I want my API keys to be handled securely so that they are not leaked in logs or terminal output.

**Why this priority**: Critical for user trust and security.

**Independent Test**: Can be tested by running `config list` and checking that keys are displayed as `********`.

**Acceptance Scenarios**:

1. **Given** an API key is stored, **When** I list configurations, **Then** the value must be masked.
2. **Given** a configuration key is marked as "secret", **When** I set it via CLI, **Then** it should not be echoed back in plain text if possible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a `config list` command to display all available configuration keys and values.
- **FR-002**: System MUST provide a `config get <key>` command to retrieve a specific value.
- **FR-003**: System MUST provide a `config set <key> <value>` command to update a setting.
- **FR-004**: System MUST mask sensitive values (defined in a "secrets" list) in the `list` output.
- **FR-005**: System MUST validate that the configuration key exists before allowing a `set` or `get` operation.
- **FR-006**: System MUST persist configuration changes to a local file (e.g., `appsettings.json` or `.env` in the user's home directory).
- FR-007: System MUST support global configuration scope (one shared configuration across all projects).
- FR-008: System MUST handle hybrid input mode for secrets (supports command-line arguments and interactive masked prompts if values are missing).

### Key Entities

- **ConfigurationEntry**: Represents a single setting, including its key, value, description, and whether it is a secret.
- **ConfigStore**: The abstraction responsible for reading and writing configuration to persistent storage.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can update a configuration value (e.g., an API key) in under 15 seconds.
- **SC-002**: Configuration changes take effect immediately in subsequent CLI commands without requiring a restart.
- **SC-003**: 100% of defined "secret" keys are masked in the output of the `list` command.

## Assumptions

- Configuration is stored in a standard location consistent with the OS (e.g., `~/.config/clawsharp/` on Linux/macOS).
- The CLI will use `Spectre.Console` for rich rendering of the configuration table.
- Initial configuration values are loaded from the application's default settings.
- Project-specific configuration (if supported) takes precedence over global configuration.
