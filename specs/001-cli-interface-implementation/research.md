# Research: CLI Implementation Patterns for ClawSharp

## Objective
Identify best practices for building a modern, async-native CLI in .NET 10 that integrates with the existing ClawSharp.Lib kernel.

## Key Research Areas

### 1. CLI Framework Selection
- **Option A: System.CommandLine**
  - *Pros*: Official Microsoft library, robust argument parsing, posix-compliant.
  - *Cons*: Still technically in "preview/beta" but very stable.
- **Option B: Spectre.Console.Cli**
  - *Pros*: Beautiful output, easy to use, built-in support for tables, progress bars, and prompts.
  - *Cons*: Slightly more opinionated about command structure.
- **Decision**: Use **Spectre.Console** for UI/Rich-IO and **System.CommandLine** for the core routing. This combination provides the best of both worlds: robust parsing and high-quality user experience.

### 2. REPL Loop Implementation
- **Implementation Strategy**:
  1. Initialize `IClawRuntime`.
  2. Main Loop:
     - `AnsiConsole.Ask<string>("[green]User >[/]")`
     - Command Parsing (if input starts with `/`, treat as command, otherwise as chat message).
     - Streaming output: Use `AnsiConsole.Live` or manual `Console.Write` to update characters as they arrive from `IAsyncEnumerable<StreamContentBlock>`.
- **Exit Strategy**: Support `exit`, `quit`, or `Ctrl+C`.

### 3. ThreadSpace Integration
- **Approach**: The CLI should check for a `.clawsharp` metadata folder or the presence of a SQLite DB to detect if the current directory is an initialized ThreadSpace.
- **Automatic Initialization**: If `claw ask` is run outside a ThreadSpace, it should prompt to initialize or use a global default (as defined in `ClawOptions`).

### 4. Error Handling
- **Pattern**: Catch `ValidationException` and display it using `AnsiConsole.MarkupLine("[red]Validation Error:[/] " + ex.Message)`.
- **Logging**: Use `Microsoft.Extensions.Logging.Console` but with a simplified formatter to avoid cluttering the REPL.

## Consistently Evaluated Alternatives
- **Pure System.Console**: Rejected due to lack of rich styling (colors, tables, live updates) which are critical for a modern AI CLI.
- **Cocona**: A lightweight CLI library, but `System.CommandLine` is preferred for long-term alignment with the .NET ecosystem.

## Conclusion
We will build `ClawSharp.CLI` using .NET 10, targeting a rich REPL experience powered by `Spectre.Console`. The CLI will act as a client to the `IClawRuntime` service.
