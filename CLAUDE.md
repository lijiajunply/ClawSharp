# CLAUDE.md

**请使用中文回答**

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build ClawSharp.slnx

# Run all tests
dotnet test ClawSharp.slnx

# Run a single test class
dotnet test ClawSharp.slnx --filter "FullyQualifiedName~RuntimeIntegrationTests"

# Run a single test method
dotnet test ClawSharp.slnx --filter "FullyQualifiedName~RuntimeIntegrationTests.RunTurn_WithStubProvider_ReturnsAssistantMessage"
```

## Architecture Overview

**ClawSharp** is a .NET 10 local-first AI application kernel (library). The core library `ClawSharp.Lib` provides all business logic; UI projects (`ClawSharp.Desktop`, `ClawSharp.CLI`) are thin consumers.

### Three-Layer Conceptual Model

- **Project** — template scaffolding and content directory generation
- **ThreadSpace** — workspace container bound to a folder, holds multiple sessions
- **Session** — the actual conversation execution unit within a ThreadSpace

### Subsystems

```
ClawSharp.Lib/
├── Configuration/   DI registration (ServiceCollectionExtensions), ClawOptions, ClawBuilder
├── Runtime/         IClawRuntime API, IClawKernel, session management, turn orchestration
│   └── Persistence/ EF Core + SQLite entities for sessions, messages, events
├── Agents/          AgentDefinition, Markdown+YAML frontmatter parsing, registry
├── Skills/          SkillDefinition, Markdown+YAML frontmatter parsing, registry
├── Tools/           Built-in tools (shell, file, search, system), capability permissions
├── Providers/       IModelProvider abstraction + OpenAI/Anthropic/Gemini/Stub implementations
├── Memory/          Vector-based memory, embedding providers, IVectorStore, scope hierarchy
├── Mcp/             MCP server catalog, session management, tool/resource/prompt discovery
└── Projects/        Project scaffolding service and template system
```

### Entry Point

All subsystems register via:
```csharp
services.AddClawSharp(builder => {
    builder.BasePath = "/path/to/workspace";
});
var runtime = provider.GetRequiredService<IClawRuntime>();
```

### Runtime Turn Flow

When `IClawRuntime.RunTurnAsync(sessionId)` executes:
1. `AgentLaunchPlan` assembles agent definition, skills, tools, MCP servers, and provider target
2. `ModelProviderResolver` resolves: agent-level provider override → global `DefaultProvider`
3. Provider is invoked with message history and tool schemas
4. Tool loop handles agent-initiated tool calls and feeds results back
5. All messages/events are persisted to SQLite
6. Returns `RunTurnResult` with final assistant message and metrics

### Agent & Skill Definitions

Defined as Markdown files with YAML frontmatter:
```yaml
---
id: planner
name: Planner
provider: claude          # optional, overrides global default
model: ""
system_prompt: You are a careful planner.
memory_scope: workspace   # workspace | agent | session
tools: [shell.run, file.read]
skills: [summarizer]
mcp_servers: [filesystem]
permissions:
  capabilities: [file.read, shell.execute]
  allowed_read_roots: ["/projects"]
---
```

### Tool Permission Model

`ToolPermissionSet` merges workspace-level and agent-level permissions using intersection (most restrictive). Capability flags: `ShellExecute`, `FileRead`, `FileWrite`, `SystemInspect`, `NetworkAccess`.

### Provider Support

| Provider | Protocol |
|----------|----------|
| OpenAI | Responses API (streaming) |
| OpenAI | Chat Completions (compatible: Gemini, local LLMs) |
| Anthropic | Messages API (native tool_use/tool_result) |
| Stub | In-process, no network (for tests/offline) |

### Configuration Hierarchy (lowest → highest priority)

`appsettings.json` → local settings → `.env` → environment variables → code overrides

### Persistence

EF Core + SQLite entities: `ThreadSpaceEntity`, `SessionEntity`, `MessageEntity` (with structured content blocks), `SessionEventEntity`.

### Memory Scope Hierarchy

- Workspace: `workspace:{id}`
- Agent: `agent:{workspace}:{agent}`
- Session: `session:{workspace}:{sessionId}`

## Test Patterns

Tests use `IDisposable` with temp directories and build the full DI container:
```csharp
private IClawRuntime CreateRuntime()
{
    var services = new ServiceCollection();
    services.AddClawSharp(builder => { builder.BasePath = _root; });
    return services.BuildServiceProvider().GetRequiredService<IClawRuntime>();
}
```

Key test files: `RuntimeIntegrationTests`, `ProviderTests` (stream parsing), `ToolSecurityTests` (permission merging), `SessionStoreTests` (EF Core persistence), `MarkdownParsingTests`.

## Active Technologies
- C# 14 / .NET 10 + System.CommandLine, Spectre.Console, EF Core 9 + SQLite (luckyfish/002-redesign-threadspace-cli)
- SQLite via EF Core migrations (`ClawDbContext`) (luckyfish/002-redesign-threadspace-cli)

## Recent Changes
- luckyfish/002-redesign-threadspace-cli: Added C# 14 / .NET 10 + System.CommandLine, Spectre.Console, EF Core 9 + SQLite
