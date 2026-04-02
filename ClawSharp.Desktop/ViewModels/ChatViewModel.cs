using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ClawSharp.Lib.Core;
using ClawSharp.Lib.Runtime;
using ReactiveUI;
using Microsoft.Extensions.DependencyInjection;

namespace ClawSharp.Desktop.ViewModels;

public class ChatViewModel : ViewModelBase
{
    private readonly IClawRuntime _runtime;
    private readonly IClawKernel _kernel;
    private SessionId? _currentSessionId;

    public ObservableCollection<AgentViewModel> Agents { get; } = new();

    private AgentViewModel? _currentAgent;
    public AgentViewModel? CurrentAgent
    {
        get => _currentAgent;
        set => this.RaiseAndSetIfChanged(ref _currentAgent, value);
    }

    private string _inputText = string.Empty;
    public string InputText
    {
        get => _inputText;
        set => this.RaiseAndSetIfChanged(ref _inputText, value);
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set => this.RaiseAndSetIfChanged(ref _isProcessing, value);
    }

    public ObservableCollection<MessageViewModel> Messages { get; } = new();

    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; }

    public ChatViewModel(IClawRuntime runtime, IClawKernel kernel)
    {
        _runtime = runtime;
        _kernel = kernel;

        var canSend = this.WhenAnyValue(
            x => x.InputText,
            x => x.IsProcessing,
            x => x.CurrentAgent,
            (text, processing, agent) => !string.IsNullOrWhiteSpace(text) && !processing && agent != null);

        SendMessageCommand = ReactiveCommand.CreateFromTask(SendMessageAsync, canSend);

        // React to agent selection
        this.WhenAnyValue(x => x.CurrentAgent)
            .Where(x => x != null)
            .Subscribe(async agent => await InitializeAsync(agent!.Id));
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || CurrentAgent == null) return;

        var userContent = InputText;
        InputText = string.Empty;
        IsProcessing = true;

        try
        {
            // Ensure session exists
            if (_currentSessionId == null)
            {
                var session = await _runtime.StartSessionAsync(CurrentAgent.Id);
                _currentSessionId = session.Record.SessionId;
            }

            // Add user message to UI
            Messages.Add(new MessageViewModel(userContent, "User", false));

            // Send to runtime
            await _runtime.AppendUserMessageAsync(_currentSessionId.Value, userContent);

            // Create AI message placeholder
            var aiMessage = new MessageViewModel("", CurrentAgent.Name, true);
            Messages.Add(aiMessage);

            // Process streaming response
            await foreach (var @event in _runtime.RunTurnStreamingAsync(_currentSessionId.Value))
            {
                if (@event.Delta != null)
                {
                    aiMessage.Content += @event.Delta;
                }
            }
        }
        catch (Exception ex)
        {
            Messages.Add(new MessageViewModel($"Error: {ex.Message}", "System", false));
        }
        finally
        {
            IsProcessing = false;
        }
    }

    public async Task LoadSessionAsync(SessionId sessionId)
    {
        var runtimeSession = await _kernel.Sessions.GetAsync(sessionId);
        _currentSessionId = sessionId;
        
        CurrentAgent = Agents.FirstOrDefault(a => a.Id == runtimeSession.Record.AgentId);
        
        Messages.Clear();
        var history = await _runtime.GetHistoryAsync(sessionId);
        foreach (var entry in history.Where(e => e.Message != null))
        {
            var msg = entry.Message!;
            Messages.Add(new MessageViewModel(
                msg.Content, 
                msg.Role == PromptMessageRole.Assistant ? (CurrentAgent?.Name ?? "Agent") : "User", 
                msg.Role == PromptMessageRole.Assistant));
        }
    }

    public async Task InitializeAsync(string agentId = "luckyfish")
    {
        // Load agents if not loaded
        if (Agents.Count == 0)
        {
            var agents = _kernel.Agents.GetAll();
            foreach (var a in agents)
            {
                Agents.Add(new AgentViewModel(a));
            }
            CurrentAgent = Agents.FirstOrDefault(a => a.Id == agentId) ?? Agents.FirstOrDefault();
        }

        if (CurrentAgent == null) return;

        Messages.Clear();
        var session = await _runtime.StartSessionAsync(CurrentAgent.Id);
        _currentSessionId = session.Record.SessionId;
        
        // Load history if needed
        var history = await _runtime.GetHistoryAsync(_currentSessionId.Value);
        foreach (var entry in history.Where(e => e.Message != null))
        {
            var msg = entry.Message!;
            Messages.Add(new MessageViewModel(
                msg.Content, 
                msg.Role == PromptMessageRole.Assistant ? CurrentAgent.Name : "User", 
                msg.Role == PromptMessageRole.Assistant));
        }
    }
}
