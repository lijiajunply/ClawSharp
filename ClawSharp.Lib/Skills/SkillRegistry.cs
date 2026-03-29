using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Skills;

public sealed class SkillRegistry(ISkillDefinitionStore store) : ISkillRegistry
{
    private readonly Dictionary<string, SkillDefinition> _skills = new(StringComparer.OrdinalIgnoreCase);

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await store.LoadAllAsync(cancellationToken).ConfigureAwait(false);
        var map = new Dictionary<string, SkillDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (!map.TryAdd(definition.Id, definition))
            {
                throw new ValidationException($"Duplicate skill id '{definition.Id}' was found.");
            }
        }

        _skills.Clear();
        foreach (var pair in map)
        {
            _skills[pair.Key] = pair.Value;
        }
    }

    public IReadOnlyCollection<SkillDefinition> GetAll() => _skills.Values.ToArray();

    public SkillDefinition Get(string id)
    {
        if (_skills.TryGetValue(id, out var skill))
        {
            return skill;
        }

        throw new KeyNotFoundException($"Skill '{id}' was not found.");
    }
}
