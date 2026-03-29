<!--
Sync Impact Report:
- Version change: Placeholder → 1.0.0
- List of modified principles:
    - [PRINCIPLE_1_NAME] → I. Library-First & Local-First
    - [PRINCIPLE_2_NAME] → II. Markdown-Driven Intelligence
    - [PRINCIPLE_3_NAME] → III. Async-First & Modern .NET
    - [PRINCIPLE_4_NAME] → IV. Provider & Tool Extensibility
    - [PRINCIPLE_5_NAME] → V. Reliable Persistence & Analytics
- Added sections: Technical Constraints, Development Workflow
- Removed sections: None
- Templates requiring updates:
    - .specify/templates/plan-template.md (✅ updated/aligned)
    - .specify/templates/spec-template.md (✅ updated/aligned)
    - .specify/templates/tasks-template.md (✅ updated/aligned)
- Follow-up TODOs: None
-->

# ClawSharp Constitution

## Core Principles

### I. Library-First & Local-First
Every feature MUST be implemented in `ClawSharp.Lib` first. The library is the core engine/kernel of the project. UI applications like `ClawSharp.CLI` and `ClawSharp.Desktop` should only be thin consumers of this library. Prioritize local execution, data privacy, and minimal external dependencies.

### II. Markdown-Driven Intelligence
Agents, skills, and project templates MUST be defined using Markdown files with YAML frontmatter. This ensures that AI definitions are human-readable, version-controllable, and portable across different environments. The system must support loading these definitions from the local filesystem.

### III. Async-First & Modern .NET
Strictly adhere to .NET 10 features and patterns. All runtime, model provider, and persistence operations MUST be asynchronous (`Task` or `ValueTask`). Nullable reference types must be enabled and strictly respected across the entire codebase to ensure type safety.

### IV. Provider & Tool Extensibility
The architecture MUST support multiple AI providers (OpenAI, Anthropic, Gemini) through a unified abstraction (`IModelProvider`). The tool system must be flexible enough to handle both built-in local tools and external tools provided via the Model Context Protocol (MCP).

### V. Reliable Persistence & Analytics
All user sessions, message history, and runtime events MUST be persisted using Entity Framework Core with a SQLite backend. Performance metrics and usage analytics should be stored and queried using DuckDB to enable efficient local data processing.

## Technical Constraints

- **Target Framework**: .NET 10 (Current)
- **Primary Language**: C# 14 (or latest supported by .NET 10)
- **Persistence**: EF Core with SQLite for operational data; DuckDB for analytical data.
- **UI Frameworks**: Avalonia for cross-platform desktop; planned Terminal interface for CLI.
- **AI Integration**: Support for OpenAI Responses API, OpenAI-compatible Chat Completions, and Anthropic Messages API.

## Development Workflow

- **Dependency Injection**: Use `Microsoft.Extensions.DependencyInjection` as the standard for component registration and lifecycle management.
- **Repository Pattern**: Implement data access through well-defined repositories to decouple the domain logic from persistence details.
- **Testing Discipline**: Every new feature or bug fix MUST include corresponding unit or integration tests using xUnit. Integration tests should be placed in `ClawSharp.Lib.Tests`.
- **Markdown Schema**: Any changes to Agent or Skill definitions must update the corresponding Markdown parsers and YAML frontmatter schemas.

## Governance

This constitution is the foundational document for ClawSharp. All architectural decisions, pull requests, and feature implementations must align with these principles.

- **Amendment Process**: Any change to this constitution requires a version increment (MAJOR for principle removal, MINOR for additions).
- **Compliance**: Code reviews must verify that changes do not violate the "Library-First" or "Async-First" mandates.
- **Version Tracking**: The `CONSTITUTION_VERSION` must be updated in all related documentation when changed.

**Version**: 1.0.0 | **Ratified**: 2026-03-29 | **Last Amended**: 2026-03-29
