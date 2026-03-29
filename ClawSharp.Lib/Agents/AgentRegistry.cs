using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Agents;

public sealed class AgentRegistry(IAgentDefinitionStore store) : IAgentRegistry
{
    private readonly Dictionary<string, AgentDefinition> _agents = new(StringComparer.OrdinalIgnoreCase);

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await store.LoadAllAsync(cancellationToken).ConfigureAwait(false);
        var map = new Dictionary<string, AgentDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (!map.TryAdd(definition.Id, definition))
            {
                throw new ValidationException($"Duplicate agent id '{definition.Id}' was found.");
            }
        }

        _agents.Clear();
        foreach (var pair in map)
        {
            _agents[pair.Key] = pair.Value;
        }
    }

    public IReadOnlyCollection<AgentDefinition> GetAll() => _agents.Values.ToArray();

    public AgentDefinition Get(string id)
    {
        if (_agents.TryGetValue(id, out var agent))
        {
            return agent;
        }

        throw new KeyNotFoundException($"Agent '{id}' was not found.");
    }
}
