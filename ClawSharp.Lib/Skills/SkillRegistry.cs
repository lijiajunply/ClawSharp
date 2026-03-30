using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Skills;

/// <summary>
/// 默认的 skill 注册表实现，负责去重并缓存已加载的 skill 定义。
/// </summary>
/// <param name="store">skill 定义来源。</param>
public sealed class SkillRegistry(ISkillDefinitionStore store) : ISkillRegistry
{
    private readonly Dictionary<string, SkillDefinition> _skills = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await store.LoadAllAsync(cancellationToken).ConfigureAwait(false);
        var map = new Dictionary<string, SkillDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            var finalId = definition.Source == DynamicSourceType.User 
                ? $"user.{definition.Id}" 
                : definition.Id;

            if (!map.TryAdd(finalId, definition with { Id = finalId }))
            {
                throw new ValidationException($"Duplicate skill id '{finalId}' was found.");
            }
        }

        _skills.Clear();
        foreach (var pair in map)
        {
            _skills[pair.Key] = pair.Value;
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<SkillDefinition> GetAll() => _skills.Values.ToArray();

    /// <inheritdoc />
    public SkillDefinition Get(string id)
    {
        return _skills.TryGetValue(id, out var skill)
            ? skill
            : throw new KeyNotFoundException($"Skill '{id}' was not found.");
    }
}