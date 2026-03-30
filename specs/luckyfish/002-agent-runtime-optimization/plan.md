# Implementation Plan: Agent Runtime Strategy Optimization

**Branch**: `luckyfish/002-agent-runtime-optimization` | **Date**: 2026-03-30 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/luckyfish/002-agent-runtime-optimization/spec.md`

## Summary

This feature optimizes the Agent execution strategy by enforcing a secure, least-privilege model through the intersection of Agent definitions and Workspace policies. It introduces a centralized `PermissionResolver`, supports "Mandatory Tools" at the workspace level, and implements an Apple-style "Just-In-Time" (JIT) permission prompt for capability elevation.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: `Microsoft.Extensions.DependencyInjection`, `Spectre.Console`, `Microsoft.EntityFrameworkCore.Sqlite`, `YamlDotNet`, `McpProtocol`  
**Storage**: SQLite (Operational data), DuckDB (Analytics)  
**Testing**: xUnit (Unit and Integration tests in `ClawSharp.Lib.Tests`)  
**Target Platform**: Cross-platform (macOS, Windows, Linux)  
**Project Type**: Library-centric (Core in `ClawSharp.Lib`, UI in `ClawSharp.CLI`)  
**Performance Goals**: <5ms overhead for permission resolution per session start.  
**Constraints**: Secure, Least-Privilege, Offline-capable, Async-first.  
**Scale/Scope**: Core runtime optimization affecting all Agent sessions.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

1. **Library-First & Local-First**: Core permission resolution logic will reside in `ClawSharp.Lib`. UI prompts will be abstracted via interfaces to keep the library decoupled from specific UI implementations. (✅ PASS)
2. **Markdown-Driven Intelligence**: Agent permissions remain defined in `agent.md` frontmatter. (✅ PASS)
3. **Async-First & Modern .NET**: All permission checks and JIT prompts will be `async`. Nullable types will be strictly used. (✅ PASS)
4. **Provider & Tool Extensibility**: Supports dynamic discovery of MCP tools based on the calculated bitmask. (✅ PASS)
5. **Reliable Persistence & Analytics**: Permission audit events will be persisted in the existing EF Core/SQLite event store. (✅ PASS)

## Project Structure

### Documentation (this feature)

```text
specs/luckyfish/002-agent-runtime-optimization/
├── spec.md              # Feature Specification
├── plan.md              # Implementation Plan (This file)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (Future)
```

### Source Code (repository root)

```text
ClawSharp.Lib/
├── Agents/
│   └── AgentDefinition.cs (Update with new permission fields if needed)
├── Configuration/
│   └── ClawOptions.cs (Add WorkspacePolicy and MandatoryTools)
├── Runtime/
│   ├── RuntimeContracts.cs (New IPermissionResolver, IPermissionUI)
│   ├── PermissionResolver.cs (New implementation)
│   └── ClawRuntime.cs (Integrate resolver into PrepareAgentAsync)
└── Tools/
    └── ToolContracts.cs (Update ToolPermissionSet and audit logic)

ClawSharp.CLI/
├── Infrastructure/
│   └── CliPermissionUI.cs (Implementation of IPermissionUI for REPL)
└── Commands/
    └── ChatCommand.cs (Handle JIT flow)

ClawSharp.Lib.Tests/
└── PermissionTests.cs (New test file)
```

**Structure Decision**: Using **Option 2: Full Application Stack** as this feature requires coordination between the Core Library (logic) and the CLI (UI interaction for JIT prompts).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | | |
