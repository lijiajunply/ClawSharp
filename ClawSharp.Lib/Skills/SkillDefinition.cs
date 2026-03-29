using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Skills;

public sealed record SkillDefinition(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> RequiredTools,
    IReadOnlyList<string> RequiredMcpServers,
    string Entry,
    string Version,
    string Body)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) ||
            string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(Description) ||
            string.IsNullOrWhiteSpace(Entry) ||
            string.IsNullOrWhiteSpace(Version))
        {
            throw new ValidationException("Skill definition is missing one or more required fields.");
        }
    }
}

public interface ISkillDefinitionStore
{
    Task<IReadOnlyList<SkillDefinition>> LoadAllAsync(CancellationToken cancellationToken = default);
}

public interface ISkillRegistry
{
    Task ReloadAsync(CancellationToken cancellationToken = default);

    IReadOnlyCollection<SkillDefinition> GetAll();

    SkillDefinition Get(string id);
}
