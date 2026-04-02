using ReactiveUI;
using System.Threading.Tasks;
using System.Reactive;
using SukiUI;
using Avalonia.Styling;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using Material.Icons;

namespace ClawSharp.Desktop.ViewModels;

public class MenuItemViewModel(string header, MaterialIconKind icon, ViewModelBase content) : ViewModelBase
{
    public string Header { get; init; } = header;
    public MaterialIconKind Icon { get; init; } = icon;
    public ViewModelBase Content { get; init; } = content;
}

public class MainWindowViewModel : ViewModelBase
{
    public MenuItemViewModel SelectedMenuItem
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<MenuItemViewModel> MenuItems { get; } = [];

    public ReactiveCommand<Unit, Unit> ToggleThemeCommand { get; }

    public MainWindowViewModel(
        ChatViewModel chatViewModel, 
        HistoryViewModel historyViewModel,
        McpViewModel mcpViewModel,
        ConfigViewModel configViewModel)
    {
        var chat = new MenuItemViewModel("Chat", MaterialIconKind.Chat, chatViewModel);
        var history = new MenuItemViewModel("History", MaterialIconKind.History, historyViewModel);
        var mcp = new MenuItemViewModel("MCP", MaterialIconKind.Tools, mcpViewModel);
        var config = new MenuItemViewModel("Config", MaterialIconKind.Settings, configViewModel);

        MenuItems.Add(chat);
        MenuItems.Add(history);
        MenuItems.Add(mcp);
        MenuItems.Add(config);
        
        SelectedMenuItem = chat;
        
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
                SelectedMenuItem = chat;
            });
        });
    }
}
