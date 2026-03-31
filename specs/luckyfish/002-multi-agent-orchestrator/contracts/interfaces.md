# Interface Contracts: Multi-Agent Orchestrator

## IOrchestratorAgent (Extension of IAgent)
- Responsible for planning and delegating sub-tasks to tools (including `AgentTool`).
```csharp
public interface IOrchestratorAgent : IAgent
{
    Task<Plan> GeneratePlanAsync(string userTask, TurnContext context);
    Task<TurnResult> ExecutePlanAsync(Plan plan, TurnContext context);
}
```

## IAgentToolProvider
- Responsible for creating tool representations of registered agents.
```csharp
public interface IAgentToolProvider
{
    IEnumerable<ITool> DiscoverAgentTools();
    ITool CreateToolFromAgent(IAgent agent);
}
```

## IPermissionScopeManager
- Responsible for enforcing tool access restrictions (Least Privilege).
```csharp
public interface IPermissionScopeManager
{
    void SetScope(PermissionScope scope);
    bool CanInvokeTool(string toolName);
}
```
