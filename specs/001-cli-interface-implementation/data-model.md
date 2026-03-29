# Data Model: CLI to Kernel Mapping

## CLI Command Entities

### `CliCommand`
The base object representing a user-initiated action.
- `Name`: String (e.g., "init", "chat", "list")
- `Arguments`: Map<String, String>
- `Flags`: List<String>

## Mapping to ClawSharp.Lib

| CLI Command | Lib Action / Method | Data Requirements |
|-------------|----------------------|-------------------|
| `claw init` | `IClawKernel.InitializeAsync` / `ThreadSpace.Init` | Target path, WorkspacePolicy |
| `claw chat [agent]` | `IClawRuntime.StartSessionAsync` / `RunTurnAsync` | AgentID, ThreadSpaceID |
| `claw list` | `ISessionRecordRepository.GetByThreadSpaceAsync` | ThreadSpaceID |
| `claw history [id]`| `IClawRuntime.GetHistoryAsync` | SessionID |

## State Management
- **CurrentThreadSpace**: The CLI must store the ID/Path of the active ThreadSpace (usually the current working directory).
- **CurrentSession**: In REPL mode, the CLI maintains an active `SessionId`.

## Error Entities
- `ValidationException` (Lib) → `AnsiConsole.MarkupLine("[red]...[/]")`
- `OperationResult` (Lib) → `AnsiConsole.Status().Start(...)` or success/fail icons.
