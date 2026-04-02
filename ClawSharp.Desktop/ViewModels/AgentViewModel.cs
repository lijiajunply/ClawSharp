using ClawSharp.Lib.Agents;
using ReactiveUI;

namespace ClawSharp.Desktop.ViewModels;

public class AgentViewModel : ViewModelBase
{
    private string _name;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private string _description;
    public string Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }

    public string Id { get; }

    public AgentViewModel(AgentDefinition agent)
    {
        Id = agent.Id;
        _name = agent.Name ?? agent.Id;
        _description = agent.Description ?? string.Empty;
    }
}
