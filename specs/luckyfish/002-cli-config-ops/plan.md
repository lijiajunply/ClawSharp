# Implementation Plan: CLI Configuration Operations

**Branch**: `luckyfish/002-cli-config-ops` | **Date**: 2026-03-30 | **Spec**: [/specs/luckyfish/002-cli-config-ops/spec.md]
**Input**: Feature specification from `/specs/luckyfish/002-cli-config-ops/spec.md`

## Summary

This feature adds a robust configuration management system to the ClawSharp CLI. The core logic resides in `ClawSharp.Lib` as a new `IConfigManager` service, adhering to the "Library-First" principle. Users can view, update, get, and reset configuration settings, with built-in security for sensitive values like API keys through masking and interactive prompts.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: `ClawSharp.Lib`, `Spectre.Console`, `Microsoft.Extensions.Configuration`, `System.CommandLine`.  
**Storage**: `appsettings.Local.json` for persistent settings.  
**Testing**: xUnit integration tests in `ClawSharp.Lib.Tests`.  
**Target Platform**: Cross-platform (Windows, macOS, Linux).
**Project Type**: CLI application and Library.  
**Performance Goals**: Instant response for configuration operations.  
**Constraints**: Secure handling of secrets, global configuration scope.
**Scale/Scope**: Manage key-value pairs for AI providers and user preferences.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Library-First**: ✅ `IConfigManager` implemented in `ClawSharp.Lib`.
- **Async-First**: ✅ Persistence and reload operations use `Task` / `ValueTask`.
- **Modern .NET**: ✅ Uses .NET 10 and nullable reference types.
- **Reliable Persistence**: ✅ Persists to `appsettings.Local.json`.

## Project Structure

### Documentation (this feature)

```text
specs/luckyfish/002-cli-config-ops/
├── plan.md              # This file
├── research.md          # Research findings and decisions
├── data-model.md        # Entities and data structures
├── quickstart.md        # User guide for the feature
├── contracts/
│   └── config.md        # Interface and CLI command contracts
└── checklists/
    └── requirements.md  # Quality checklist
```

### Source Code (repository root)

```text
ClawSharp.Lib/
├── Configuration/
│   ├── IConfigManager.cs (New - Refined with async methods)
│   ├── ConfigManager.cs (New - Uses reflection for key discovery)
│   └── [Existing files]

ClawSharp.CLI/
├── Commands/
│   └── ConfigCommands.cs (New - Uses Spectre.Console for tables and masked prompts)
├── Infrastructure/
│   └── [Existing files]
└── Program.cs (Updated to register ConfigCommands)

ClawSharp.Lib.Tests/
└── ConfigurationTests.cs (New - Integration tests verifying persistence and masking)
```

**Structure Decision**: Logic is split between the kernel (`ClawSharp.Lib`) for engine-level config management and the UI (`ClawSharp.CLI`) for interaction.

## Phase 0: Outline & Research

Findings consolidated in `research.md`. Key decisions:
- Persistent storage in `appsettings.Local.json`.
- `IConfigManager` in `ClawSharp.Lib`.
- `Spectre.Console` for CLI UI and masking.
- Reflection-based key discovery for `ClawOptions` to enable robust validation and auto-completion.

## Phase 1: Design & Contracts

Design artifacts generated:
- `data-model.md`
- `contracts/config.md`
- `quickstart.md`

### Agent Context Update
Updated `gemini` agent context via `.specify/scripts/bash/update-agent-context.sh`.

## Phase 2: Implementation & Verification

(To be executed by `speckit.tasks` and subsequent implementation commands)

- Implementation of `IConfigManager` in `ClawSharp.Lib`.
- Unit tests for configuration loading/saving.
- Implementation of `config` command in `ClawSharp.CLI`.
- Integration test with `ClawSharp.CLI` to verify **SC-001** (performance) and **SC-002** (real-time updates).
- Verify **SC-003** (masking) through automated test cases in `ConfigurationTests.cs`.
