namespace ClawSharp.Lib.Agents;

using ClawSharp.Lib.Runtime;

/// <summary>
/// Base class for custom agent implementations.
/// </summary>
public abstract class Agent(AgentDefinition definition) : IAgent
{
    /// <inheritdoc />
    public AgentDefinition Definition => definition;
}
