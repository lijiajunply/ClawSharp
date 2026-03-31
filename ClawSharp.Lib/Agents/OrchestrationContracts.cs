namespace ClawSharp.Lib.Agents;

using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Tools;

/// <summary>
/// Basic contract for an agent.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Gets the underlying agent definition.
    /// </summary>
    AgentDefinition Definition { get; }
}

/// <summary>
/// Responsible for planning and delegating sub-tasks to tools (including AgentTool).
/// </summary>
public interface IOrchestratorAgent : IAgent
{
    /// <summary>
    /// Generates a list of steps for the orchestration plan.
    /// </summary>
    Task<Plan> GeneratePlanAsync(string userTask, TurnContext context);
    
    /// <summary>
    /// Executes the orchestration plan.
    /// </summary>
    Task<RunTurnResult> ExecutePlanAsync(Plan plan, TurnContext context);
}

/// <summary>
/// Responsible for creating tool representations of registered agents.
/// </summary>
public interface IAgentToolProvider
{
    /// <summary>
    /// Discovers tools representing agents in the system.
    /// </summary>
    IEnumerable<IToolExecutor> DiscoverAgentTools();
    
    /// <summary>
    /// Creates a tool representation of a specific agent.
    /// </summary>
    IToolExecutor CreateToolFromAgent(IAgent agent);

    /// <summary>
    /// Creates a tool representation of a specific agent definition.
    /// </summary>
    IToolExecutor CreateToolFromAgent(AgentDefinition definition);
}

/// <summary>
/// Responsible for enforcing tool access restrictions (Least Privilege).
/// </summary>
public interface IPermissionScopeManager
{
    /// <summary>
    /// Sets the current permission scope.
    /// </summary>
    void SetScope(PermissionScope scope);
    
    /// <summary>
    /// Determines if a tool can be invoked within the current scope.
    /// </summary>
    bool CanInvokeTool(string toolName);
}
