using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Core;

namespace ClawSharp.Lib.Agents;

/// <summary>
/// 从文件系统中的 <c>agent.md</c> 文件以及用户目录下的 Markdown 文件加载 agent 定义。
/// </summary>
/// <param name="options">用于解析 agent 目录的库配置。</param>
public sealed class FileSystemAgentDefinitionStore(ClawOptions options) : IAgentDefinitionStore
{
    private readonly MarkdownAgentParser _parser = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentDefinition>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        var definitions = new List<AgentDefinition>();

        // 1. 从 Workspace 加载 (保持向下兼容，仅查找 agent.md)
        var workspacePath = ResolvePath(options.Agents.AgentsPath);
        if (Directory.Exists(workspacePath))
        {
            var workspaceDefinitions = Directory.EnumerateFiles(workspacePath, "agent.md", SearchOption.AllDirectories)
                .Select(path => _parser.Parse(File.ReadAllText(path), DynamicSourceType.Workspace, path));
            definitions.AddRange(workspaceDefinitions);
        }

        // 2. 从用户主目录加载 (~/.agent/*.md)
        var userPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agent");
        if (Directory.Exists(userPath))
        {
            var userDefinitions = Directory.EnumerateFiles(userPath, "*.md", SearchOption.TopDirectoryOnly)
                .Select(path => _parser.Parse(File.ReadAllText(path), DynamicSourceType.User, path));
            definitions.AddRange(userDefinitions);
        }

        return Task.FromResult<IReadOnlyList<AgentDefinition>>(definitions);
    }

    private string ResolvePath(string relativeOrAbsolute) =>
        Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(options.Runtime.WorkspaceRoot, relativeOrAbsolute);
}
