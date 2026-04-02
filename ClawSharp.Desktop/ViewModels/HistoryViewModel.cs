using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ClawSharp.Lib.Core;
using ClawSharp.Lib.Runtime;
using ReactiveUI;

namespace ClawSharp.Desktop.ViewModels;

public class HistoryViewModel : ViewModelBase
{
    private readonly IClawKernel _kernel;

    public ObservableCollection<ThreadSpaceRecord> ThreadSpaces { get; } = new();
    
    private ThreadSpaceRecord? _selectedThreadSpace;
    public ThreadSpaceRecord? SelectedThreadSpace
    {
        get => _selectedThreadSpace;
        set 
        {
            this.RaiseAndSetIfChanged(ref _selectedThreadSpace, value);
            if (value != null)
            {
                Task.Run(() => LoadSessionsAsync(value.ThreadSpaceId));
            }
        }
    }

    public ObservableCollection<SessionRecord> Sessions { get; } = new();

    private SessionRecord? _selectedSession;
    public SessionRecord? SelectedSession
    {
        get => _selectedSession;
        set => this.RaiseAndSetIfChanged(ref _selectedSession, value);
    }

    private readonly Subject<SessionRecord> _sessionSelected = new();
    public IObservable<SessionRecord> SessionSelected => _sessionSelected;

    public ReactiveCommand<Unit, Unit> OpenSessionCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public HistoryViewModel(IClawKernel kernel)
    {
        _kernel = kernel;

        OpenSessionCommand = ReactiveCommand.Create(() =>
        {
            if (SelectedSession != null)
            {
                _sessionSelected.OnNext(SelectedSession);
            }
        }, this.WhenAnyValue(x => x.SelectedSession).Select(s => s != null));

        RefreshCommand = ReactiveCommand.CreateFromTask(InitializeAsync);

        Task.Run(InitializeAsync);
    }

    public async Task InitializeAsync()
    {
        var spaces = await _kernel.ThreadSpaces.ListAsync();
        ThreadSpaces.Clear();
        foreach (var space in spaces)
        {
            ThreadSpaces.Add(space);
        }

        if (SelectedThreadSpace == null && ThreadSpaces.Count > 0)
        {
            SelectedThreadSpace = ThreadSpaces[0];
        }
        else if (SelectedThreadSpace != null)
        {
            await LoadSessionsAsync(SelectedThreadSpace.ThreadSpaceId);
        }
    }

    private async Task LoadSessionsAsync(ThreadSpaceId spaceId)
    {
        var sessions = await _kernel.ThreadSpaces.ListSessionsAsync(spaceId);
        Sessions.Clear();
        foreach (var session in sessions)
        {
            Sessions.Add(session);
        }
    }
}
