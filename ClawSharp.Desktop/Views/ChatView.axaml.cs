using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ClawSharp.Desktop.ViewModels;

namespace ClawSharp.Desktop.Views;

public partial class ChatView : ReactiveUserControl<ChatViewModel>
{
    public ChatView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
