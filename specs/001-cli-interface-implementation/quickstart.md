# Quickstart: ClawSharp CLI

## Prerequisites
- .NET 10 SDK installed.
- Valid API keys in `.env` at the repository root.

## Installation
Currently, run the CLI via `dotnet run` from the root directory specifying the project:

```bash
dotnet run --project ClawSharp.CLI -- init
```

## First Chat Session
1. Initialize your project folder:
   ```bash
   dotnet run --project ClawSharp.CLI -- init
   ```
2. Check available agents:
   ```bash
   dotnet run --project ClawSharp.CLI -- agents
   ```
3. Start a conversation with the `planner` agent:
   ```bash
   dotnet run --project ClawSharp.CLI -- chat planner
   ```
4. Type your message and press Enter.
5. Exit the session by typing `/exit` or `/quit`.

## Common Commands
- **List past sessions**: `dotnet run --project ClawSharp.CLI -- list`
- **View specific history**: `dotnet run --project ClawSharp.CLI -- history <id>`
- **List skills**: `dotnet run --project ClawSharp.CLI -- skills`
