using System;
using System.Collections.ObjectModel;
using System.IO;
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

    private ThreadSpaceRecord? _currentThreadSpace;
    public ThreadSpaceRecord? CurrentThreadSpace
    {
        get => _currentThreadSpace;
        set => this.RaiseAndSetIfChanged(ref _currentThreadSpace, value);
    }

    private SessionRecord? _selectedSession;
    public SessionRecord? SelectedSession
    {
        get => _selectedSession;
        set => this.RaiseAndSetIfChanged(ref _selectedSession, value);
    }

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
        set 
        {
            this.RaiseAndSetIfChanged(ref _inputText, value);
            UpdateSuggestions();
        }
    }

    private ObservableCollection<string> _suggestions = new();
    public ObservableCollection<string> Suggestions => _suggestions;

    private bool _isSuggestionsVisible;
    public bool IsSuggestionsVisible
    {
        get => _isSuggestionsVisible;
        set => this.RaiseAndSetIfChanged(ref _isSuggestionsVisible, value);
    }

    private int _selectedSuggestionIndex;
    public int SelectedSuggestionIndex
    {
        get => _selectedSuggestionIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedSuggestionIndex, value);
    }

    private void UpdateSuggestions()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            IsSuggestionsVisible = false;
            return;
        }

        var text = InputText;
        _suggestions.Clear();

        if (text.StartsWith("/"))
        {
            var cmdPart = text[1..].ToLower();
            var commands = new[] { "help", "new", "clear", "agents", "spaces", "config", "history", "stats", "hub", "mcp", "speckit", "plan", "quit" };
            foreach (var cmd in commands.Where(c => c.StartsWith(cmdPart)))
            {
                _suggestions.Add("/" + cmd);
            }
        }
        else
        {
            var lastAtIndex = text.LastIndexOf('@');
            if (lastAtIndex >= 0 && (lastAtIndex == 0 || char.IsWhiteSpace(text[lastAtIndex - 1])))
            {
                var partialPath = text[(lastAtIndex + 1)..].Replace('/', Path.DirectorySeparatorChar);
                var baseDir = CurrentThreadSpace?.BoundFolderPath;

                if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
                {
                    try
                    {
                        var searchDir = baseDir;
                        var searchPattern = "*";
                        
                        if (partialPath.Contains(Path.DirectorySeparatorChar))
                        {
                            var lastSeparatorPos = partialPath.LastIndexOf(Path.DirectorySeparatorChar);
                            var subPath = partialPath[..lastSeparatorPos];
                            searchDir = Path.Combine(baseDir, subPath);
                            searchPattern = partialPath[(lastSeparatorPos + 1)..] + "*";
                        }
                        else
                        {
                            searchPattern = partialPath + "*";
                        }

                        if (Directory.Exists(searchDir))
                        {
                            var entries = Directory.GetFileSystemEntries(searchDir, searchPattern)
                                .Select(e => Path.GetRelativePath(baseDir, e))
                                .Select(e => e.Replace(Path.DirectorySeparatorChar, '/'))
                                .Take(20);

                            foreach (var entry in entries)
                            {
                                _suggestions.Add("@" + entry);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore file system errors
                    }
                }
            }
        }

        IsSuggestionsVisible = _suggestions.Any();
        if (IsSuggestionsVisible) SelectedSuggestionIndex = 0;
    }

    public void ApplySelectedSuggestion()
    {
        if (SelectedSuggestionIndex >= 0 && SelectedSuggestionIndex < Suggestions.Count)
        {
            ApplySuggestion(Suggestions[SelectedSuggestionIndex]);
        }
    }

    public void ApplySuggestion(string suggestion)
    {
        if (suggestion.StartsWith("/"))
        {
            InputText = suggestion + " ";
        }
        else if (suggestion.StartsWith("@"))
        {
            var lastAtIndex = InputText.LastIndexOf('@');
            if (lastAtIndex >= 0)
            {
                InputText = InputText[..lastAtIndex] + suggestion + " ";
            }
        }
        IsSuggestionsVisible = false;
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        private set => this.RaiseAndSetIfChanged(ref _isProcessing, value);
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
            x => x.CurrentThreadSpace,
            (text, processing, agent, space) => 
            {
                var hasText = !string.IsNullOrWhiteSpace(text);
                var isNotProcessing = !processing;
                var hasAgent = agent != null;
                var hasSpace = space != null;
                
                // 开发者调试：如果在开发环境下，可以取消注释下面这行来观察原因
                // System.Diagnostics.Debug.WriteLine($"CanSend check: Text={hasText}, !Proc={isNotProcessing}, Agent={hasAgent}, Space={hasSpace}");
                
                return hasText && isNotProcessing && hasAgent && hasSpace;
            });

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
        try
        {
            // 加载 ThreadSpaces
            if (ThreadSpaces.Count == 0)
            {
                var spaces = await _kernel.ThreadSpaces.ListAsync();
                foreach (var s in spaces) ThreadSpaces.Add(s);
                CurrentThreadSpace = ThreadSpaces.FirstOrDefault(x => x.IsGlobal) ?? ThreadSpaces.FirstOrDefault();
            }

            // 加载 Agents
            if (Agents.Count == 0)
            {
                // 确保 Registry 已加载 (如果是单例且尚未加载，GetAll 会返回空)
                await _kernel.Agents.ReloadAsync();
                
                var agents = _kernel.Agents.GetAll();
                foreach (var a in agents) Agents.Add(new AgentViewModel(a));
                CurrentAgent = Agents.FirstOrDefault(a => a.Id == "luckyfish") ?? Agents.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            // 如果初始化失败，至少尝试显示一个系统消息（如果会话已经存在）
            Messages.Add(new MessageViewModel($"Failed to initialize chat: {ex.Message}", "System", false));
        }
    }
}