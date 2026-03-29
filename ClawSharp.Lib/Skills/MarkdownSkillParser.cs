using ClawSharp.Lib.Agents;
using ClawSharp.Lib.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ClawSharp.Lib.Skills;

public sealed class MarkdownSkillParser
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public SkillDefinition Parse(string markdown)
    {
        var (frontMatter, body) = MarkdownFrontMatter.Parse(markdown);
        var dto = _deserializer.Deserialize<SkillFrontMatter>(frontMatter)
                  ?? throw new ValidationException("Skill frontmatter is missing or invalid.");

        var definition = new SkillDefinition(
            dto.Id ?? string.Empty,
            dto.Name ?? string.Empty,
            dto.Description ?? string.Empty,
            dto.Inputs ?? [],
            dto.Outputs ?? [],
            dto.Dependencies ?? [],
            dto.RequiredTools ?? [],
            dto.RequiredMcpServers ?? [],
            dto.Entry ?? string.Empty,
            dto.Version ?? string.Empty,
            body.Trim());

        definition.Validate();
        return definition;
    }

    private sealed class SkillFrontMatter
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }
        public List<string>? Inputs { get; init; }
        public List<string>? Outputs { get; init; }
        public List<string>? Dependencies { get; init; }
        public List<string>? RequiredTools { get; init; }
        public List<string>? RequiredMcpServers { get; init; }
        public string? Entry { get; init; }
        public string? Version { get; init; }
    }
}
