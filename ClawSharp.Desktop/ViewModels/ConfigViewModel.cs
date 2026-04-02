using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ClawSharp.Lib.Configuration;
using ReactiveUI;
using System.Linq;

namespace ClawSharp.Desktop.ViewModels;

public class ConfigItemViewModel : ViewModelBase
{
    private readonly IConfigManager _manager;
    public string Key { get; }
    
    private string? _value;
    public string? Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public bool IsSecret { get; }

    public ConfigItemViewModel(IConfigManager manager, string key, string? value)
    {
        _manager = manager;
        Key = key;
        _value = value;
        IsSecret = manager.IsSecret(key);
    }

    public async Task SaveAsync()
    {
        await _manager.SetAsync(Key, Value);
    }
}

public class ConfigViewModel : ViewModelBase
{
    private readonly IConfigManager _manager;

    public ObservableCollection<ConfigItemViewModel> ConfigItems { get; } = new();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAllCommand { get; }

    public ConfigViewModel(IConfigManager manager)
    {
        _manager = manager;

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        SaveAllCommand = ReactiveCommand.CreateFromTask(SaveAllAsync);

        Task.Run(RefreshAsync);
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var config = await _manager.GetAllAsync(true);
            ConfigItems.Clear();
            foreach (var kvp in config.OrderBy(x => x.Key))
            {
                ConfigItems.Add(new ConfigItemViewModel(_manager, kvp.Key, kvp.Value));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveAllAsync()
    {
        IsLoading = true;
        try
        {
            foreach (var item in ConfigItems)
            {
                await item.SaveAsync();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
