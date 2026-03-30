# ClawSharp

[中文](./README.md)

ClawSharp is a local-first AI application kernel written in C# and targeting `.NET 10`.

The long-term goal is to align with the core ideas behind OpenClaw:

- MCP integration
- Markdown-based agents
- Markdown-based skills
- Built-in local tools
- Vector memory
- Local configuration with JSON and `.env`
- A reusable library-first architecture that can later power CLI and Web apps

This repository is currently focused on `ClawSharp.Lib`. The library already contains a working runtime skeleton, persistent sessions, provider abstraction, tool execution, memory, and real OpenAI-compatible provider support.

## Current Status

What is already implemented in `ClawSharp.Lib`:

- Core DI entrypoint via [`AddClawSharp(...)`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Configuration/ServiceCollectionExtensions.cs#L26)
- Markdown agent parsing and registry loading
- Markdown skill parsing and registry loading
- Built-in tools:
  - `shell.run`
  - `file.read`
  - `file.write`
  - `file.list`
  - `system.info`
  - `system.processes`
  - `search.text`
  - `search.files`
- Tool capability and permission model
- MCP client skeleton and process-backed sessions
- Memory abstractions with default local implementations
- ThreadSpace containers with bound folders and a default `init` workspace
- SQLite-backed session store, prompt history, and session event store
- Single-agent runtime loop with tool invocation and persisted history
- Local worker abstraction with loopback worker and stdio JSON-RPC transport shape
- Provider abstraction with:
  - `StubModelProvider` for tests and offline fallback
  - `OpenAiResponsesModelProvider`
  - `OpenAiCompatibleChatModelProvider`

The main runtime interface lives in [`IClawRuntime`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Runtime/RuntimeContracts.cs#L50). It currently supports:

- `StartSessionAsync`
- `StartSessionAsync(StartSessionRequest)`
- `AppendUserMessageAsync`
- `RunTurnAsync`
- `GetHistoryAsync`
- `CancelSessionAsync`

## Architecture

The library is organized around a few major subsystems:

- `Configuration`
  - Strongly typed options
  - JSON + `.env` + environment variable loading
- `Agents`
  - Markdown frontmatter parsing
  - File-system backed definition loading
  - Registry model
- `Skills`
  - Markdown-based skill definitions
  - Registry model
- `Tools`
  - Tool schema + executor abstraction
  - Permissions, allowlists, and execution context
- `Mcp`
  - MCP catalog and client/session skeleton
- `Memory`
  - Embedding abstraction
  - Vector store abstraction
  - Local default implementation
- `Providers`
  - Model provider abstraction with structured content blocks
  - OpenAI Responses API support
  - OpenAI-compatible Chat Completions support
- `Runtime`
  - ThreadSpaces
  - Sessions
  - Prompt history
  - Session events
  - Agent worker integration
  - Single-agent turn orchestration

## Project / ThreadSpace / Session

ClawSharp now separates three layers of workspace concepts:

- `Project`
  - template-driven scaffolding for content directories
- `ThreadSpace`
  - a chat/work container bound to a specific folder, or a global container (`global`) without any folder binding
  - a single ThreadSpace can contain multiple sessions
  - the runtime always guarantees a default `global` ThreadSpace for general conversations across projects
- `session`
  - the concrete conversation execution unit inside a ThreadSpace

### CLI Experience

Simply run `claw` to enter REPL mode. By default, it starts a conversation in the `global` ThreadSpace.

**Interactive Features:**
- **Intelligent Suggestions**: Displays grey "ghost text" suggestions as you type; press `Tab` or `→` to complete.
- **Persistent History**: Use Up/Down arrows to navigate command history, saved across sessions in `.clawsharp/cli_history.txt`.
- **Shortcuts**: `Ctrl+U` to quickly clear the current line.

Supported slash commands:
- `/help`: Show help
- `/new`: Start a new session
- `/resume`: Resume the last session
- `/cd <path>`: Switch to a specific directory-bound workspace
- `/home`: Return to the global space
- `/init`: Initialize an `agent.md` definition in the current space
- `/init-proj`: Interactively scaffold a new project from templates
- `/clear`: Clear screen
- `/quit, /exit`: Exit the REPL

## Provider Support

ClawSharp currently supports five provider modes:

1. `stub`
   Used for tests and explicit offline mode.

2. `openai-responses`
   The primary production path. This uses OpenAI's Responses API and supports:
   - streaming text
   - custom function-style tool calls
   - tool result round-trips inside the runtime loop

3. `openai-chat-compatible`
   A compatibility path for services that expose an OpenAI-style `chat/completions` API.

4. `gemini-openai-compatible`
   For Gemini's OpenAI-compatible endpoint, with `chat/completions` as the default path.

5. `anthropic-messages`
   For Claude via Anthropic's Messages API.
   The current implementation supports both streaming text and Claude-native `tool_use` / `tool_result` loops.

The provider resolver is implemented in [`ModelProviderResolver`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/ModelProviderContracts.cs#L174).  
The concrete provider implementations live in:

- [`OpenAiResponsesModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/OpenAiProviders.cs#L330)
- [`OpenAiCompatibleChatModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/OpenAiProviders.cs#L458)
- [`GeminiCompatibleChatModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/OpenAiProviders.cs)
- [`AnthropicMessagesModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/AnthropicProviders.cs)
- [`StubModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/ModelProviderContracts.cs#L235)

## Agent and Skill Layout

The current convention is:

```text
workspace/
  agents/
    <agent-id>/
      agent.md
  skills/
    <skill-id>/
      SKILL.md
      assets/
      scripts/
  project-templates/
    <template-id>/
      template.md
      ...
```

Agent files are Markdown with YAML frontmatter. The body can hold longer instructions or notes.  
Skill files follow the same general pattern.
The project template directory powers the `ClawSharp.Lib.Projects` scaffolding API; template metadata lives in `template.md`, and every other file is generated into the new project using its relative path.

Agent frontmatter now supports an optional `provider` field so each agent can target a specific model vendor. If omitted, runtime falls back to `Providers:DefaultProvider`. `model` can also be left empty, in which case the selected provider's `DefaultModel` is used first.

## Configuration

Configuration is loaded in this order:

1. `appsettings.json`
2. `appsettings.Local.json`
3. `.env`
4. environment variables
5. explicit runtime overrides passed into `AddClawSharp`

The main options type is [`ClawOptions`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Configuration/ClawOptions.cs#L5).

Important sections:

- `Runtime`
- `Agents`
- `Projects`
- `Tools`
- `Mcp`
- `Memory`
- `Embedding`
- `Sessions`
- `History`
- `Providers`
- `Worker`
- `WorkspacePolicy`

### Example `appsettings.json`

```json
{
  "Runtime": {
    "WorkspaceRoot": ".",
    "DataPath": ".clawsharp"
  },
  "Sessions": {
    "DatabasePath": ".clawsharp/clawsharp.db"
  },
  "Providers": {
    "DefaultProvider": "openai",
    "Models": [
      {
        "Name": "openai",
        "Type": "openai-responses",
        "BaseUrl": "https://api.openai.com",
        "DefaultModel": "gpt-4.1-mini",
        "SupportsResponses": true
      },
      {
        "Name": "compat",
        "Type": "openai-chat-compatible",
        "BaseUrl": "https://your-compatible-endpoint.example",
        "DefaultModel": "your-model",
        "SupportsChatCompletions": true
      },
      {
        "Name": "gemini",
        "Type": "gemini-openai-compatible",
        "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/openai",
        "DefaultModel": "gemini-2.5-flash",
        "SupportsChatCompletions": true
      },
      {
        "Name": "claude",
        "Type": "anthropic-messages",
        "BaseUrl": "https://api.anthropic.com",
        "DefaultModel": "claude-sonnet-4-20250514"
      }
    ]
  }
}
```

### Example `.env`

```dotenv
Providers__Models__0__ApiKey=your_openai_api_key
Providers__Models__1__ApiKey=your_compatible_api_key
Providers__Models__2__ApiKey=your_gemini_api_key
Providers__Models__3__ApiKey=your_anthropic_api_key
```

### Example `agent.md` frontmatter

```yaml
---
id: planner
name: Planner
description: Multi-provider planner
provider: claude
model: ""
system_prompt: You are a careful planner.
memory_scope: workspace
version: v1
---
```

## Using the Library

The library is designed to be the shared kernel for future CLI and Web frontends.

Basic DI setup:

```csharp
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddClawSharp(builder =>
{
    builder.BasePath = Directory.GetCurrentDirectory();
});

var provider = services.BuildServiceProvider();
var runtime = provider.GetRequiredService<IClawRuntime>();

await runtime.InitializeAsync();

var session = await runtime.StartSessionAsync("planner");
await runtime.AppendUserMessageAsync(session.Record.SessionId, "Hello");

var result = await runtime.RunTurnAsync(session.Record.SessionId);
var history = await runtime.GetHistoryAsync(session.Record.SessionId);
```

To use the project scaffolding service, resolve `IProjectScaffolder` from DI:

```csharp
using ClawSharp.Lib.Projects;

var scaffolder = provider.GetRequiredService<IProjectScaffolder>();

var createResult = await scaffolder.CreateProjectAsync(
    new CreateProjectRequest(
        "paper",
        "My Research Project",
        "projects/my-research",
        new Dictionary<string, string>
        {
            ["author"] = "Lucky Fish",
            ["project_summary"] = "Used to organize research notes, writing plans, and experiment logs."
        }));
```

The default template path is `workspace/project-templates`.  
Each project type gets its own folder, for example:

```text
workspace/
  project-templates/
    paper/
      template.md
      docs/
        outline.md
      references/
        bibliography.md
```

`template.md` uses YAML frontmatter for metadata, and its Markdown body is appended to the generated `README.md`:

```md
---
id: paper
name: Paper Project
description: A template for organizing paper writing and research materials.
version: v1
directories:
  - docs
  - references
variables:
  author: Unknown Author
---
## Research Context
Capture the main question, assumptions, and references here.
```

Both template file names and file contents support simple variable interpolation such as `{{project_name}}`, `{{project_type}}`, `{{created_at}}`, and any custom variables provided by the caller.  
Every generated project always gets a unified `README.md`, even if the template does not declare one directly.

## Testing

Run the test suite with:

```bash
/usr/local/share/dotnet/dotnet test ClawSharp.slnx
```

At the time of writing, the repository includes tests for:

- agent and skill Markdown parsing
- registries
- tool security and permission merging
- configuration precedence and provider binding
- SQLite session/history/event storage
- memory indexing
- MCP startup failure and timeout behavior
- provider resolution
- Responses API stream parsing
- OpenAI-compatible chat stream parsing
- runtime integration with stub and real provider-style streams

## What This Project Is Not Yet

ClawSharp is not yet:

- a finished CLI
- a finished Web UI
- a multi-agent orchestration platform
- a production-ready MCP host/server
- a complete external worker process implementation
- a full RAG system with automatic retrieval in every turn

Those pieces are planned, but the current emphasis is the reusable application kernel.

## Roadmap

The most natural next steps are:

- add a real external worker executable for stdio JSON-RPC
- add a first CLI on top of `IClawRuntime`
- add `.env.example` and richer sample configs
- improve provider support with retries, richer error handling, and more wire-shape coverage
- deepen MCP integration so MCP tools/resources participate more directly in prompt assembly
- expand memory integration into optional retrieval pipelines

## Project Direction

The original goal was to build the lowest-level kernel for a local AI application first, then add CLI and Web layers on top.  
The repository now reflects that direction:

- the domain model is library-first
- the runtime is persistent and testable
- the provider layer can already talk to real external APIs
- the tool loop and history model are in place for future interfaces

That makes `ClawSharp.Lib` the foundation for everything that comes next.
