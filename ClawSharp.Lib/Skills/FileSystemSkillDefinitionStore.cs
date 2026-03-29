using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Skills;

/// <summary>
/// 从文件系统中的 <c>SKILL.md</c> 文件加载 skill 定义。
/// </summary>
/// <param name="options">用于解析 skill 目录的库配置。</param>
public sealed class FileSystemSkillDefinitionStore(ClawOptions options) : ISkillDefinitionStore
{
    private readonly MarkdownSkillParser _parser = new();

    /// <inheritdoc />
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
