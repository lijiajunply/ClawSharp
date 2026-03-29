# ClawSharp Project Context

**请使用中文回答**

ClawSharp is a local-first AI application kernel built with C# for .NET 10. It provides a robust library (`ClawSharp.Lib`) for building AI-powered applications with features like MCP integration, Markdown-defined agents and skills, a local tool system, and vector-based memory.

## Project Overview

- **Core Framework:** .NET 10
- **Primary Language:** C#
- **Main Library:** `ClawSharp.Lib` (The engine/kernel)
- **UI Applications:** 
  - `ClawSharp.CLI`: Terminal interface (under development)
  - `ClawSharp.Desktop`: Avalonia-based cross-platform UI (under development)
- **Persistence:** Entity Framework Core with SQLite for sessions/history; DuckDB for analytics.
- **AI Connectivity:** Supports OpenAI (Responses API), OpenAI-compatible, Gemini (OpenAI-compatible), and Anthropic (Messages API).

## Architecture & Concepts

- **Kernel (`IClawKernel`):** Aggregates all sub-systems including Agent/Skill registries, Tool system, Memory, and Session management.
- **Runtime (`IClawRuntime`):** The high-level API for managing AI sessions, turn orchestration, and history.
- **ThreadSpace:** A workspace container bound to a specific folder. It can host multiple sessions.
- **Agents & Skills:** Defined using Markdown files with YAML frontmatter (located in `workspace/agents` and `workspace/skills`).
- **Tools:** Built-in tools for shell execution, file operations, system info, and search. MCP tools are also supported.

## Building and Running

- **Build Solution:**
  ```bash
  dotnet build ClawSharp.slnx
  ```
- **Run Tests:**
  ```bash
  dotnet test ClawSharp.slnx
  ```
- **Configuration:**
  - Main configuration: `appsettings.json` / `appsettings.Local.json`
  - Secrets/API Keys: `.env` file (see `README.md` for format)

## Development Conventions

- **Modern C#:** Uses .NET 10 features, implicit usings, and nullable reference types.
- **Async-First:** Most runtime and repository operations are asynchronous (`Task` / `ValueTask`).
- **Dependency Injection:** Relies heavily on `Microsoft.Extensions.DependencyInjection`. Use `AddClawSharp(...)` to register services.
- **Testing:** Uses xUnit for testing. Integration tests in `ClawSharp.Lib.Tests` demonstrate runtime usage.
- **Persistence Patterns:** Uses Repository pattern with Entity Framework Core. Migrations are located in `ClawSharp.Lib/Runtime/Persistence/Migrations`.
- **Markdown Definitions:** Agents and Skills must follow the YAML frontmatter schema defined in `AgentDefinition.cs` and `SkillDefinition.cs`.

## Key Files for Reference

- `ClawSharp.Lib/Runtime/RuntimeContracts.cs`: Core `IClawRuntime` and `IClawKernel` interfaces.
- `ClawSharp.Lib/Configuration/ClawOptions.cs`: Root configuration object.
- `ClawSharp.Lib/Configuration/ServiceCollectionExtensions.cs`: DI registration logic.
- `ClawSharp.Lib/Providers/ModelProviderContracts.cs`: AI Provider abstractions.
- `workspace/`: Default directory for agents, skills, and project templates.
