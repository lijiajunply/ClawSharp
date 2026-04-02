# Research: Add Avalonia+SukiUI GUI version

## SukiUI Integration

### Decision:
Use `SukiUI` for the main application theme and component styles.

### Rationale:
SukiUI provides a modern, "Glassmorphism" inspired design out of the box with built-in theme management and high-quality components.

### Findings:
- SukiUI needs to be added as a style in `App.axaml`:
  ```xml
  <Application.Styles>
      <sukiUi:SukiTheme ThemeColor="Blue" />
  </Application.Styles>
  ```
- Use `SukiWindow` for the main window to get native-looking title bars and Suki-specific controls.
- Theme switching is handled by `SukiTheme.GetInstance().ChangeTheme()`.

## Dependency Injection (Avalonia + Microsoft.Extensions.DependencyInjection)

### Decision:
Use `Microsoft.Extensions.DependencyInjection` for consistency with `ClawSharp.Lib` and `ClawSharp.CLI`.

### Rationale:
The core kernel and models are already built for DI. We want to avoid static singletons or inconsistent service lifetimes.

### Findings:
- Standard practice in Avalonia for DI is to configure services in `Program.cs` or a separate `ServiceConfigurator`.
- `App.axaml.cs` should be responsible for resolving the `MainWindow` and its `ViewModel`.
- We can use `Splat` (ReactiveUI's internal DI) but mapping it to `Microsoft.Extensions.DependencyInjection` is better for a unified container.

## Integrating ClawSharp.Lib Reactive State

### Decision:
Bridge `ClawSharp.Lib` events and state to `ReactiveUI` ViewModels using `ObservableCollection` and `ReactiveObject`.

### Rationale:
`ClawSharp.Lib` uses `Task` based patterns and internal state. ViewModels need to react to message arrivals and streaming responses.

### Findings:
- Create a `ChatViewModel` that observes message updates from the kernel.
- Use `DynamicData` (part of ReactiveUI) if advanced collection manipulation (sorting, filtering) is needed for the agent list.
- Stream AI responses by updating the `Content` of the last message in an `ObservableCollection`.

## Markdown Rendering in Avalonia

### Decision:
Use `Avalonia.Markdown` or `Markdown.Avalonia` for rendering AI responses.

### Rationale:
AI responses are Markdown-heavy (code blocks, bold, lists). Raw text is insufficient for a "modern" GUI.

### Findings:
- `Markdown.Avalonia` is a mature library that can render Markdown directly into Avalonia controls.
- It supports code highlighting (usually via `AvaloniaEdit` integration).
- Needs to be configured as a control in `ChatView.axaml`.

## Alternatives Considered:
- **Vanilla Avalonia**: Rejected because it requires significant styling effort to achieve the "modern" look SukiUI provides.
- **FluentAvalonia**: Good alternative, but SukiUI's "Glass" aesthetic matches the "ClawSharp" branding better.
- **Direct UI to Kernel coupling**: Rejected due to "Library-First" principle (need a clean abstraction via ViewModels).
