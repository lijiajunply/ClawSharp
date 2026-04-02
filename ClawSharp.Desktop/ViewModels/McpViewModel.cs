using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ClawSharp.Lib.Mcp;
using ReactiveUI;

namespace ClawSharp.Desktop.ViewModels;

public class McpServerViewModel : ViewModelBase
{
    private readonly McpServerDefinition _definition;

    public string Name => _definition.Name;
    public string Command => _definition.Command;
    public string Arguments => _definition.Arguments;
    public string Capabilities => _definition.Capabilities.ToString();

    public McpServerViewModel(McpServerDefinition definition)
    {
        _definition = definition;
    }
}

public class McpViewModel : ViewModelBase
{
    private readonly IMcpServerCatalog _catalog;
    private readonly ISmitheryClient _smithery;
    private readonly IMcpInstaller _installer;

    public ObservableCollection<McpServerViewModel> InstalledServers { get; } = new();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public McpViewModel(IMcpServerCatalog catalog, ISmitheryClient smithery, IMcpInstaller installer)
    {
        _catalog = catalog;
        _smithery = smithery;
        _installer = installer;

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);

        Task.Run(RefreshAsync);
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var servers = _catalog.GetAll();
            InstalledServers.Clear();
            foreach (var server in servers)
            {
                InstalledServers.Add(new McpServerViewModel(server));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
