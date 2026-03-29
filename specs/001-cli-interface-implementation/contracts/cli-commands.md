# CLI Command Contracts

## CLI Syntax Definition

```bash
# General Syntax
claw <command> [options]

# Commands
claw init                       # Initialize a ThreadSpace in the current folder.
claw chat <agent-id>            # Start a new REPL session with the specified agent.
claw list                       # List all sessions in the current ThreadSpace.
claw history <session-id>       # View message history for a session.
claw agents                     # List all registered agents.
claw skills                     # List all registered skills.
```

## Argument and Options Schema

### `init`
- **Optional**: `--path <dir-path>` (Default: `.`)
- **Action**: Call `IClawKernel.InitializeAsync`. Create `.clawsharp` folder and initialize SQLite DB if it doesn't exist.

### `chat <agent-id>`
- **Required**: `agent-id` (e.g., "planner")
- **Optional**: `--session <session-id>` (To resume an existing session)
- **Interaction**: Enter REPL mode.
- **Internal Loop**:
  1. Print `Agent Name vX`.
  2. Input: `User > `.
  3. Action: `AppendUserMessageAsync` → `RunTurnAsync`.
  4. Output: Live character streaming.

### `list`
- **Output**: Table with `SessionID`, `Agent`, `Last Message`, `Created At`.

### `history <session-id>`
- **Output**: Pretty-printed conversation blocks.
