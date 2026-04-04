using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ClawSharp.Desktop.ViewModels;

namespace ClawSharp.Desktop.Views;

public partial class ChatView : ReactiveUserControl<ChatViewModel>
{
    public ChatView()
    {
        InitializeComponent();
        
        var textBox = this.FindControl<TextBox>("InputTextBox");
        if (textBox != null)
        {
            textBox.AddHandler(KeyDownEvent, OnTextBoxKeyDown, RoutingStrategies.Tunnel);
        }
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel == null || !ViewModel.IsSuggestionsVisible) return;

        switch (e.Key)
        {
            case Key.Up:
                if (ViewModel.Suggestions.Count > 0)
                {
                    ViewModel.SelectedSuggestionIndex = (ViewModel.SelectedSuggestionIndex - 1 + ViewModel.Suggestions.Count) % ViewModel.Suggestions.Count;
                }
                e.Handled = true;
                break;
            case Key.Down:
                if (ViewModel.Suggestions.Count > 0)
                {
                    ViewModel.SelectedSuggestionIndex = (ViewModel.SelectedSuggestionIndex + 1) % ViewModel.Suggestions.Count;
                }
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Tab:
                ViewModel.ApplySelectedSuggestion();
                e.Handled = true;
                break;
            case Key.Escape:
                ViewModel.IsSuggestionsVisible = false;
                e.Handled = true;
                break;
        }
    }

    private void OnSuggestionsTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.ApplySelectedSuggestion();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
