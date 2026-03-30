# Research: CLI Configuration Operations

## Decision 1: Configuration Storage Location
**Decision**: Use `appsettings.Local.json` in the application base directory for primary configuration persistence. Support `.env` for secrets if present.
**Rationale**: `ClawConfigurationLoader` already looks for `appsettings.Local.json` and `.env`. Using these files maintains consistency and follows standard .NET patterns.
**Alternatives considered**: 
- `~/.config/clawsharp/config.json`: Standard for Linux but requires custom loading logic.
- Dedicated SQLite table: overkill for simple key-value pairs.

## Decision 2: Implementation Layer
**Decision**: Implement `IConfigManager` in `ClawSharp.Lib.Configuration`.
**Rationale**: Adheres to the "Library-First" mandate. This allows other consumers (like a Desktop UI) to use the same configuration logic.
**Alternatives considered**: 
- Implementing logic directly in CLI commands: violates the Library-First principle.

## Decision 3: CLI UI Patterns
**Decision**: Use `Spectre.Console` for rich table display in `config list` and `AnsiConsole.Prompt` with `IsSecret = true` for interactive secret input.
**Rationale**: Provides a professional, user-friendly terminal experience consistent with existing commands.
**Alternatives considered**: 
- Standard `Console.ReadLine`: lacks masking and rich formatting.

## Decision 4: Masking Strategy
**Decision**: Define a hardcoded list of "known secret keys" (e.g., keys containing ".Key", ".Token", ".Secret", "ApiKey") and use a 8-character `********` mask.
**Rationale**: Balances security with usability. Hardcoded patterns are simple to maintain initially.
**Alternatives considered**: 
- Metadata-driven secrets: Requires a more complex configuration schema.
