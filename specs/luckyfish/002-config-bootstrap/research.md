# Research: Configuration Bootstrap Wizard

## Decision 1: Trigger Point
**Decision**: Trigger the wizard in `Program.cs` before `ServiceConfigurator.BuildHost(args)` is called.
**Rationale**: This ensures that by the time the Host is built and services are registered (including `ClawOptions`), the configuration file already exists and can be loaded normally. It allows for a seamless "run any command -> setup -> execution" flow.
**Alternatives considered**: 
- Middleware in `System.CommandLine`: More complex to implement correctly with DI.
- Inside `ServiceConfigurator`: Might make the host building logic messy.

## Decision 2: File Check Logic
**Decision**: Specifically check for `appsettings.json` in the current working directory.
**Rationale**: `appsettings.json` is the primary configuration file. If it's missing, the system is considered "uninitialized".
**Handling `appsettings.Local.json` (FR-009)**: If `appsettings.json` is missing but `appsettings.Local.json` exists, the wizard will display a warning and ask if the user wants to:
1. Continue with the wizard (to create the main file).
2. Skip the wizard and use only the Local file (the app might still work if Local contains enough info).

## Decision 3: UI Implementation
**Decision**: Use `Spectre.Console` for all wizard prompts.
**Rationale**: Already used in the CLI for rich rendering. Provides `SelectionPrompt` for providers and `TextPrompt` with `.Secret()` for API keys.

## Decision 4: Configuration Template
**Decision**: Generate a basic structured JSON that mirrors `ClawOptions`.
**Rationale**: Ensures compatibility with the existing binder.
**Template Structure**:
```json
{
  "Runtime": {
    "WorkspaceRoot": ".",
    "DataPath": ".clawsharp"
  },
  "Providers": {
    "DefaultProvider": "openai",
    "Models": [
      {
        "Name": "openai",
        "Type": "openai-responses",
        "ApiKey": "..."
      }
    ]
  }
}
```

## Decision 5: Library vs CLI
**Decision**: Implement the core wizard logic in `ClawSharp.Lib.Configuration` (as a non-interactive configuration generator) and the interactive part in `ClawSharp.CLI`.
**Rationale**: Adheres to the "Library-First" mandate. The library can provide the template and file-writing logic, while the CLI handles the user input.
