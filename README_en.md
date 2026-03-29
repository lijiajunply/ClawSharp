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
- SQLite-backed session store, prompt history, and session event store
- Single-agent runtime loop with tool invocation and persisted history
- Local worker abstraction with loopback worker and stdio JSON-RPC transport shape
- Provider abstraction with:
  - `StubModelProvider` for tests and offline fallback
  - `OpenAiResponsesModelProvider`
  - `OpenAiCompatibleChatModelProvider`

The main runtime interface lives in [`IClawRuntime`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Runtime/RuntimeContracts.cs#L50). It currently supports:

- `StartSessionAsync`
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
  - Model provider abstraction
  - OpenAI Responses API support
  - OpenAI-compatible Chat Completions support
- `Runtime`
  - Sessions
  - Prompt history
  - Session events
  - Agent worker integration
  - Single-agent turn orchestration

## Provider Support

ClawSharp currently supports three provider modes:

1. `stub`
   Used for tests and explicit offline mode.

2. `openai-responses`
   The primary production path. This uses OpenAI's Responses API and supports:
   - streaming text
   - custom function-style tool calls
   - tool result round-trips inside the runtime loop

3. `openai-chat-compatible`
   A compatibility path for services that expose an OpenAI-style `chat/completions` API.

The provider resolver is implemented in [`ModelProviderResolver`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/ModelProviderContracts.cs#L174).  
The concrete provider implementations live in:

- [`OpenAiResponsesModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/OpenAiProviders.cs#L330)
- [`OpenAiCompatibleChatModelProvider`](/Users/luckyfish/Documents/Project/RiderProjects/ClawSharp/ClawSharp.Lib/Providers/OpenAiProviders.cs#L458)
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
```

Agent files are Markdown with YAML frontmatter. The body can hold longer instructions or notes.  
Skill files follow the same general pattern.

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
      }
    ]
  }
}
```

### Example `.env`

```dotenv
Providers__Models__0__ApiKey=your_openai_api_key
Providers__Models__1__ApiKey=your_compatible_api_key
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
- add provider selection at the agent definition level
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
