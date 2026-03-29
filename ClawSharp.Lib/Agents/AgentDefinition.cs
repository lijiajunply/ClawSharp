using ClawSharp.Lib.Core;
using ClawSharp.Lib.Tools;

namespace ClawSharp.Lib.Agents;

public sealed record AgentDefinition(
    string Id,
    string Name,
    string Description,
    string Model,
    string SystemPrompt,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Skills,
    string MemoryScope,
    IReadOnlyList<string> McpServers,
    ToolPermissionSet Permissions,
    string Version,
    string Body)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) ||
            string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(Description) ||
            string.IsNullOrWhiteSpace(Model) ||
            string.IsNullOrWhiteSpace(SystemPrompt) ||
            string.IsNullOrWhiteSpace(MemoryScope) ||
            string.IsNullOrWhiteSpace(Version))
        {
            throw new ValidationException("Agent definition is missing one or more required fields.");
        }
    }
}

public interface IAgentDefinitionStore
{
    Task<IReadOnlyList<AgentDefinition>> LoadAllAsync(CancellationToken cancellationToken = default);
}

public interface IAgentRegistry
{
    Task ReloadAsync(CancellationToken cancellationToken = default);

    IReadOnlyCollection<AgentDefinition> GetAll();

    AgentDefinition Get(string id);
}
