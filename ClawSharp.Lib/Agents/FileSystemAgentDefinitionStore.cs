using ClawSharp.Lib.Configuration;

namespace ClawSharp.Lib.Agents;

public sealed class FileSystemAgentDefinitionStore(ClawOptions options) : IAgentDefinitionStore
{
    private readonly MarkdownAgentParser _parser = new();

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
