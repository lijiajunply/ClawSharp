using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;
using ReactiveUI;

namespace ClawSharp.Desktop.ViewModels;

public class BootstrapViewModel : ViewModelBase
{
    private string _workspaceRoot = ".";
    public string WorkspaceRoot
    {
        get => _workspaceRoot;
        set => this.RaiseAndSetIfChanged(ref _workspaceRoot, value);
    }

    private string _dataPath = ".clawsharp";
    public string DataPath
    {
        get => _dataPath;
        set => this.RaiseAndSetIfChanged(ref _dataPath, value);
    }

    private ObservableCollection<ProviderTemplate> _availableProviders = new();
    public ObservableCollection<ProviderTemplate> AvailableProviders
    {
        get => _availableProviders;
        set => this.RaiseAndSetIfChanged(ref _availableProviders, value);
    }

    private ProviderTemplate? _selectedProvider;
    public ProviderTemplate? SelectedProvider
    {
        get => _selectedProvider;
        set 
        {
            this.RaiseAndSetIfChanged(ref _selectedProvider, value);
            this.RaisePropertyChanged(nameof(RequiresApiKey));
            this.RaisePropertyChanged(nameof(NeedsCustomModel));
        }
    }

    private string _apiKey = string.Empty;
    public string ApiKey
    {
        get => _apiKey;
        set => this.RaiseAndSetIfChanged(ref _apiKey, value);
    }

    private string _defaultModel = string.Empty;
    public string DefaultModel
    {
        get => _defaultModel;
        set => this.RaiseAndSetIfChanged(ref _defaultModel, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    private string _loadingMessage = string.Empty;
    public string LoadingMessage
    {
        get => _loadingMessage;
        set => this.RaiseAndSetIfChanged(ref _loadingMessage, value);
    }

    public bool RequiresApiKey => SelectedProvider?.RequiresApiKey == true;
    public bool NeedsCustomModel => string.IsNullOrWhiteSpace(SelectedProvider?.DefaultModel) && 
                                  (SelectedProvider?.Id == "ollama-local" || SelectedProvider?.Id == "llamaedge-local");

    public ReactiveCommand<Unit, bool> FinishCommand { get; }

    public BootstrapViewModel()
    {
        FinishCommand = ReactiveCommand.CreateFromTask(ExecuteFinishAsync);
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        LoadingMessage = "Discovering environments...";
        try
        {
            var bootstrapper = new ConfigBootstrapper();
            var discovery = await EnvironmentDiscoveryInspector.DiscoverAsync();
            var templates = bootstrapper.GetProviderTemplates(discovery).ToList();
            
            AvailableProviders = new ObservableCollection<ProviderTemplate>(templates);
            SelectedProvider = AvailableProviders.FirstOrDefault();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<bool> ExecuteFinishAsync()
    {
        if (SelectedProvider == null) return false;

        IsLoading = true;
        LoadingMessage = "Saving configuration...";
        try
        {
            var bootstrapper = new ConfigBootstrapper();
            var config = new BootstrapConfig
            {
                WorkspaceRoot = WorkspaceRoot,
                DataPath = DataPath,
                DefaultProvider = SelectedProvider.Id,
                ProviderType = SelectedProvider.Type,
                BaseUrl = SelectedProvider.BaseUrl,
                DefaultModel = NeedsCustomModel ? DefaultModel : SelectedProvider.DefaultModel,
                RequestPath = SelectedProvider.RequestPath,
                SupportsResponses = SelectedProvider.SupportsResponses,
                SupportsChatCompletions = SelectedProvider.SupportsChatCompletions,
                ApiKey = ApiKey
            };

            var json = bootstrapper.GenerateConfigJson(config);
            await bootstrapper.SaveConfigAsync("appsettings.json", json);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during bootstrap: {ex}");
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
