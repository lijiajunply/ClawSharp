using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ClawSharp.Lib.Runtime;
using ReactiveUI;

namespace ClawSharp.Desktop.ViewModels;

public class ChatViewModel : ViewModelBase
{
    private readonly IClawRuntime _runtime;
    private readonly IClawKernel _kernel;
    private SessionId? _currentSessionId;

    public ObservableCollection<ThreadSpaceRecord> ThreadSpaces { get; } = [];
    public ObservableCollection<SessionRecord> RecentSessions { get; } = [];
    public ObservableCollection<AgentViewModel> Agents { get; } = [];

    public ThreadSpaceRecord? CurrentThreadSpace
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public SessionRecord? SelectedSession
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public AgentViewModel? CurrentAgent
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string InputText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool IsProcessing
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<MessageViewModel> Messages { get; } = new();

    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; }
    public ReactiveCommand<Unit, Unit> NewChatCommand { get; }

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
        NewChatCommand = ReactiveCommand.CreateFromTask(NewChatAsync);

        // 监听空间切换
        this.WhenAnyValue(x => x.CurrentThreadSpace)
            .Where(x => x != null)
            .Subscribe(async space => await LoadSessionsForSpaceAsync(space!.ThreadSpaceId));

        // 监听历史会话选择
        this.WhenAnyValue(x => x.SelectedSession)
            .Where(x => x != null)
            .Subscribe(async session => await LoadSessionAsync(session!.SessionId));
    }

    private async Task NewChatAsync()
    {
        Messages.Clear();
        _currentSessionId = null;
        SelectedSession = null;
        // 默认保持当前 Agent，或者让用户重新选
    }

    private async Task LoadSessionsForSpaceAsync(ThreadSpaceId spaceId)
    {
        RecentSessions.Clear();
        var sessions = await _kernel.ThreadSpaces.ListSessionsAsync(spaceId);
        foreach (var s in sessions.OrderByDescending(x => x.StartedAt))
        {
            RecentSessions.Add(s);
        }
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || CurrentAgent == null || CurrentThreadSpace == null) return;

        var userContent = InputText;
        InputText = string.Empty;
        IsProcessing = true;

        try
        {
            if (_currentSessionId == null)
            {
                var session = await _runtime.StartSessionAsync(new StartSessionRequest(
                    CurrentAgent.Id,
                    CurrentThreadSpace.ThreadSpaceId));
                _currentSessionId = session.Record.SessionId;

                // 刷新历史列表以包含新会话
                await LoadSessionsForSpaceAsync(CurrentThreadSpace.ThreadSpaceId);
            }

            Messages.Add(new MessageViewModel(userContent, "User", false));
            await _runtime.AppendUserMessageAsync(_currentSessionId.Value, userContent);

            var aiMessage = new MessageViewModel("", CurrentAgent.Name, true);
            Messages.Add(aiMessage);

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

    public async Task InitializeAsync()
    {
        // 加载 Agents
        if (Agents.Count == 0)
        {
            var agents = _kernel.Agents.GetAll();
            foreach (var a in agents) Agents.Add(new AgentViewModel(a));
            CurrentAgent = Agents.FirstOrDefault(a => a.Id == "luckyfish") ?? Agents.FirstOrDefault();
        }

        // 加载 ThreadSpaces
        if (ThreadSpaces.Count == 0)
        {
            var spaces = await _kernel.ThreadSpaces.ListAsync();
            foreach (var s in spaces) ThreadSpaces.Add(s);
            CurrentThreadSpace = ThreadSpaces.FirstOrDefault(x => x.IsGlobal) ?? ThreadSpaces.FirstOrDefault();
        }
    }
}