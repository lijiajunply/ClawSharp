# Quickstart: Add Avalonia+SukiUI GUI version

## Development Setup

1. **Install Dependencies**:
   Ensure you have the .NET 10 SDK installed.
   ```bash
   dotnet restore ClawSharp.slnx
   ```

2. **Run the Application**:
   You can run the GUI directly using the `dotnet` CLI:
   ```bash
   dotnet run --project ClawSharp.Desktop/ClawSharp.Desktop.csproj
   ```

3. **Workspace Configuration**:
   The GUI uses the same workspace as the CLI (`~/.clawsharp`). Ensure your agents are defined in `~/.clawsharp/agents/`.

## Key Features

- **Multi-Agent Sidebar**: Browse and switch between agents defined in your workspace.
- **Glassmorphic UI**: Powered by SukiUI for a modern, semi-transparent look.
- **Theme Switching**: Toggle between Light and Dark modes instantly from the sidebar footer.
- **Rich Markdown**: AI responses are rendered with full Markdown support including code blocks.
- **Virtualized Chat**: Efficiently handles long conversations using Avalonia's native virtualization.

## Troubleshooting

- **Build Errors**: Ensure all NuGet packages were restored correctly.
- **Agent Loading**: If no agents appear, verify that `ClawSharp.Lib` is looking in the correct `~/.agent` or `workspace/agents` paths.
- **Responsive UI**: All AI processing is async, the UI should never freeze during generation.
