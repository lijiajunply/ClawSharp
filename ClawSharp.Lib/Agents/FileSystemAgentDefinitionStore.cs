using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Agents;

/// <summary>
/// 从文件系统中的 <c>agent.md</c> 文件加载 agent 定义。
/// </summary>
/// <param name="options">用于解析 agent 目录的库配置。</param>
public sealed class FileSystemAgentDefinitionStore(ClawOptions options) : IAgentDefinitionStore
{
    private readonly MarkdownAgentParser _parser = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentDefinition>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        var basePath = ResolvePath(options.Agents.AgentsPath);
        if (!Directory.Exists(basePath))
        {
            return Task.FromResult<IReadOnlyList<AgentDefinition>>([]);
        }

        var definitions = Directory.EnumerateFiles(basePath, "agent.md", SearchOption.AllDirectories)
            .Select(path => _parser.Parse(File.ReadAllText(path)))
            .ToList();

        return Task.FromResult<IReadOnlyList<AgentDefinition>>(definitions);
    }

    private string ResolvePath(string relativeOrAbsolute) =>
        Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(options.Runtime.WorkspaceRoot, relativeOrAbsolute);
}
