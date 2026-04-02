# Data Model: Add Avalonia+SukiUI GUI version

## UI ViewModels

### MainViewModel (Root)
Manages the application state, theme, and main view navigation.
- `CurrentView`: `ViewModelBase` (for switching between Chat and Settings).
- `IsSettingsOpen`: `bool`.
- `ToggleThemeCommand`: `ICommand`.

### ChatViewModel
Manages a single chat session.
- `Messages`: `ObservableCollection<MessageViewModel>`.
- `InputText`: `string`.
- `SendMessageCommand`: `ReactiveCommand<string, Unit>`.
- `SelectedAgent`: `AgentViewModel`.

### MessageViewModel
Represents a single message in the chat.
- `Content`: `string` (Markdown).
- `Sender`: `string` (User/Agent).
- `Timestamp`: `DateTime`.
- `IsAI`: `bool`.

### AgentViewModel
Represents an available agent for selection.
- `Name`: `string`.
- `Description`: `string`.
- `Icon`: `string` (SukiUI/Material icon name).
- `AgentDefinition`: `AgentDefinition` (ref to Lib model).

## State Transitions

### Message Sending
1. User enters `InputText`.
2. `SendMessageCommand` is triggered.
3. User `MessageViewModel` is added to `Messages`.
4. `InputText` is cleared.
5. `IClawKernel.ProcessAsync` is called (async).
6. Partial results (streaming) update the last AI `MessageViewModel.Content`.
7. Final result completes the message.

### Agent Selection
1. User clicks an `AgentCard`.
2. `SelectedAgent` in `ChatViewModel` is updated.
3. `IClawRuntime.StartSessionAsync` is called with the selected agent's ID.
4. `Messages` collection is cleared for the new session.

## Validation Rules
- **InputText**: Must not be empty or whitespace only before sending.
- **AgentSelection**: Must have at least one agent selected to send a message.
- **ThemeColor**: Must be a valid SukiUI theme color name.
