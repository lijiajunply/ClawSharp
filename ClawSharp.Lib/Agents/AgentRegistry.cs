using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Agents;

/// <summary>
/// 默认的 agent 注册表实现，负责去重并缓存已加载的 agent 定义。
/// </summary>
/// <param name="store">agent 定义来源。</param>
public sealed class AgentRegistry(IAgentDefinitionStore store) : IAgentRegistry
{
    private readonly Dictionary<string, AgentDefinition> _agents = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await store.LoadAllAsync(cancellationToken).ConfigureAwait(false);
        var map = new Dictionary<string, AgentDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            var finalId = definition.Source == DynamicSourceType.User
                ? $"user.{definition.Id}"
                : definition.Id;

            if (!map.TryAdd(finalId, definition with { Id = finalId }))
            {
                throw new ValidationException($"Duplicate agent id '{finalId}' was found.");
            }
        }

        _agents.Clear();
        foreach (var pair in map)
        {
            _agents[pair.Key] = pair.Value;
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AgentDefinition> GetAll() => _agents.Values.ToArray();

    /// <inheritdoc />
    public AgentDefinition Get(string id)
    {
        if (_agents.TryGetValue(id, out var agent))
        {
            return agent;
        }

        throw new KeyNotFoundException($"Agent '{id}' was not found.");
    }
}
