namespace ClawSharp.Lib.Agents;

using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Tools;

/// <summary>
/// Discovers agents from the file system (via AgentRegistry) and provides them as tools.
/// </summary>
public sealed class FileSystemAgentToolProvider(IAgentRegistry registry, IServiceProvider serviceProvider) : IAgentToolProvider
{
    /// <inheritdoc />
    public IEnumerable<IToolExecutor> DiscoverAgentTools()
    {
        return registry.GetAll().Select(CreateToolFromAgent);
    }

    /// <inheritdoc />
    public IToolExecutor CreateToolFromAgent(IAgent agent)
    {
        return new AgentTool(agent, serviceProvider);
    }

    /// <inheritdoc />
    public IToolExecutor CreateToolFromAgent(AgentDefinition definition)
    {
        return new AgentTool(new SimpleAgentWrapper(definition), serviceProvider);
    }

    private sealed class SimpleAgentWrapper(AgentDefinition definition) : IAgent
    {
        public AgentDefinition Definition => definition;
    }
}
