using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Skills;

/// <summary>
/// 从文件系统中的 <c>SKILL.md</c> 文件以及用户目录下的 Markdown 文件加载 skill 定义。
/// </summary>
/// <param name="options">用于解析 skill 目录的库配置。</param>
public sealed class FileSystemSkillDefinitionStore(ClawOptions options) : ISkillDefinitionStore
{
    private readonly MarkdownSkillParser _parser = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<SkillDefinition>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        var definitions = new List<SkillDefinition>();

        // 1. 从 Workspace 加载 (保持向下兼容，仅查找 SKILL.md)
        var workspacePath = ResolvePath(options.Agents.SkillsPath);
        if (Directory.Exists(workspacePath))
        {
            var workspaceDefinitions = Directory.EnumerateFiles(workspacePath, "SKILL.md", SearchOption.AllDirectories)
                .Select(path => _parser.Parse(File.ReadAllText(path), DynamicSourceType.Workspace, path));
            definitions.AddRange(workspaceDefinitions);
        }

        // 2. 从用户主目录加载 (~/.skills/*.md)
        var userPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".skills");
        if (Directory.Exists(userPath))
        {
            var userFiles = Directory.EnumerateFiles(userPath, "*.md", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(userPath, "SKILL.md", SearchOption.AllDirectories));
            var userDefinitions = userFiles
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => _parser.Parse(File.ReadAllText(path), DynamicSourceType.User, path));
            definitions.AddRange(userDefinitions);
        }

        return Task.FromResult<IReadOnlyList<SkillDefinition>>(definitions);
    }

    private string ResolvePath(string relativeOrAbsolute) =>
        Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(options.Runtime.WorkspaceRoot, relativeOrAbsolute);
}
