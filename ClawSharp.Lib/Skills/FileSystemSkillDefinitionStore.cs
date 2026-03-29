using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Skills;

public sealed class FileSystemSkillDefinitionStore(ClawOptions options) : ISkillDefinitionStore
{
    private readonly MarkdownSkillParser _parser = new();

    public Task<IReadOnlyList<SkillDefinition>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        var basePath = ResolvePath(options.Agents.SkillsPath);
        if (!Directory.Exists(basePath))
        {
            return Task.FromResult<IReadOnlyList<SkillDefinition>>([]);
        }

        var definitions = Directory.EnumerateFiles(basePath, "SKILL.md", SearchOption.AllDirectories)
            .Select(path => _parser.Parse(File.ReadAllText(path)))
            .ToList();

        return Task.FromResult<IReadOnlyList<SkillDefinition>>(definitions);
    }

    private string ResolvePath(string relativeOrAbsolute) =>
        Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(options.Runtime.WorkspaceRoot, relativeOrAbsolute);
}
