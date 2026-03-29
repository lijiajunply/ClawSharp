# Implementation Plan: CLI Interface for ClawSharp.Lib

**Branch**: `001-cli-interface-implementation` | **Date**: 2026-03-29 | **Spec**: [specs/001-cli-interface-implementation/spec.md](spec.md)
**Input**: Feature specification for basic CLI interaction loop, ThreadSpace initialization, and session history management.

## Summary
Implement a cross-platform CLI application (`ClawSharp.CLI`) that provides a persistent REPL environment for interacting with ClawSharp agents. The CLI will serve as a thin wrapper around `ClawSharp.Lib`, ensuring all core logic remains in the library.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: ClawSharp.Lib, Microsoft.Extensions.DependencyInjection, Spectre.Console (for rich UI/REPL), System.CommandLine (for argument parsing)  
**Storage**: SQLite (via ClawSharp.Lib)  
**Testing**: xUnit (Integration tests in ClawSharp.Lib.Tests)  
**Target Platform**: Windows, macOS, Linux (Core .NET supported)
**Project Type**: CLI Application  
**Performance Goals**: TTFT < 2s (excluding network), Init < 5s  
**Constraints**: Must use .NET 10 patterns, strictly async operations, and follow Library-First principle.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Library-First**: ✅ All core logic (session management, agent loop, persistence) is already in `ClawSharp.Lib`. CLI will only handle I/O.
- **II. Markdown-Driven**: ✅ CLI will use `AgentRegistry` and `SkillRegistry` from Lib to load definitions.
- **III. Async-First**: ✅ All CLI commands will be async-native.
- **IV. Provider Extensibility**: ✅ CLI will use `IClawRuntime` which abstracts providers.
- **V. Reliable Persistence**: ✅ CLI will use the shared SQLite store via Lib.

## Project Structure

### Documentation (this feature)

```text
specs/001-cli-interface-implementation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

```text
# Option 2: Full Application Stack
ClawSharp.Lib/
└── [Existing Core Logic]

ClawSharp.CLI/
├── Commands/            # Individual command implementations (Init, Chat, List)
├── Infrastructure/      # CLI-specific DI and logging setup
└── Program.cs           # Entry point and REPL loop

ClawSharp.Lib.Tests/
└── [Integration tests for CLI scenarios]
```

**Structure Decision**: Selected Option 2 to maintain separation between the kernel (Lib) and the interface (CLI).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
