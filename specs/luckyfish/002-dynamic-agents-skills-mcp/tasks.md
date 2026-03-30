# Tasks: Dynamic Agents, Skills, and MCP Support

**Feature**: Dynamic Agents, Skills, and MCP Support
**Branch**: `luckyfish/002-dynamic-agents-skills-mcp`
**Status**: Completed

## Implementation Strategy

We will follow an incremental delivery approach:
1.  **Phase 1 & 2**: Establish the foundational models and the MCP protocol layer.
2.  **Phase 3 (MVP)**: Implement dynamic Agent loading. This provides immediate visible value.
3.  **Phase 4**: Implement dynamic Skill loading with automated namespacing.
4.  **Phase 5**: Integrate MCP servers and tools.
5.  **Phase 6**: Add polish (file system watching) and finalize integration.

## Phase 1: Setup

- [x] T001 Define `DynamicSourceType` enum in `ClawSharp.Lib/Core/DynamicSourceType.cs`
- [x] T002 Add `Source`, `OriginalId`, and `SourcePath` properties to `AgentDefinition` in `ClawSharp.Lib/Agents/AgentDefinition.cs`
- [x] T003 Add `Source`, `OriginalId`, and `SourcePath` properties to `SkillDefinition` in `ClawSharp.Lib/Skills/SkillDefinition.cs`
- [x] T004 Define `McpServerConfig` and related configuration models in `ClawSharp.Lib/Configuration/McpConfiguration.cs`

## Phase 2: Foundational (MCP Protocol)

- [x] T005 Implement JSON-RPC message models (Request, Response, Error) in `ClawSharp.Lib/Mcp/McpProtocol.cs`
- [x] T006 [P] Implement `McpStdioTransport` using `System.Diagnostics.Process` in `ClawSharp.Lib/Mcp/McpStdioTransport.cs`
- [x] T007 Implement `McpClient` to handle initialization handshake and message mapping in `ClawSharp.Lib/Mcp/McpClient.cs`
- [x] T008 Create unit tests for MCP protocol and transport in `ClawSharp.Lib.Tests/McpTests.cs`

## Phase 3: [US1] Local Agent Discovery

**Goal**: Automatically load agent definitions from `~/.agent/`.
**Independent Test**: Place a valid `.md` file in `~/.agent/` and verify it appears in `GetAll()` of `AgentRegistry`.

- [x] T009 [P] [US1] Update `FileSystemAgentDefinitionStore` to resolve `~/.agent` path using `Environment.SpecialFolder.UserProfile`
- [x] T010 [US1] Modify `FileSystemAgentDefinitionStore.LoadAllAsync` to scan both workspace and user home directory for `.md` files in `ClawSharp.Lib/Agents/FileSystemAgentDefinitionStore.cs`
- [x] T011 [US1] Update `AgentRegistry.ReloadAsync` to correctly handle `Source` and `OriginalId` in `ClawSharp.Lib/Agents/AgentRegistry.cs`
- [x] T012 [US1] Add integration test for dynamic agent loading in `ClawSharp.Lib.Tests/RegistryTests.cs`

## Phase 4: [US2] Local Skill Extension

**Goal**: Load skills from `~/.skills/` with automated namespacing to avoid conflicts.
**Independent Test**: Place a skill with a conflicting ID in `~/.skills/` and verify it is registered with a `user.` prefix.

- [x] T013 [P] [US2] Update `FileSystemSkillDefinitionStore` to resolve `~/.skills` path and scan for `.md` files in `ClawSharp.Lib/Skills/FileSystemSkillDefinitionStore.cs`
- [x] T014 [US2] Implement namespacing logic in `SkillRegistry.ReloadAsync` (prefix `user.` for `User` source skills) in `ClawSharp.Lib/Skills/SkillRegistry.cs`
- [x] T015 [US2] Add integration test for dynamic skill loading and namespacing in `ClawSharp.Lib.Tests/RegistryTests.cs`

## Phase 5: [US3] MCP Tool Integration

**Goal**: Connect to external MCP servers and expose their tools to the AI.
**Independent Test**: Configure a mock MCP server in `mcp.json` and verify tools are listed and callable.

- [x] T016 [US3] Implement `McpToolProxy` to wrap MCP tools as standard `ITool` implementations in `ClawSharp.Lib/Tools/McpToolProxy.cs`
- [x] T017 [US3] Create `McpService` to manage server lifecycles based on `~/.clawsharp/mcp.json` in `ClawSharp.Lib/Mcp/McpService.cs`
- [x] T018 [US3] Integrate `McpService` into `ClawKernel` initialization in `ClawSharp.Lib/Runtime/ClawKernel.cs`
- [x] T019 [US3] Add end-to-end integration test for MCP tool execution in `ClawSharp.Lib.Tests/McpIntegrationTests.cs`

## Phase 6: Polish & Cross-cutting Concerns

- [x] T020 [P] Implement `DefinitionWatcher` using `FileSystemWatcher` for `~/.agent` and `~/.skills` in `ClawSharp.Lib/Runtime/DefinitionWatcher.cs`
- [x] T021 Connect `DefinitionWatcher` events to `Registry.ReloadAsync` with debouncing in `ClawSharp.Lib/Runtime/ClawKernel.cs`
- [x] T022 Update `ClawSharp.CLI` to display the source (Built-in/User) of agents and skills in `ClawSharp.CLI/Commands/ListCommand.cs`

## Dependencies

- **US1 & US2** depend on **Phase 1** (Models).
- **US3** depends on **Phase 2** (MCP Protocol) and **Phase 1** (Config).
- **Phase 6** depends on **US1 & US2** being completed.

## Parallel Execution Examples

- **Agent vs Skill Stores**: T009 [US1] and T013 [US2] can be done in parallel.
- **MCP Transport**: T006 can be implemented while the registries are being updated.
- **Documentation**: Updating Quickstart or README can happen anytime.
