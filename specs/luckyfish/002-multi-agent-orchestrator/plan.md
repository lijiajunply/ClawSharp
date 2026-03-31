# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

This feature introduces the **Orchestrator Pattern** to ClawSharp. It centers on a specialized `SupervisorAgent` that can automatically discover other registered agents and invoke them as tools (via `AgentTool`). The implementation prioritizes the **Least Privilege** permission model (FR-007) and robust loop detection (SC-004) to ensure safe and efficient multi-agent collaboration within the `ClawSharp.Lib` kernel.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: `ClawSharp.Lib`, `Microsoft.Extensions.DependencyInjection`, `YamlDotNet`, `McpProtocol`  
**Storage**: SQLite (via EF Core) for session/history; DuckDB for analytics.  
**Testing**: xUnit with new integration test suite for multi-agent delegation.  
**Target Platform**: .NET 10 Runtime (Windows/macOS/Linux).
**Project Type**: Library/CLI.  
**Performance Goals**: Delegation overhead < 500ms (SC-002).  
**Constraints**: Async-first, Modern .NET 10, Library-first (Constitution I, III), Least Privilege (FR-007).
**Scale/Scope**: Support for complex multi-agent workflows with depth-limited delegation (FR-006).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Principle I (Library-First)**: ✅ All orchestration logic (Supervisor, AgentTool) is implemented in `ClawSharp.Lib`.
- **Principle II (Markdown-Driven)**: ✅ Agents used as tools are defined via standard Markdown/YAML schemas.
- **Principle III (Async-First)**: ✅ All delegation and tool invocation methods are async.
- **Principle IV (Extensibility)**: ✅ `AgentTool` leverages the existing tool abstraction, supporting both local and MCP tools.
- **Principle V (Persistence)**: ✅ Delegation records are stored in SQLite via EF Core.


## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
# Option 1: ClawSharp Core (ClawSharp.Lib focus)
ClawSharp.Lib/
├── Agents/
├── Configuration/
├── Core/
├── Mcp/
├── Memory/
├── Projects/
├── Providers/
├── Runtime/
├── Skills/
└── Tools/

ClawSharp.Lib.Tests/
└── [Unit and Integration Tests]

# Option 2: Full Application Stack
ClawSharp.Lib/
└── [Core Kernel Logic]

ClawSharp.CLI/
└── [Terminal Interface]

ClawSharp.Desktop/
├── Views/
├── ViewModels/
└── Models/

ClawSharp.Lib.Tests/
└── [Tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
