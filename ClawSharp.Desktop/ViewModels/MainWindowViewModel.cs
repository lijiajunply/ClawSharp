using ReactiveUI;
using System.Threading.Tasks;
using System.Reactive;
using SukiUI;
using Avalonia.Styling;
using System.Collections.ObjectModel;
using System.Linq;
using System;

namespace ClawSharp.Desktop.ViewModels;

public class MenuItemViewModel(string header, object icon, ViewModelBase content) : ViewModelBase
{
    public string Header { get; init; } = header;
    public object Icon { get; init; } = icon;
    public ViewModelBase Content { get; init; } = content;
}

public class MainWindowViewModel : ViewModelBase
{
    public MenuItemViewModel? SelectedMenuItem
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<MenuItemViewModel> MenuItems { get; } = [];

    public ReactiveCommand<Unit, Unit> ToggleThemeCommand { get; }
    
    public MainWindowViewModel(){}

    public MainWindowViewModel(
        ChatViewModel chatViewModel, 
        HistoryViewModel historyViewModel,
        McpViewModel mcpViewModel,
        ConfigViewModel configViewModel)
    {
        MenuItems.Add(new MenuItemViewModel("Chat", "💬", chatViewModel));
        MenuItems.Add(new MenuItemViewModel("History", "📜", historyViewModel));
        MenuItems.Add(new MenuItemViewModel("MCP", "🛠️", mcpViewModel));
        MenuItems.Add(new MenuItemViewModel("Config", "⚙️", configViewModel));
        
        SelectedMenuItem = MenuItems.First();
        
        ToggleThemeCommand = ReactiveCommand.Create(() =>
        {
            var theme = SukiTheme.GetInstance();
            var newVariant = theme.ActiveBaseTheme == ThemeVariant.Dark 
                ? ThemeVariant.Light 
                : ThemeVariant.Dark;
            
            theme.ChangeBaseTheme(newVariant);
        });

        // Initialize components
        Avalonia.Threading.Dispatcher.UIThread.Post(async () => 
        {
            await chatViewModel.InitializeAsync();
        });
        
        // Listen for history selection to switch back to chat
        historyViewModel.SessionSelected.Subscribe(async session =>
        {
            await chatViewModel.LoadSessionAsync(session.SessionId);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                SelectedMenuItem = MenuItems.First(m => m.Header == "Chat");
            });
        });
    }
}
