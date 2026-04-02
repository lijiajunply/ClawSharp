using System;
using ReactiveUI;

namespace ClawSharp.Desktop.ViewModels;

public class MessageViewModel : ViewModelBase
{
    private string _content = string.Empty;
    public string Content
    {
        get => _content;
        set => this.RaiseAndSetIfChanged(ref _content, value);
    }

    private string _sender = string.Empty;
    public string Sender
    {
        get => _sender;
        set => this.RaiseAndSetIfChanged(ref _sender, value);
    }

    private DateTime _timestamp;
    public DateTime Timestamp
    {
        get => _timestamp;
        set => this.RaiseAndSetIfChanged(ref _timestamp, value);
    }

    private bool _isAi;
    public bool IsAi
    {
        get => _isAi;
        set => this.RaiseAndSetIfChanged(ref _isAi, value);
    }

    public MessageViewModel(string content, string sender, bool isAi)
    {
        Content = content;
        Sender = sender;
        IsAi = isAi;
        Timestamp = DateTime.Now;
    }
}
